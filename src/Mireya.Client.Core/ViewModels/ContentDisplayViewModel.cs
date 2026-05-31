using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Models;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.Services;

namespace Mireya.Client.Avalonia.ViewModels;

public sealed partial class ContentDisplayViewModel : ViewModelBase, IDisposable
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ILocalAssetSyncService _assetSyncService;
    private readonly IScreenHubService _hubService;
    private readonly ILogger<ContentDisplayViewModel> _logger;
    private readonly List<PlaylistItem> _playlist = [];
    private int _currentIndex;
    private DispatcherTimer? _advanceTimer;
    private ScreenConfiguration? _pendingConfiguration;
    private bool _disposed;

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
    private IBrush _connectionIndicatorColor = Brushes.Gray;

    [ObservableProperty]
    private ContentType _currentContentType = ContentType.None;

    [ObservableProperty]
    private Bitmap? _currentImage;

    [ObservableProperty]
    private Stretch _currentImageStretch = Stretch.Uniform;

    [ObservableProperty]
    private double _currentImageOpacity = 1;

    [ObservableProperty]
    private string? _currentVideoPath;

    [ObservableProperty]
    private Uri? _currentVideoUri;

    [ObservableProperty]
    private string? _currentWebsiteUrl;

    [ObservableProperty]
    private Uri? _currentWebsiteUri;

    // ── First-run / approval (UA3) + pairing & remote identify (UA7) ──────────

    /// <summary>True while the screen is registered but not yet approved by an admin.</summary>
    [ObservableProperty]
    private bool _isAwaitingApproval;

    /// <summary>Human-readable pairing code (the screen identifier) an admin uses to find this screen.</summary>
    [ObservableProperty]
    private string _pairingCode = "";

    /// <summary>Explanatory text shown beneath the pairing code while awaiting approval.</summary>
    [ObservableProperty]
    private string _approvalStatusText = "Waiting for an administrator to approve this screen…";

    /// <summary>True for a few seconds after an admin sends the "identify" command, flashing the screen.</summary>
    [ObservableProperty]
    private bool _isIdentifying;

    // Event to notify video component to start playback
    public event Action<string, bool>? VideoPlaybackRequested; // path, isMuted
    public event Action? VideoStopRequested;

    public ContentDisplayViewModel(
        IAuthenticationService authenticationService,
        IScreenHubService hubService,
        ILocalAssetSyncService assetSyncService,
        ILogger<ContentDisplayViewModel> logger,
        AppSettings appSettings
    )
    {
        _authenticationService = authenticationService;
        _hubService = hubService;
        _assetSyncService = assetSyncService;
        _logger = logger;

        _hubService.OnConfigurationUpdateReceived += OnConfigurationUpdateReceived;
        _hubService.OnStartAssetSync += OnStartAssetSync;
        _hubService.OnCommandReceived += OnCommandReceived;
        _hubService.OnReconnecting += OnHubReconnecting;
        _hubService.OnReconnected += OnHubReconnected;
        _hubService.OnClosed += OnHubClosed;

        _logger.LogInformation("ContentDisplayViewModel initialized");

        // Start authentication and connection in background
        _ = InitializeAsync().ContinueWith(
            t => _logger.LogError(t.Exception, "Background initialization failed"),
            TaskContinuationOptions.OnlyOnFaulted);

        // Auto-hide the status overlay once content starts playing (independent of AutoStart)
        if (appSettings.HideScreenInfo)
        {
            _ = AutoHideOverlayAsync().ContinueWith(
                t => _logger.LogError(t.Exception, "Auto-hide overlay faulted"),
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            StatusText = "Checking authentication...";
            ConnectionStatus = "Initializing...";

            var state = await _authenticationService.GetAuthenticationStateAsync();
            _logger.LogInformation("Authentication state: {State}", state);
            StatusText = $"Auth state: {state}";

            state = await EnsureRegisteredAsync(state);
            if (state == AuthenticationState.Failed)
                return;

            await EnsureAuthenticatedAndConnectedAsync(state);

            // First-run / approval gate (UA3): surface the pairing code and wait
            // for an administrator to approve this screen before expecting content.
            await PollApprovalAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize content display");
            StatusText = $"Connection error: {ex.Message}";
            ConnectionStatus = "Error ✗";
        }
    }

    private async Task<AuthenticationState> EnsureRegisteredAsync(AuthenticationState state)
    {
        if (state != AuthenticationState.NotRegistered)
            return state;

        StatusText = "Registering device...";
        var registerResult = await _authenticationService.RegisterAsync();
        if (!registerResult.Success)
        {
            StatusText = $"Registration failed: {registerResult.ErrorMessage}";
            return AuthenticationState.Failed;
        }
        return await _authenticationService.GetAuthenticationStateAsync();
    }

    private async Task EnsureAuthenticatedAndConnectedAsync(AuthenticationState state)
    {
        if (state == AuthenticationState.NotAuthenticated)
        {
            StatusText = "Authenticating...";
            var loginResult = await _authenticationService.LoginAsync();
            if (!loginResult.Success)
            {
                StatusText = $"Authentication failed: {loginResult.ErrorMessage}";
                return;
            }
        }
        else if (state == AuthenticationState.Authenticated)
        {
            await ConnectToSignalRAsync();
            return;
        }

        UpdateConnectionStatus();
    }

    private async Task ConnectToSignalRAsync()
    {
        if (_hubService.IsConnected)
        {
            UpdateConnectionStatus();
            return;
        }

        StatusText = "Connecting to SignalR...";
        ConnectionStatus = "Connecting...";
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

        UpdateConnectionStatus();
    }

    private void UpdateConnectionStatus()
    {
        ConnectionStatus = _hubService.IsConnected ? "Connected ✓" : "Disconnected ✗";
        ConnectionIndicatorColor = _hubService.IsConnected ? Brushes.LimeGreen : Brushes.OrangeRed;
        StatusText = _hubService.IsConnected ? "Waiting for content..." : "Not connected to server";
        _logger.LogInformation("Authentication completed, SignalR connected: {IsConnected}", _hubService.IsConnected);
    }

    /// <summary>
    ///     Polls the backend for this screen's approval status. While the screen is not yet
    ///     approved it shows the pairing code (screen identifier) and an explanatory message so
    ///     an operator can locate and approve it. Returns once the screen is approved (the server
    ///     then pushes a configuration via SignalR) or the view model is disposed.
    /// </summary>
    private async Task PollApprovalAsync()
    {
        var firstPass = true;
        while (!_disposed)
        {
            var info = await _authenticationService.GetScreenInfoAsync();
            if (info == null)
            {
                // Could not read status yet (e.g. token not ready); retry shortly.
                await Task.Delay(TimeSpan.FromSeconds(5));
                continue;
            }

            var approved = string.Equals(info.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase);
            var rejected = string.Equals(info.ApprovalStatus, "Rejected", StringComparison.OrdinalIgnoreCase);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PairingCode = FormatPairingCode(info.ScreenIdentifier);
                if (!string.IsNullOrWhiteSpace(info.ScreenName))
                    DisplayName = info.ScreenName;

                if (approved)
                {
                    IsAwaitingApproval = false;
                }
                else
                {
                    IsAwaitingApproval = true;
                    ApprovalStatusText = rejected
                        ? "This screen was rejected. Please contact your administrator."
                        : "Waiting for an administrator to approve this screen…";
                    StatusText = ApprovalStatusText;
                }
            });

            if (approved)
            {
                _logger.LogInformation("Screen approved; awaiting configuration push");
                return;
            }

            if (firstPass)
            {
                _logger.LogInformation(
                    "Screen not yet approved (status: {Status}); showing pairing code {Code}",
                    info.ApprovalStatus, info.ScreenIdentifier);
                firstPass = false;
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Groups the identifier into readable blocks (e.g. "AB12-CD34-EF56") for easier reading.</summary>
    private static string FormatPairingCode(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return "";

        var upper = identifier.Trim().ToUpperInvariant();
        var groups = new List<string>();
        for (var i = 0; i < upper.Length; i += 4)
            groups.Add(upper.Substring(i, Math.Min(4, upper.Length - i)));
        return string.Join("-", groups);
    }

    /// <summary>Flashes the screen for a few seconds so an operator can locate it within a fleet (UA7).</summary>
    private async Task FlashIdentifyAsync()
    {
        _logger.LogInformation("Identify command received; flashing screen");
        await Dispatcher.UIThread.InvokeAsync(() => IsIdentifying = true);
        await Task.Delay(TimeSpan.FromSeconds(6));
        await Dispatcher.UIThread.InvokeAsync(() => IsIdentifying = false);
    }

    private void OnHubReconnecting()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ConnectionStatus = "Reconnecting...";
            ConnectionIndicatorColor = Brushes.Gold;
            StatusText = "Connection lost, reconnecting...";
        });
    }

    private void OnHubReconnected()
    {
        Dispatcher.UIThread.Post(UpdateConnectionStatus);
    }

    private void OnHubClosed()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ConnectionStatus = "Disconnected ✗";
            ConnectionIndicatorColor = Brushes.OrangeRed;
            StatusText = "Disconnected from server";
        });
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
            IsAwaitingApproval = false;
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

    private void OnCommandReceived(string command)
    {
        _logger.LogInformation("Handling remote command: {Command}", command);
        Dispatcher.UIThread.Post(() =>
        {
            switch (command)
            {
                case "restart":
                    if (_playlist.Count > 0)
                        StartPlayback();
                    break;
                case "reload":
                    if (_playlist.Count > 0)
                        ShowCurrentItem();
                    break;
                case "identify":
                    _ = FlashIdentifyAsync().ContinueWith(
                        t => _logger.LogError(t.Exception, "Identify flash faulted"),
                        TaskContinuationOptions.OnlyOnFaulted);
                    break;
                default:
                    _logger.LogWarning("Ignoring unknown remote command: {Command}", command);
                    break;
            }
        });
    }

    private void BuildPlaylist(ScreenConfiguration config)
    {
        _logger.LogInformation("Building playlist from configuration");

        _playlist.Clear();
        _currentIndex = 0;

        foreach (var campaign in config.Campaigns)
        {
            var sortedAssets = campaign.Assets.OrderBy(a => a.Position).ToList();
            foreach (var asset in sortedAssets)
            {
                var item = TryCreatePlaylistItem(campaign, asset);
                if (item != null)
                    _playlist.Add(item);
            }
        }

        TotalAssets = _playlist.Count;
        _logger.LogInformation("Playlist built with {Count} items", _playlist.Count);

        if (config.ShufflePlayback && _playlist.Count > 1)
        {
            ShufflePlaylist();
            _logger.LogInformation("Playlist shuffled (per-screen shuffle enabled)");
        }

        if (_playlist.Count == 0)
        {
            StatusText = "No content available";
            CurrentContentType = ContentType.None;
        }
    }

    private void ShufflePlaylist()
    {
        // Fisher-Yates shuffle for an unbiased in-place random order.
        for (var i = _playlist.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (_playlist[i], _playlist[j]) = (_playlist[j], _playlist[i]);
        }
    }

    private PlaylistItem? TryCreatePlaylistItem(
        Mireya.ApiClient.Models.CampaignDetail campaign,
        Mireya.ApiClient.Models.CampaignAssetItem asset)
    {
        var localPath = _assetSyncService.GetAssetLocalPath(asset.AssetId);
        var needsLocalFile = asset.AssetType != AssetType.Website;
        var hasLocalFile = !string.IsNullOrEmpty(localPath) && File.Exists(localPath);

        if (needsLocalFile && !hasLocalFile)
        {
            _logger.LogWarning(
                "Asset {AssetId} ({AssetName}) not found locally, skipping",
                asset.AssetId,
                asset.AssetName
            );
            return null;
        }

        return new PlaylistItem
        {
            CampaignId = campaign.Id,
            CampaignName = campaign.Name,
            AssetId = asset.AssetId,
            AssetName = asset.AssetName,
            AssetType = asset.AssetType,
            LocalPath = hasLocalFile ? localPath! : string.Empty,
            Source = asset.Source,
            DurationSeconds = asset.ResolvedDuration,
            Position = asset.Position,
            IsMuted = asset.IsMuted,
            ImageFit = asset.ImageFit,
        };
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

        // Report now-playing to the server for real-time admin visibility
        _ = ReportNowPlayingAsync(item);

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

        // Stop any playing video
        VideoStopRequested?.Invoke();

        CurrentContentType = ContentType.Image;
        CurrentVideoPath = null;
        CurrentVideoUri = null;
        CurrentWebsiteUrl = null;
        CurrentWebsiteUri = null;
        CurrentImageStretch = MapStretch(item.ImageFit);

        try
        {
            if (File.Exists(item.LocalPath))
            {
                var oldImage = CurrentImage;
                CurrentImage = new Bitmap(item.LocalPath);
                oldImage?.Dispose();
                FadeInImage();
            }
            else
            {
                _logger.LogWarning("Image file not found: {Path}", item.LocalPath);
                var oldImage = CurrentImage;
                CurrentImage = null;
                oldImage?.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load image: {Path}", item.LocalPath);
            var oldImage = CurrentImage;
            CurrentImage = null;
            oldImage?.Dispose();
        }

        // Set timer to advance after duration
        StartAdvanceTimer(item.DurationSeconds);
    }

    private static Stretch MapStretch(ImageFit fit) => fit switch
    {
        ImageFit.Cover => Stretch.UniformToFill,
        ImageFit.Fill => Stretch.Fill,
        _ => Stretch.Uniform,
    };

    /// <summary>
    ///     Restarts the image opacity at zero and animates it back to full on the next UI tick,
    ///     producing a fade-in via the Opacity transition declared on the image control.
    /// </summary>
    private void FadeInImage()
    {
        CurrentImageOpacity = 0;
        Dispatcher.UIThread.Post(() => CurrentImageOpacity = 1, DispatcherPriority.Background);
    }

    private void ShowVideo(PlaylistItem item)
    {
        _logger.LogDebug("Loading video: {Path}", item.LocalPath);

        CurrentContentType = ContentType.Video;
        CurrentVideoPath = item.LocalPath;
        CurrentVideoUri = TryCreateUri(item.LocalPath);
        CurrentImage = null;
        CurrentWebsiteUrl = null;
        CurrentWebsiteUri = null;

        if (string.IsNullOrEmpty(item.LocalPath) || !File.Exists(item.LocalPath))
        {
            _logger.LogWarning("Video file not found for asset {AssetId}", item.AssetId);
            AdvanceToNext();
            return;
        }

        // Trigger video playback in the UI component
        VideoPlaybackRequested?.Invoke(item.LocalPath, item.IsMuted);

        // Set timer to advance after duration
        StartAdvanceTimer(item.DurationSeconds);
    }

    private void ShowWebsite(PlaylistItem item)
    {
        _logger.LogDebug("Loading website: {Url}", item.Source);

        // Stop any playing video
        VideoStopRequested?.Invoke();

        // Flash fix A: set the URI *before* ContentType so Navigate() is called while
        // WebsiteAssetDisplay is still invisible.  NavigateInternal sets _isNavigating=true
        // immediately, which prevents OnEffectiveVisibilityChanged from revealing the
        // controller before the new page has loaded.
        CurrentWebsiteUrl = item.Source;
        CurrentWebsiteUri = TryCreateUri(item.Source);

        CurrentContentType = ContentType.Website;
        CurrentImage = null;
        CurrentVideoPath = null;
        CurrentVideoUri = null;

        if (CurrentWebsiteUri == null)
        {
            _logger.LogWarning("Invalid website URL for asset {AssetId}", item.AssetId);
            AdvanceToNext();
            return;
        }

        // Set timer to advance after duration
        StartAdvanceTimer(item.DurationSeconds);
    }

    private static Uri? TryCreateUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }

    private void StartAdvanceTimer(int durationSeconds)
    {
        // Stop existing timer
        if (_advanceTimer != null)
        {
            _advanceTimer.Stop();
        }
        else
        {
            // Create the timer only once and reuse it to avoid event handler leaks
            _advanceTimer = new DispatcherTimer();
            _advanceTimer.Tick += OnAdvanceTimerTick;
        }

        // Ensure minimum duration of 1 second to prevent rapid cycling
        var duration = Math.Max(durationSeconds, 1);
        _advanceTimer.Interval = TimeSpan.FromSeconds(duration);
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

    private async Task AutoHideOverlayAsync()
    {
        _logger.LogDebug("AutoStart: overlay will auto-hide in 10 s");
        await Task.Delay(TimeSpan.FromSeconds(10));
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsOverlayVisible = false;
            _logger.LogInformation("AutoStart: overlay hidden");
        });
    }

    public void Cleanup()
    {
        _logger.LogInformation("Cleaning up ContentDisplayViewModel");
        if (_advanceTimer != null)
        {
            _advanceTimer.Stop();
            _advanceTimer.Tick -= OnAdvanceTimerTick;
        }
        _hubService.OnConfigurationUpdateReceived -= OnConfigurationUpdateReceived;
        _hubService.OnStartAssetSync -= OnStartAssetSync;
        _hubService.OnCommandReceived -= OnCommandReceived;
        _hubService.OnReconnecting -= OnHubReconnecting;
        _hubService.OnReconnected -= OnHubReconnected;
        _hubService.OnClosed -= OnHubClosed;
        CurrentImage?.Dispose();
        CurrentImage = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cleanup();
        GC.SuppressFinalize(this);
    }

    private async Task ReportNowPlayingAsync(PlaylistItem item)
    {
        try
        {
            await _hubService.ReportNowPlayingAsync(item.AssetId, item.AssetName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report now-playing to server");
        }
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
    public bool IsMuted { get; set; }
    public ImageFit ImageFit { get; set; }
}

public enum ContentType
{
    None,
    Image,
    Video,
    Website,
}
