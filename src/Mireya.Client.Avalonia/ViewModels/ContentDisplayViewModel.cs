using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Models;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.Services;

namespace Mireya.Client.Avalonia.ViewModels;

public partial class ContentDisplayViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ILocalAssetSyncService _assetSyncService;
    private readonly IScreenHubService _hubService;
    private readonly ILogger<ContentDisplayViewModel> _logger;
    private readonly List<PlaylistItem> _playlist = [];
    private int _currentIndex;
    private DispatcherTimer? _advanceTimer;
    private ScreenConfiguration? _pendingConfiguration;

    [ObservableProperty]
    private string _displayName = "(not received yet)";

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private string _currentAssetName = "";

    [ObservableProperty]
    private string _currentCampaignName = "";

    [ObservableProperty]
    private int _currentAssetPosition;

    [ObservableProperty]
    private int _totalAssets;

    [ObservableProperty]
    private bool _isOverlayVisible = true;

    [ObservableProperty]
    private string _statusText = "Waiting for content...";

    [ObservableProperty]
    private ContentType _currentContentType = ContentType.None;

    [ObservableProperty]
    private Bitmap? _currentImage;

    [ObservableProperty]
    private string? _currentVideoPath;

    [ObservableProperty]
    private string? _currentWebsiteUrl;

    public ContentDisplayViewModel(
        IAuthenticationService authenticationService,
        IScreenHubService hubService,
        ILocalAssetSyncService assetSyncService,
        ILogger<ContentDisplayViewModel> logger
    )
    {
        _authenticationService = authenticationService;
        _hubService = hubService;
        _assetSyncService = assetSyncService;
        _logger = logger;

        _hubService.OnConfigurationUpdateReceived += OnConfigurationUpdateReceived;
        _hubService.OnStartAssetSync += OnStartAssetSync;

        _logger.LogInformation("ContentDisplayViewModel initialized");

        // Start authentication and connection in background
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            StatusText = "Checking authentication...";
            ConnectionStatus = "Initializing...";

            // Check authentication state and authenticate if needed
            var state = await _authenticationService.GetAuthenticationStateAsync();
            _logger.LogInformation("Authentication state: {State}", state);
            StatusText = $"Auth state: {state}";

            if (state == AuthenticationState.NotRegistered)
            {
                StatusText = "Registering device...";
                var registerResult = await _authenticationService.RegisterAsync();
                if (!registerResult.Success)
                {
                    StatusText = $"Registration failed: {registerResult.ErrorMessage}";
                    return;
                }
                state = await _authenticationService.GetAuthenticationStateAsync();
            }

            if (state == AuthenticationState.NotAuthenticated)
            {
                StatusText = "Authenticating...";
                var loginResult = await _authenticationService.LoginAsync();
                if (!loginResult.Success)
                {
                    StatusText = $"Authentication failed: {loginResult.ErrorMessage}";
                    return;
                }
                // LoginAsync already connects to SignalR
            }
            else if (state == AuthenticationState.Authenticated)
            {
                // Already authenticated, but need to connect to SignalR
                StatusText = "Connecting to SignalR...";
                ConnectionStatus = "Connecting...";
                if (!_hubService.IsConnected)
                {
                    try
                    {
                        await _hubService.ConnectAsync();
                        _logger.LogInformation("SignalR connected successfully");
                    }
                    catch (Exception connectEx)
                    {
                        _logger.LogError(connectEx, "Failed to connect to SignalR");
                        StatusText = $"SignalR error: {connectEx.Message}";
                        ConnectionStatus = "Failed ✗";
                        return;
                    }
                }
            }

            ConnectionStatus = _hubService.IsConnected ? "Connected ✓" : "Disconnected ✗";
            StatusText = _hubService.IsConnected
                ? "Waiting for content..."
                : "Not connected to server";
            _logger.LogInformation(
                "Authentication completed, SignalR connected: {IsConnected}",
                _hubService.IsConnected
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize content display");
            StatusText = $"Connection error: {ex.Message}";
            ConnectionStatus = "Error ✗";
        }
    }

    private void OnConfigurationUpdateReceived(ScreenConfiguration config)
    {
        _logger.LogInformation(
            "Configuration received: {ScreenName} with {CampaignCount} campaigns",
            config.ScreenName,
            config.Campaigns.Count
        );

        // Store the configuration but DON'T build playlist yet - wait for assets to sync
        _pendingConfiguration = config;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            DisplayName = config.ScreenName;
            StatusText = "Syncing assets...";
        });
    }

    private async void OnStartAssetSync(List<Mireya.ApiClient.Models.CampaignSyncInfo> campaigns)
    {
        _logger.LogInformation("Starting asset sync for {Count} campaigns", campaigns.Count);

        Dispatcher.UIThread.Post(() =>
        {
            StatusText = $"Syncing {campaigns.Count} campaign(s)...";
        });

        try
        {
            // Download all assets first
            await _assetSyncService.SyncCampaignsAsync(campaigns);
            _logger.LogInformation("Asset sync completed");

            // Now build playlist with downloaded assets
            if (_pendingConfiguration != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _logger.LogInformation("Building playlist after asset sync");
                    BuildPlaylist(_pendingConfiguration);
                    StartPlayback();
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Asset sync failed");
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = $"Sync error: {ex.Message}";
            });
        }
    }

    private void BuildPlaylist(ScreenConfiguration config)
    {
        _logger.LogInformation("Building playlist from configuration");

        _playlist.Clear();
        _currentIndex = 0;

        // Build playlist from all campaigns
        foreach (var campaign in config.Campaigns)
        {
            var sortedAssets = campaign.Assets.OrderBy(a => a.Position).ToList();

            foreach (var asset in sortedAssets)
            {
                var localPath = _assetSyncService.GetAssetLocalPath(asset.AssetId);

                // Skip if asset is not downloaded yet
                if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
                {
                    _logger.LogWarning(
                        "Asset {AssetId} ({AssetName}) not found locally, skipping",
                        asset.AssetId,
                        asset.AssetName
                    );
                    continue;
                }

                _playlist.Add(
                    new PlaylistItem
                    {
                        CampaignId = campaign.Id,
                        CampaignName = campaign.Name,
                        AssetId = asset.AssetId,
                        AssetName = asset.AssetName,
                        AssetType = asset.AssetType,
                        LocalPath = localPath,
                        Source = asset.Source,
                        DurationSeconds = asset.ResolvedDuration,
                        Position = asset.Position,
                    }
                );
            }
        }

        TotalAssets = _playlist.Count;
        _logger.LogInformation("Playlist built with {Count} items", _playlist.Count);

        if (_playlist.Count == 0)
        {
            StatusText = "No content available";
            CurrentContentType = ContentType.None;
        }
    }

    private void StartPlayback()
    {
        if (_playlist.Count == 0)
        {
            _logger.LogWarning("Cannot start playback: playlist is empty");
            return;
        }

        _logger.LogInformation("Starting playback");
        _currentIndex = 0;
        ShowCurrentItem();
    }

    private void ShowCurrentItem()
    {
        if (_playlist.Count == 0)
            return;

        var item = _playlist[_currentIndex];
        _logger.LogInformation(
            "Showing item {Index}/{Total}: {AssetName} ({AssetType})",
            _currentIndex + 1,
            _playlist.Count,
            item.AssetName,
            item.AssetType
        );

        CurrentAssetName = item.AssetName;
        CurrentCampaignName = item.CampaignName;
        CurrentAssetPosition = _currentIndex + 1;
        StatusText = $"Playing: {item.CampaignName}";

        // Stop any existing timer
        _advanceTimer?.Stop();

        try
        {
            switch (item.AssetType)
            {
                case AssetType.Image:
                    ShowImage(item);
                    break;
                case AssetType.Video:
                    ShowVideo(item);
                    break;
                case AssetType.Website:
                    ShowWebsite(item);
                    break;
                default:
                    _logger.LogWarning("Unknown asset type: {Type}", item.AssetType);
                    AdvanceToNext();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing item {AssetName}", item.AssetName);
            AdvanceToNext();
        }
    }

    private void ShowImage(PlaylistItem item)
    {
        _logger.LogDebug("Loading image: {Path}", item.LocalPath);

        CurrentContentType = ContentType.Image;
        CurrentVideoPath = null;
        CurrentWebsiteUrl = null;

        try
        {
            if (File.Exists(item.LocalPath))
            {
                CurrentImage = new Bitmap(item.LocalPath);
            }
            else
            {
                _logger.LogWarning("Image file not found: {Path}", item.LocalPath);
                CurrentImage = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load image: {Path}", item.LocalPath);
            CurrentImage = null;
        }

        // Set timer to advance after duration
        StartAdvanceTimer(item.DurationSeconds);
    }

    private void ShowVideo(PlaylistItem item)
    {
        _logger.LogDebug("Loading video: {Path}", item.LocalPath);

        CurrentContentType = ContentType.Video;
        CurrentVideoPath = item.LocalPath;
        CurrentImage = null;
        CurrentWebsiteUrl = null;

        // Video player should handle completion event
        // For now, use duration as fallback
        StartAdvanceTimer(item.DurationSeconds);
    }

    private void ShowWebsite(PlaylistItem item)
    {
        _logger.LogDebug("Loading website: {Url}", item.Source);

        CurrentContentType = ContentType.Website;
        CurrentWebsiteUrl = item.Source;
        CurrentImage = null;
        CurrentVideoPath = null;

        // Set timer to advance after duration
        StartAdvanceTimer(item.DurationSeconds);
    }

    private void StartAdvanceTimer(int durationSeconds)
    {
        _advanceTimer?.Stop();

        // Ensure minimum duration of 1 second to prevent rapid cycling
        var duration = Math.Max(durationSeconds, 1);

        _advanceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(duration) };
        _advanceTimer.Tick += OnAdvanceTimerTick;
        _advanceTimer.Start();

        _logger.LogDebug("Timer started for {Duration} seconds", duration);
    }

    private void OnAdvanceTimerTick(object? sender, EventArgs e)
    {
        AdvanceToNext();
    }

    private void AdvanceToNext()
    {
        _advanceTimer?.Stop();

        _currentIndex++;
        if (_currentIndex >= _playlist.Count)
        {
            _currentIndex = 0; // Loop back to start
            _logger.LogInformation("Reached end of playlist, looping to start");
        }

        ShowCurrentItem();
    }

    [RelayCommand]
    private void ToggleOverlay()
    {
        IsOverlayVisible = !IsOverlayVisible;
        _logger.LogDebug("Overlay visibility: {Visible}", IsOverlayVisible);
    }

    [RelayCommand]
    private void NextAsset()
    {
        _logger.LogInformation("Manual advance to next asset");
        AdvanceToNext();
    }

    [RelayCommand]
    private void PreviousAsset()
    {
        _logger.LogInformation("Manual advance to previous asset");
        _advanceTimer?.Stop();

        _currentIndex--;
        if (_currentIndex < 0)
        {
            _currentIndex = _playlist.Count - 1;
        }

        ShowCurrentItem();
    }

    public void Cleanup()
    {
        _logger.LogInformation("Cleaning up ContentDisplayViewModel");
        _advanceTimer?.Stop();
        _hubService.OnConfigurationUpdateReceived -= OnConfigurationUpdateReceived;
        _hubService.OnStartAssetSync -= OnStartAssetSync;
    }
}

public class PlaylistItem
{
    public Guid CampaignId { get; set; }
    public string CampaignName { get; set; } = "";
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = "";
    public AssetType AssetType { get; set; }
    public string LocalPath { get; set; } = "";
    public string Source { get; set; } = "";
    public int DurationSeconds { get; set; }
    public int Position { get; set; }
}

public enum ContentType
{
    None,
    Image,
    Video,
    Website,
}
