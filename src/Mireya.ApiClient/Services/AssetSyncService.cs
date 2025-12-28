using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mireya.ApiClient.Models;
using Mireya.ApiClient.Options;

namespace Mireya.ApiClient.Services;

public interface IAssetSyncService
{
    event Action<Guid, string, int>? OnSyncProgressChanged;
    event Action<Guid, string>? OnCampaignSyncCompleted;
    event Action<Guid, string>? OnAssetSyncFailed;
    
    Task StartSyncAsync(List<CampaignSyncInfo> campaigns, CancellationToken cancellationToken = default);
    Task<List<AssetSyncStatusDto>> GetSyncStatusAsync();
    string GetAssetLocalPath(Guid assetId);
}

public class AssetSyncService : IAssetSyncService
{
    private readonly HttpClient _httpClient;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly ILogger<AssetSyncService> _logger;
    private readonly string _assetCacheDirectory;
    private readonly string _baseUrl;

    public event Action<Guid, string, int>? OnSyncProgressChanged;
    public event Action<Guid, string>? OnCampaignSyncCompleted;
    public event Action<Guid, string>? OnAssetSyncFailed;

    public AssetSyncService(
        IHttpClientFactory httpClientFactory,
        IAccessTokenProvider accessTokenProvider,
        IOptions<MireyaApiClientOptions> options,
        ILogger<AssetSyncService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _accessTokenProvider = accessTokenProvider;
        _logger = logger;
        _baseUrl = options.Value.BaseUrl.TrimEnd('/');

        // Set up local cache directory for assets
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _assetCacheDirectory = Path.Combine(appDataPath, "Mireya", "AssetCache");
        Directory.CreateDirectory(_assetCacheDirectory);
        
        _logger.LogInformation("Asset cache directory: {Directory}", _assetCacheDirectory);
    }

    public async Task StartSyncAsync(List<CampaignSyncInfo> campaigns, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting sync for {CampaignCount} campaigns", campaigns.Count);

        foreach (var campaign in campaigns)
        {
            try
            {
                await SyncCampaignAsync(campaign, cancellationToken);
                OnCampaignSyncCompleted?.Invoke(campaign.CampaignId, campaign.CampaignName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync campaign {CampaignId} ({CampaignName})", 
                    campaign.CampaignId, campaign.CampaignName);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Sync cancelled");
                break;
            }
        }

        _logger.LogInformation("Asset sync completed");
    }

    private async Task SyncCampaignAsync(CampaignSyncInfo campaign, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Syncing campaign {CampaignId} ({CampaignName}) with {AssetCount} assets",
            campaign.CampaignId, campaign.CampaignName, campaign.Assets.Count);

        foreach (var asset in campaign.Assets)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await SyncAssetAsync(campaign.CampaignId, asset, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync asset {AssetId} ({AssetName})", 
                    asset.AssetId, asset.Name);
                OnAssetSyncFailed?.Invoke(asset.AssetId, ex.Message);
            }
        }
    }

    private async Task SyncAssetAsync(Guid campaignId, AssetDownloadInfo asset, CancellationToken cancellationToken)
    {
        var localPath = GetAssetLocalPath(asset.AssetId);

        // Check if asset already exists locally
        if (File.Exists(localPath))
        {
            _logger.LogDebug("Asset {AssetId} already exists locally, skipping download", asset.AssetId);
            await UpdateSyncStatusAsync(asset.AssetId, "Downloaded", 100);
            return;
        }

        // Only download image and video assets, skip website URLs
        if (asset.Type == "Website")
        {
            _logger.LogDebug("Skipping download for website asset {AssetId}", asset.AssetId);
            await UpdateSyncStatusAsync(asset.AssetId, "Downloaded", 100);
            return;
        }

        try
        {
            await UpdateSyncStatusAsync(asset.AssetId, "Downloading", 0);
            OnSyncProgressChanged?.Invoke(asset.AssetId, "Downloading", 0);

            // Construct full URL
            var downloadUrl = asset.Source.StartsWith("http") 
                ? asset.Source 
                : $"{_baseUrl}{asset.Source}";

            _logger.LogInformation("Downloading asset {AssetId} from {Url}", asset.AssetId, downloadUrl);

            // Add authorization token
            var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            var token = _accessTokenProvider.GetAccessToken();
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? asset.FileSizeBytes ?? 0;
            var downloadedBytes = 0L;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    var progress = (int)((downloadedBytes * 100) / totalBytes);
                    await UpdateSyncStatusAsync(asset.AssetId, "Downloading", progress);
                    OnSyncProgressChanged?.Invoke(asset.AssetId, "Downloading", progress);
                }
            }

            await UpdateSyncStatusAsync(asset.AssetId, "Downloaded", 100);
            OnSyncProgressChanged?.Invoke(asset.AssetId, "Downloaded", 100);

            _logger.LogInformation("Successfully downloaded asset {AssetId} to {Path}", asset.AssetId, localPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download asset {AssetId}", asset.AssetId);
            await UpdateSyncStatusAsync(asset.AssetId, "Failed", 0, ex.Message);
            throw;
        }
    }

    private async Task UpdateSyncStatusAsync(Guid assetId, string syncState, int progress, string? errorMessage = null)
    {
        try
        {
            var request = new UpdateAssetSyncRequest
            {
                AssetId = assetId,
                SyncState = syncState,
                Progress = progress,
                ErrorMessage = errorMessage
            };

            var token = _accessTokenProvider.GetAccessToken();
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/AssetSync/status")
            {
                Content = JsonContent.Create(request)
            };
            
            if (!string.IsNullOrEmpty(token))
            {
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();

            _logger.LogDebug("Updated sync status: Asset {AssetId}, State {State}, Progress {Progress}%",
                assetId, syncState, progress);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update sync status for asset {AssetId}", assetId);
        }
    }

    public async Task<List<AssetSyncStatusDto>> GetSyncStatusAsync()
    {
        try
        {
            var url = $"{_baseUrl}/api/AssetSync/status";

            var token = _accessTokenProvider.GetAccessToken();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<List<AssetSyncStatusDto>>() ?? new List<AssetSyncStatusDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sync status");
            return new List<AssetSyncStatusDto>();
        }
    }

    public string GetAssetLocalPath(Guid assetId)
    {
        return Path.Combine(_assetCacheDirectory, assetId.ToString());
    }
}
