using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Models;
using Mireya.Database.Models;

namespace Mireya.ApiClient.Services;

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
    private readonly LocalDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LocalAssetSyncService> _logger;

    public LocalAssetSyncService(
        LocalDbContext db,
        IBackendManager backendManager,
        IHttpClientFactory httpClientFactory,
        IAccessTokenProvider accessTokenProvider,
        ILogger<LocalAssetSyncService> logger
    )
    {
        _db = db;
        _backendManager = backendManager;
        _httpClientFactory = httpClientFactory;
        _accessTokenProvider = accessTokenProvider;
        _logger = logger;

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
        var backend = await _backendManager.GetCurrentBackendAsync();
        if (backend == null)
        {
            _logger.LogError("Cannot sync: No current backend configured");
            return;
        }

        _logger.LogInformation(
            "=== START SYNC === Syncing {CampaignCount} campaigns for backend {BackendId} - {BaseUrl}: {CampaignNames}",
            campaigns.Count,
            backend.Id,
            backend.BaseUrl,
            string.Join(", ", campaigns.Select(c => $"{c.CampaignName}({c.Assets.Count} assets)"))
        );

        foreach (var campaign in campaigns)
        {
            _logger.LogDebug("Upserting campaign {CampaignId} to local DB", campaign.CampaignId);
            await UpsertCampaignAsync(campaign, backend.Id);
        }

        var uniqueAssets = campaigns
            .SelectMany(c => c.Assets)
            .GroupBy(a => a.AssetId)
            .Select(g => g.First())
            .ToList();
        _logger.LogDebug("Total unique assets to check: {Count}", uniqueAssets.Count);

        await DownloadUniqueAssetsAsync(
            uniqueAssets,
            backend.Id,
            new Uri(backend.BaseUrl.TrimEnd('/')),
            cancellationToken
        );

        foreach (var campaign in campaigns)
            OnCampaignSyncCompleted?.Invoke(campaign.CampaignId, campaign.CampaignName);
    }

    public async Task<List<Guid>> GetMissingAssetIdsAsync(List<Guid> requiredAssetIds)
    {
        var backend = await _backendManager.GetCurrentBackendAsync();
        if (backend == null)
        {
            _logger.LogWarning("Cannot check missing assets: No current backend");
            return requiredAssetIds;
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
        // Use synchronous EF Core query to avoid sync-over-async deadlock
        var backend = _db.BackendInstances.FirstOrDefault(b => b.IsCurrentBackend);
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

    private async Task DownloadUniqueAssetsAsync(
        List<AssetDownloadInfo> uniqueAssets,
        Guid backendId,
        Uri baseUrl,
        CancellationToken cancellationToken
    )
    {
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

                var wasDownloaded = await SyncAssetAsync(
                    new AssetDownloadContext(asset, backendId, baseUrl),
                    cancellationToken
                );
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
    }

    private async Task UpsertCampaignAsync(CampaignSyncInfo campaign, Guid backendId)
    {
        _logger.LogDebug(
            "Upserting campaign {CampaignId} - {CampaignName} for backend {BackendId}",
            campaign.CampaignId,
            campaign.CampaignName,
            backendId
        );

        await UpsertCampaignEntityAsync(campaign);
        await UpsertBackendCampaignMappingAsync(campaign.CampaignId, backendId);
        await UpsertCampaignAssetsAsync(campaign, backendId);

        _logger.LogInformation(
            "Upserted campaign {CampaignId} with {AssetCount} assets for backend {BackendId}",
            campaign.CampaignId,
            campaign.Assets.Count,
            backendId
        );
    }

    private async Task UpsertCampaignEntityAsync(CampaignSyncInfo campaign)
    {
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
    }

    private async Task UpsertBackendCampaignMappingAsync(Guid campaignId, Guid backendId)
    {
        var backendCampaign = await _db.BackendCampaigns.FirstOrDefaultAsync(bc =>
            bc.BackendInstanceId == backendId && bc.CampaignId == campaignId
        );

        if (backendCampaign == null)
        {
            _db.BackendCampaigns.Add(
                new BackendCampaign
                {
                    BackendInstanceId = backendId,
                    CampaignId = campaignId,
                    SyncedAt = DateTime.UtcNow,
                }
            );
            _logger.LogDebug("Created BackendCampaign mapping for backend {BackendId}", backendId);
        }
        else
        {
            backendCampaign.SyncedAt = DateTime.UtcNow;
            _logger.LogDebug("Updated BackendCampaign mapping for backend {BackendId}", backendId);
        }

        await _db.SaveChangesAsync();
    }

    private async Task UpsertCampaignAssetsAsync(CampaignSyncInfo campaign, Guid backendId)
    {
        foreach (var assetInfo in campaign.Assets)
            await UpsertAssetEntityAsync(assetInfo, backendId);

        await _db.SaveChangesAsync();
        await ReplaceCampaignAssetLinksAsync(campaign);
    }

    private async Task UpsertAssetEntityAsync(AssetDownloadInfo assetInfo, Guid backendId)
    {
        var asset = await _db.Assets.FindAsync(assetInfo.AssetId);
        if (asset == null)
        {
            _db.Assets.Add(
                new Asset
                {
                    Id = assetInfo.AssetId,
                    Name = assetInfo.Name,
                    Type = Enum.Parse<AssetType>(assetInfo.Type, true),
                    Source = assetInfo.Source,
                    FileSizeBytes = assetInfo.FileSizeBytes,
                    DurationSeconds = assetInfo.DurationSeconds,
                    IsMuted = assetInfo.IsMuted,
                    CreatedAt = DateTime.UtcNow,
                }
            );
            _logger.LogDebug(
                "Created new asset {AssetId} - {AssetName}",
                assetInfo.AssetId,
                assetInfo.Name
            );
        }
        else
        {
            asset.Name = assetInfo.Name;
            asset.Type = Enum.Parse<AssetType>(assetInfo.Type, true);
            asset.Source = assetInfo.Source;
            asset.FileSizeBytes = assetInfo.FileSizeBytes;
            asset.DurationSeconds = assetInfo.DurationSeconds;
            asset.IsMuted = assetInfo.IsMuted;
            asset.UpdatedAt = DateTime.UtcNow;
        }

        await UpsertBackendAssetMappingAsync(assetInfo.AssetId, backendId);
    }

    private async Task UpsertBackendAssetMappingAsync(Guid assetId, Guid backendId)
    {
        var backendAsset = await _db.BackendAssets.FirstOrDefaultAsync(ba =>
            ba.BackendInstanceId == backendId && ba.AssetId == assetId
        );

        if (backendAsset == null)
            _db.BackendAssets.Add(
                new BackendAsset
                {
                    BackendInstanceId = backendId,
                    AssetId = assetId,
                    SyncedAt = DateTime.UtcNow,
                }
            );
        else
            backendAsset.SyncedAt = DateTime.UtcNow;
    }

    private async Task ReplaceCampaignAssetLinksAsync(CampaignSyncInfo campaign)
    {
        var existing = await _db
            .CampaignAssets.Where(ca => ca.CampaignId == campaign.CampaignId)
            .ToListAsync();
        if (existing.Count != 0)
        {
            _db.CampaignAssets.RemoveRange(existing);
            await _db.SaveChangesAsync();
            _logger.LogDebug("Removed {Count} existing campaign assets", existing.Count);
        }

        for (var i = 0; i < campaign.Assets.Count; i++)
        {
            _db.CampaignAssets.Add(
                new CampaignAsset
                {
                    CampaignId = campaign.CampaignId,
                    AssetId = campaign.Assets[i].AssetId,
                    Position = i + 1,
                    DurationSeconds = null,
                }
            );
        }

        await _db.SaveChangesAsync();
    }

    private async Task<bool> SyncAssetAsync(
        AssetDownloadContext ctx,
        CancellationToken cancellationToken
    )
    {
        _logger.LogDebug(
            "SyncAssetAsync: Checking asset {AssetId} for backend {BackendId}",
            ctx.Asset.AssetId,
            ctx.BackendId
        );

        var downloadedAsset = await _db.DownloadedAssets.FirstOrDefaultAsync(da =>
            da.BackendInstanceId == ctx.BackendId && da.AssetId == ctx.Asset.AssetId
        );

        if (
            downloadedAsset is { IsDownloaded: true, LocalPath: { Length: > 0 } localPath }
            && File.Exists(localPath)
        )
        {
            _logger.LogInformation(
                "Asset {AssetId} already downloaded at {Path}, skipping",
                ctx.Asset.AssetId,
                localPath
            );
            downloadedAsset.LastCheckedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return false;
        }

        if (IsStaleDownload(downloadedAsset))
            _logger.LogWarning(
                "Asset {AssetId} marked downloaded but file missing, re-downloading",
                ctx.Asset.AssetId
            );

        if (ctx.Asset.Type == "Website")
        {
            _logger.LogInformation(
                "Asset {AssetId} is a Website, no download needed",
                ctx.Asset.AssetId
            );
            await UpsertDownloadedAssetAsync(
                ctx.BackendId,
                ctx.Asset.AssetId,
                new AssetDownloadState(null, null, true)
            );
            return false;
        }

        return await DownloadAndTrackAssetAsync(ctx, cancellationToken);
    }

    private static bool IsStaleDownload(DownloadedAsset? asset) =>
        asset?.IsDownloaded == true
        && !string.IsNullOrEmpty(asset.LocalPath)
        && !File.Exists(asset.LocalPath);

    private async Task<bool> DownloadAndTrackAssetAsync(
        AssetDownloadContext ctx,
        CancellationToken cancellationToken
    )
    {
        try
        {
            _logger.LogInformation(
                "Downloading asset {AssetId} from {Source} for backend {BackendId}",
                ctx.Asset.AssetId,
                ctx.Asset.Source,
                ctx.BackendId
            );
            OnSyncProgressChanged?.Invoke(ctx.Asset.AssetId, "Downloading", 0);

            var (localPath, extension) = await DownloadAssetAsync(ctx, cancellationToken);

            _logger.LogInformation(
                "Successfully downloaded asset {AssetId} to {Path} ({Extension})",
                ctx.Asset.AssetId,
                localPath,
                extension
            );

            await UpsertDownloadedAssetAsync(
                ctx.BackendId,
                ctx.Asset.AssetId,
                new AssetDownloadState(localPath, extension, true)
            );
            await UpdateServerSyncStatusAsync(
                ctx.Asset.AssetId,
                new SyncStatusUpdate("Downloaded", 100)
            );

            OnSyncProgressChanged?.Invoke(ctx.Asset.AssetId, "Downloaded", 100);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to download asset {AssetId} for backend {BackendId}",
                ctx.Asset.AssetId,
                ctx.BackendId
            );
            await UpsertDownloadedAssetAsync(
                ctx.BackendId,
                ctx.Asset.AssetId,
                new AssetDownloadState(null, null, false)
            );
            await UpdateServerSyncStatusAsync(
                ctx.Asset.AssetId,
                new SyncStatusUpdate("Failed", 0, ex.Message)
            );
            return false;
        }
    }

    private async Task<(string localPath, string extension)> DownloadAssetAsync(
        AssetDownloadContext ctx,
        CancellationToken cancellationToken
    )
    {
        _logger.LogDebug("DownloadAssetAsync: Starting download for {AssetId}", ctx.Asset.AssetId);

        var assetCacheDirectory = await GetAssetCacheDirectoryAsync();
        var extension = DetermineInitialExtension(ctx.Asset);
        var downloadUrl = ctx.Asset.Source.StartsWith("http")
            ? new Uri(ctx.Asset.Source)
            : new Uri(ctx.BaseUrl, ctx.Asset.Source);

        _logger.LogInformation("Download URL: {Url}", downloadUrl);

        using var response = await SendAuthorizedGetAsync(downloadUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        extension = RefineExtensionFromResponse(response, extension);

        var localPath = Path.Combine(assetCacheDirectory, $"{ctx.Asset.AssetId}{extension}");
        _logger.LogDebug("Target local path: {LocalPath}", localPath);

        var downloadedBytes = await WriteResponseToFileAsync(
            response,
            localPath,
            ctx.Asset,
            cancellationToken
        );

        _logger.LogInformation(
            "Download complete: {Bytes} bytes written to {Path}",
            downloadedBytes,
            localPath
        );
        return (localPath, extension);
    }

    private static string DetermineInitialExtension(AssetDownloadInfo asset)
    {
        var extension = Path.GetExtension(asset.Source);
        if (string.IsNullOrEmpty(extension))
        {
            extension = asset.Type == "Image" ? ".jpg" : ".mp4";
            return extension;
        }
        return extension;
    }

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(
        Uri url,
        CancellationToken cancellationToken
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var token = _accessTokenProvider.GetAccessToken();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await _httpClientFactory
            .CreateClient()
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private string RefineExtensionFromResponse(
        HttpResponseMessage response,
        string currentExtension
    )
    {
        if (currentExtension != ".jpg" && currentExtension != ".mp4")
            return currentExtension;

        var contentType = response.Content.Headers.ContentType?.MediaType;
        _logger.LogDebug("Content-Type from response: {ContentType}", contentType);

        var detected =
            contentType != null && ContentTypeExtensions.TryGetValue(contentType, out var ext)
                ? ext
                : null;
        if (detected != null)
            _logger.LogInformation("Extension updated from Content-Type: {Extension}", detected);

        return detected ?? currentExtension;
    }

    private async Task<long> WriteResponseToFileAsync(
        HttpResponseMessage response,
        string localPath,
        AssetDownloadInfo asset,
        CancellationToken cancellationToken
    )
    {
        var totalBytes = response.Content.Headers.ContentLength ?? asset.FileSizeBytes ?? 0;
        _logger.LogInformation("Content length: {Bytes} bytes", totalBytes);

        var downloadedBytes = 0L;
        var lastLoggedProgress = -1;

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
                    await UpdateServerSyncStatusAsync(
                        asset.AssetId,
                        new SyncStatusUpdate("Downloading", progress)
                    );
            }
        }

        return downloadedBytes;
    }

    private static readonly Dictionary<string, string> ContentTypeExtensions = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp",
        ["video/mp4"] = ".mp4",
        ["video/webm"] = ".webm",
        ["video/x-msvideo"] = ".avi",
        ["video/quicktime"] = ".mov",
    };

    private sealed record AssetDownloadState(
        string? LocalPath,
        string? Extension,
        bool IsDownloaded
    );

    private sealed record AssetDownloadContext(
        AssetDownloadInfo Asset,
        Guid BackendId,
        Uri BaseUrl
    );

    private sealed record SyncStatusUpdate(string State, int Progress, string? ErrorMessage = null);

    private async Task UpsertDownloadedAssetAsync(
        Guid backendId,
        Guid assetId,
        AssetDownloadState state
    )
    {
        var downloadedAsset = await _db.DownloadedAssets.FirstOrDefaultAsync(da =>
            da.BackendInstanceId == backendId && da.AssetId == assetId
        );

        if (downloadedAsset == null)
        {
            _db.DownloadedAssets.Add(
                new DownloadedAsset
                {
                    BackendInstanceId = backendId,
                    AssetId = assetId,
                    LocalPath = state.LocalPath,
                    FileExtension = state.Extension,
                    IsDownloaded = state.IsDownloaded,
                    DownloadedAt = state.IsDownloaded ? DateTime.UtcNow : null,
                    LastCheckedAt = DateTime.UtcNow,
                }
            );
            _logger.LogDebug(
                "Created DownloadedAsset record for {AssetId} (backend {BackendId})",
                assetId,
                backendId
            );
        }
        else
        {
            if (state.LocalPath != null)
                downloadedAsset.LocalPath = state.LocalPath;
            if (state.Extension != null)
                downloadedAsset.FileExtension = state.Extension;
            downloadedAsset.IsDownloaded = state.IsDownloaded;
            if (state.IsDownloaded)
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

    private async Task UpdateServerSyncStatusAsync(Guid assetId, SyncStatusUpdate status)
    {
        try
        {
            var request = new Generated.UpdateAssetSyncRequest
            {
                AssetId = assetId,
                SyncState = status.State,
                Progress = status.Progress,
                ErrorMessage = status.ErrorMessage,
            };

            var baseUrl =
                (await _backendManager.GetCurrentBackendAsync())?.BaseUrl?.TrimEnd('/')
                ?? string.Empty;
            var token = _accessTokenProvider.GetAccessToken();
            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl}/api/AssetSync/status"
            )
            {
                Content = JsonContent.Create(request),
            };

            if (!string.IsNullOrEmpty(token))
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClientFactory.CreateClient().SendAsync(httpRequest);
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
