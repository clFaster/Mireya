using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mireya.ApiClient.Models;
using Mireya.ApiClient.Options;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.Data;
using Mireya.Database.Models;

namespace Mireya.Client.Avalonia.Services;

public interface ILocalAssetSyncService
{
    event Action<Guid, string, int>? OnSyncProgressChanged;
    event Action<Guid, string>? OnCampaignSyncCompleted;
    event Action<Guid, string>? OnAssetSyncFailed;

    Task SyncCampaignsAsync(
        List<CampaignSyncInfo> campaigns,
        CancellationToken cancellationToken = default
    );
    Task<List<Guid>> GetMissingAssetIdsAsync(List<Guid> requiredAssetIds);
    Task<bool> IsAssetDownloadedAsync(Guid assetId);
    string GetAssetLocalPath(Guid assetId);
}

public class LocalAssetSyncService : ILocalAssetSyncService
{
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly string _assetCacheBaseDirectory;
    private readonly IBackendManager _backendManager;
    private readonly string _baseUrl;
    private readonly LocalDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocalAssetSyncService> _logger;

    public LocalAssetSyncService(
        LocalDbContext db,
        IBackendManager backendManager,
        IHttpClientFactory httpClientFactory,
        IAccessTokenProvider accessTokenProvider,
        IOptions<MireyaApiClientOptions> options,
        ILogger<LocalAssetSyncService> logger
    )
    {
        _db = db;
        _backendManager = backendManager;
        _httpClient = httpClientFactory.CreateClient();
        _accessTokenProvider = accessTokenProvider;
        _logger = logger;
        _baseUrl = options.Value.BaseUrl.TrimEnd('/');

        // Set up base cache directory for assets
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _assetCacheBaseDirectory = Path.Combine(appDataPath, "Mireya", "AssetCache");
        Directory.CreateDirectory(_assetCacheBaseDirectory);

        _logger.LogInformation("Asset cache base directory: {Directory}", _assetCacheBaseDirectory);
    }

    public event Action<Guid, string, int>? OnSyncProgressChanged;
    public event Action<Guid, string>? OnCampaignSyncCompleted;
    public event Action<Guid, string>? OnAssetSyncFailed;

    public async Task SyncCampaignsAsync(
        List<CampaignSyncInfo> campaigns,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "=== START SYNC === Syncing {CampaignCount} campaigns",
            campaigns.Count
        );

        var backend = await _backendManager.GetCurrentBackendAsync();
        if (backend == null)
        {
            _logger.LogError("Cannot sync: No current backend configured");
            return;
        }

        _logger.LogInformation(
            "Syncing campaigns for backend {BackendId} - {BaseUrl}",
            backend.Id,
            backend.BaseUrl
        );

        foreach (var campaign in campaigns)
            _logger.LogInformation(
                "Campaign: {CampaignId} - {CampaignName} with {AssetCount} assets",
                campaign.CampaignId,
                campaign.CampaignName,
                campaign.Assets.Count
            );

        // Update local campaign records with backend association
        foreach (var campaign in campaigns)
        {
            _logger.LogDebug("Upserting campaign {CampaignId} to local DB", campaign.CampaignId);
            await UpsertCampaignAsync(campaign, backend.Id);
        }

        // Get all unique assets needed
        var allAssets = campaigns.SelectMany(c => c.Assets).ToList();
        var uniqueAssets = allAssets.GroupBy(a => a.AssetId).Select(g => g.First()).ToList();

        _logger.LogInformation("Total unique assets to check: {Count}", uniqueAssets.Count);

        // Download missing assets
        var downloadCount = 0;
        var skipCount = 0;
        var errorCount = 0;

        foreach (var asset in uniqueAssets)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Sync cancelled by user");
                break;
            }

            try
            {
                _logger.LogInformation(
                    "Processing asset {AssetId} - {AssetName} ({Type})",
                    asset.AssetId,
                    asset.Name,
                    asset.Type
                );

                var wasDownloaded = await SyncAssetAsync(asset, backend.Id, cancellationToken);
                if (wasDownloaded)
                    downloadCount++;
                else
                    skipCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogError(
                    ex,
                    "Failed to sync asset {AssetId} ({AssetName})",
                    asset.AssetId,
                    asset.Name
                );
                OnAssetSyncFailed?.Invoke(asset.AssetId, ex.Message);
            }
        }

        _logger.LogInformation(
            "=== SYNC COMPLETE === Downloaded: {Downloaded}, Skipped: {Skipped}, Errors: {Errors}",
            downloadCount,
            skipCount,
            errorCount
        );

        // Mark campaigns as synced
        foreach (var campaign in campaigns)
            OnCampaignSyncCompleted?.Invoke(campaign.CampaignId, campaign.CampaignName);
    }

    public async Task<List<Guid>> GetMissingAssetIdsAsync(List<Guid> requiredAssetIds)
    {
        var backend = await _backendManager.GetCurrentBackendAsync();
        if (backend == null)
        {
            _logger.LogWarning("Cannot check missing assets: No current backend");
            return requiredAssetIds; // All are missing if no backend
        }

        var downloaded = await _db
            .DownloadedAssets.Where(a =>
                a.BackendInstanceId == backend.Id
                && requiredAssetIds.Contains(a.AssetId)
                && a.IsDownloaded
            )
            .Select(a => a.AssetId)
            .ToListAsync();

        return requiredAssetIds.Except(downloaded).ToList();
    }

    public async Task<bool> IsAssetDownloadedAsync(Guid assetId)
    {
        var backend = await _backendManager.GetCurrentBackendAsync();
        if (backend == null)
            return false;

        var downloadedAsset = await _db.DownloadedAssets.FirstOrDefaultAsync(da =>
            da.BackendInstanceId == backend.Id && da.AssetId == assetId
        );

        return downloadedAsset?.IsDownloaded == true
            && !string.IsNullOrEmpty(downloadedAsset.LocalPath)
            && File.Exists(downloadedAsset.LocalPath);
    }

    public string GetAssetLocalPath(Guid assetId)
    {
        var backend = Task.Run(async () => await _backendManager.GetCurrentBackendAsync()).Result;
        if (backend == null)
        {
            _logger.LogWarning("Cannot get asset path: No current backend");
            return string.Empty;
        }

        var downloadedAsset = _db.DownloadedAssets.FirstOrDefault(da =>
            da.BackendInstanceId == backend.Id && da.AssetId == assetId
        );

        if (downloadedAsset != null && !string.IsNullOrEmpty(downloadedAsset.LocalPath))
            return downloadedAsset.LocalPath;

        // Fallback: construct expected path
        var backendCacheDir = Path.Combine(_assetCacheBaseDirectory, backend.Id.ToString());
        return Path.Combine(backendCacheDir, assetId.ToString());
    }

    private async Task<string> GetAssetCacheDirectoryAsync()
    {
        var backend = await _backendManager.GetCurrentBackendAsync();
        if (backend == null)
            throw new InvalidOperationException("No current backend configured");

        var backendCacheDir = Path.Combine(_assetCacheBaseDirectory, backend.Id.ToString());
        Directory.CreateDirectory(backendCacheDir);
        return backendCacheDir;
    }

    private async Task UpsertCampaignAsync(CampaignSyncInfo campaign, Guid backendId)
    {
        _logger.LogDebug(
            "Upserting campaign {CampaignId} - {CampaignName} for backend {BackendId}",
            campaign.CampaignId,
            campaign.CampaignName,
            backendId
        );

        var localCampaign = await _db.Campaigns.FindAsync(campaign.CampaignId);

        if (localCampaign == null)
        {
            localCampaign = new Campaign
            {
                Id = campaign.CampaignId,
                Name = campaign.CampaignName,
                Description = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.Campaigns.Add(localCampaign);
            _logger.LogDebug("Created new campaign {CampaignId}", campaign.CampaignId);
        }
        else
        {
            localCampaign.Name = campaign.CampaignName;
            localCampaign.UpdatedAt = DateTime.UtcNow;
            _logger.LogDebug("Updated existing campaign {CampaignId}", campaign.CampaignId);
        }

        await _db.SaveChangesAsync();

        // Create/update BackendCampaign mapping
        var backendCampaign = await _db.BackendCampaigns.FirstOrDefaultAsync(bc =>
            bc.BackendInstanceId == backendId && bc.CampaignId == campaign.CampaignId
        );

        if (backendCampaign == null)
        {
            backendCampaign = new BackendCampaign
            {
                BackendInstanceId = backendId,
                CampaignId = campaign.CampaignId,
                SyncedAt = DateTime.UtcNow,
            };
            _db.BackendCampaigns.Add(backendCampaign);
            _logger.LogDebug("Created BackendCampaign mapping for backend {BackendId}", backendId);
        }
        else
        {
            backendCampaign.SyncedAt = DateTime.UtcNow;
            _logger.LogDebug("Updated BackendCampaign mapping for backend {BackendId}", backendId);
        }

        await _db.SaveChangesAsync();

        // Now handle assets - ensure they exist first
        foreach (var assetInfo in campaign.Assets)
        {
            var asset = await _db.Assets.FindAsync(assetInfo.AssetId);
            if (asset == null)
            {
                asset = new Asset
                {
                    Id = assetInfo.AssetId,
                    Name = assetInfo.Name,
                    Type = Enum.Parse<AssetType>(assetInfo.Type, true),
                    Source = assetInfo.Source,
                    FileSizeBytes = assetInfo.FileSizeBytes,
                    DurationSeconds = assetInfo.DurationSeconds,
                    IsMuted = assetInfo.IsMuted,
                    CreatedAt = DateTime.UtcNow,
                };
                _db.Assets.Add(asset);
                _logger.LogDebug("Created new asset {AssetId} - {AssetName}", asset.Id, asset.Name);
            }
            else
            {
                // Keep local asset metadata in sync with server
                asset.Name = assetInfo.Name;
                asset.Type = Enum.Parse<AssetType>(assetInfo.Type, true);
                asset.Source = assetInfo.Source;
                asset.FileSizeBytes = assetInfo.FileSizeBytes;
                asset.DurationSeconds = assetInfo.DurationSeconds;
                asset.IsMuted = assetInfo.IsMuted;
                asset.UpdatedAt = DateTime.UtcNow;
            }

            // Create/update BackendAsset mapping
            var backendAsset = await _db.BackendAssets.FirstOrDefaultAsync(ba =>
                ba.BackendInstanceId == backendId && ba.AssetId == assetInfo.AssetId
            );

            if (backendAsset == null)
            {
                backendAsset = new BackendAsset
                {
                    BackendInstanceId = backendId,
                    AssetId = assetInfo.AssetId,
                    SyncedAt = DateTime.UtcNow,
                };
                _db.BackendAssets.Add(backendAsset);
            }
            else
            {
                backendAsset.SyncedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();

        // Update campaign assets - remove old ones and add new ones
        var existing = await _db
            .CampaignAssets.Where(ca => ca.CampaignId == campaign.CampaignId)
            .ToListAsync();

        if (existing.Any())
        {
            _db.CampaignAssets.RemoveRange(existing);
            await _db.SaveChangesAsync();
            _logger.LogDebug("Removed {Count} existing campaign assets", existing.Count);
        }

        for (var i = 0; i < campaign.Assets.Count; i++)
        {
            var assetInfo = campaign.Assets[i];
            var campaignAsset = new CampaignAsset
            {
                CampaignId = campaign.CampaignId,
                AssetId = assetInfo.AssetId,
                Position = i + 1,
                DurationSeconds = null,
            };
            _db.CampaignAssets.Add(campaignAsset);
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation(
            "Upserted campaign {CampaignId} with {AssetCount} assets for backend {BackendId}",
            campaign.CampaignId,
            campaign.Assets.Count,
            backendId
        );
    }

    private async Task<bool> SyncAssetAsync(
        AssetDownloadInfo asset,
        Guid backendId,
        CancellationToken cancellationToken
    )
    {
        _logger.LogDebug(
            "SyncAssetAsync: Checking asset {AssetId} for backend {BackendId}",
            asset.AssetId,
            backendId
        );

        // Check if asset already downloaded for this backend
        var downloadedAsset = await _db.DownloadedAssets.FirstOrDefaultAsync(da =>
            da.BackendInstanceId == backendId && da.AssetId == asset.AssetId
        );

        if (
            downloadedAsset?.IsDownloaded == true
            && !string.IsNullOrEmpty(downloadedAsset.LocalPath)
            && File.Exists(downloadedAsset.LocalPath)
        )
        {
            _logger.LogInformation(
                "Asset {AssetId} already downloaded for backend {BackendId} at {Path}, skipping",
                asset.AssetId,
                backendId,
                downloadedAsset.LocalPath
            );
            downloadedAsset.LastCheckedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return false;
        }

        if (
            downloadedAsset?.IsDownloaded == true
            && !string.IsNullOrEmpty(downloadedAsset.LocalPath)
            && !File.Exists(downloadedAsset.LocalPath)
        )
            _logger.LogWarning(
                "Asset {AssetId} marked as downloaded but file missing at {Path}, re-downloading",
                asset.AssetId,
                downloadedAsset.LocalPath
            );

        // Website assets don't need downloading
        if (asset.Type == "Website")
        {
            _logger.LogInformation(
                "Asset {AssetId} is a Website, no download needed",
                asset.AssetId
            );
            await UpsertDownloadedAssetAsync(backendId, asset.AssetId, null, null, true);
            return false;
        }

        // Download the asset
        try
        {
            _logger.LogInformation(
                "Downloading asset {AssetId} from {Source} for backend {BackendId}",
                asset.AssetId,
                asset.Source,
                backendId
            );
            OnSyncProgressChanged?.Invoke(asset.AssetId, "Downloading", 0);

            var (localPath, extension) = await DownloadAssetAsync(
                asset,
                backendId,
                cancellationToken
            );

            _logger.LogInformation(
                "Successfully downloaded asset {AssetId} to {Path} ({Extension}) for backend {BackendId}",
                asset.AssetId,
                localPath,
                extension,
                backendId
            );

            await UpsertDownloadedAssetAsync(backendId, asset.AssetId, localPath, extension, true);
            await UpdateServerSyncStatusAsync(asset.AssetId, "Downloaded", 100);

            OnSyncProgressChanged?.Invoke(asset.AssetId, "Downloaded", 100);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to download asset {AssetId} for backend {BackendId}",
                asset.AssetId,
                backendId
            );
            await UpsertDownloadedAssetAsync(backendId, asset.AssetId, null, null, false);
            await UpdateServerSyncStatusAsync(asset.AssetId, "Failed", 0, ex.Message);
            throw;
        }
    }

    private async Task<(string localPath, string extension)> DownloadAssetAsync(
        AssetDownloadInfo asset,
        Guid backendId,
        CancellationToken cancellationToken
    )
    {
        _logger.LogDebug(
            "DownloadAssetAsync: Starting download for {AssetId} (backend {BackendId})",
            asset.AssetId,
            backendId
        );

        // Get backend-specific cache directory
        var assetCacheDirectory = await GetAssetCacheDirectoryAsync();

        // Determine file extension from source URL or content type
        var extension = Path.GetExtension(asset.Source);
        if (string.IsNullOrEmpty(extension))
        {
            extension = asset.Type == "Image" ? ".jpg" : ".mp4";
            _logger.LogDebug("No extension in source, using default: {Extension}", extension);
        }
        else
        {
            _logger.LogDebug("Extension from source: {Extension}", extension);
        }

        var fileName = $"{asset.AssetId}{extension}";
        var localPath = Path.Combine(assetCacheDirectory, fileName);

        _logger.LogDebug("Target local path: {LocalPath}", localPath);

        var downloadUrl = asset.Source.StartsWith("http")
            ? asset.Source
            : $"{_baseUrl}{asset.Source}";

        _logger.LogInformation("Download URL: {Url}", downloadUrl);

        var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        var token = _accessTokenProvider.GetAccessToken();
        if (!string.IsNullOrEmpty(token))
        {
            _logger.LogDebug("Adding Bearer token to request");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _logger.LogWarning("No access token available for download request");
        }

        _logger.LogDebug("Sending HTTP request...");
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        _logger.LogInformation("HTTP Response: {StatusCode}", response.StatusCode);
        response.EnsureSuccessStatusCode();

        // Try to get extension from Content-Type if we don't have one
        if (extension == ".jpg" || extension == ".mp4")
        {
            var contentType = response.Content.Headers.ContentType?.MediaType;
            _logger.LogDebug("Content-Type from response: {ContentType}", contentType);

            var detectedExtension = GetExtensionFromContentType(contentType);
            if (detectedExtension != null)
            {
                extension = detectedExtension;
                fileName = $"{asset.AssetId}{extension}";
                localPath = Path.Combine(assetCacheDirectory, fileName);
                _logger.LogInformation(
                    "Extension updated from Content-Type: {Extension}, new path: {Path}",
                    extension,
                    localPath
                );
            }
        }

        var totalBytes = response.Content.Headers.ContentLength ?? asset.FileSizeBytes ?? 0;
        _logger.LogInformation("Content length: {Bytes} bytes", totalBytes);

        var downloadedBytes = 0L;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(
            localPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            8192,
            true
        );

        var buffer = new byte[8192];
        int bytesRead;
        var lastLoggedProgress = -1;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            downloadedBytes += bytesRead;

            if (totalBytes > 0)
            {
                var progress = (int)(downloadedBytes * 100 / totalBytes);

                if (progress >= lastLoggedProgress + 25 || progress == 100)
                {
                    _logger.LogInformation(
                        "Download progress: {Progress}% ({Downloaded}/{Total} bytes)",
                        progress,
                        downloadedBytes,
                        totalBytes
                    );
                    lastLoggedProgress = progress;
                }

                OnSyncProgressChanged?.Invoke(asset.AssetId, "Downloading", progress);

                if (progress % 25 == 0)
                    await UpdateServerSyncStatusAsync(asset.AssetId, "Downloading", progress);
            }
        }

        _logger.LogInformation(
            "Download complete: {Bytes} bytes written to {Path}",
            downloadedBytes,
            localPath
        );
        return (localPath, extension);
    }

    private string? GetExtensionFromContentType(string? contentType)
    {
        return contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "video/mp4" => ".mp4",
            "video/webm" => ".webm",
            "video/x-msvideo" => ".avi",
            "video/quicktime" => ".mov",
            _ => null,
        };
    }

    private async Task UpsertDownloadedAssetAsync(
        Guid backendId,
        Guid assetId,
        string? localPath,
        string? extension,
        bool isDownloaded
    )
    {
        var downloadedAsset = await _db.DownloadedAssets.FirstOrDefaultAsync(da =>
            da.BackendInstanceId == backendId && da.AssetId == assetId
        );

        if (downloadedAsset == null)
        {
            downloadedAsset = new DownloadedAsset
            {
                BackendInstanceId = backendId,
                AssetId = assetId,
                LocalPath = localPath,
                FileExtension = extension,
                IsDownloaded = isDownloaded,
                DownloadedAt = isDownloaded ? DateTime.UtcNow : null,
                LastCheckedAt = DateTime.UtcNow,
            };
            _db.DownloadedAssets.Add(downloadedAsset);
            _logger.LogDebug(
                "Created DownloadedAsset record for {AssetId} (backend {BackendId})",
                assetId,
                backendId
            );
        }
        else
        {
            if (localPath != null)
                downloadedAsset.LocalPath = localPath;
            if (extension != null)
                downloadedAsset.FileExtension = extension;
            downloadedAsset.IsDownloaded = isDownloaded;
            if (isDownloaded)
                downloadedAsset.DownloadedAt = DateTime.UtcNow;
            downloadedAsset.LastCheckedAt = DateTime.UtcNow;
            _logger.LogDebug(
                "Updated DownloadedAsset record for {AssetId} (backend {BackendId})",
                assetId,
                backendId
            );
        }

        await _db.SaveChangesAsync();
    }

    private async Task UpdateServerSyncStatusAsync(
        Guid assetId,
        string syncState,
        int progress,
        string? errorMessage = null
    )
    {
        try
        {
            var request = new UpdateAssetSyncRequest
            {
                AssetId = assetId,
                SyncState = syncState,
                Progress = progress,
                ErrorMessage = errorMessage,
            };

            var token = _accessTokenProvider.GetAccessToken();
            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_baseUrl}/api/AssetSync/status"
            )
            {
                Content = JsonContent.Create(request),
            };

            if (!string.IsNullOrEmpty(token))
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to update server sync status for asset {AssetId}",
                assetId
            );
        }
    }
}
