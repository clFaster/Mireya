using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Models;
using Mireya.ApiClient.Services;
using ClientImageFit = Mireya.ApiClient.Models.ImageFit;

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
    private readonly bool _isDesignInstance;

    /// <summary>
    ///     Side-effect-free sample data for the XAML previewer. Runtime instances are
    ///     created through dependency injection using the public constructor below.
    /// </summary>
    public static ContentDisplayViewModel DesignInstance => new();

    // Customer assets can be much larger than the display. Decoding them at their native
    // resolution allocates width * height * 4 bytes in Skia's native heap on every playlist
    // loop, which can make Android's low-memory killer terminate the client. Keep the decoded
    // surface bounded independently of the uploaded file's dimensions.
    private const int MaxDecodeWidth = 1920;
    private const int MaxCachedImages = 5;

    private readonly Dictionary<Guid, CachedImageEntry> _decodedImageCache = [];
    private long _imageCacheUseCounter;

    [ObservableProperty]
    private string _screenName = "(not received yet)";

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
    private bool _isScreenInfoVisible;

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

    /// <summary>
    ///     Creates sample data for XAML design tools. Runtime code should resolve this
    ///     view model through dependency injection instead.
    /// </summary>
    public ContentDisplayViewModel()
    {
        _authenticationService = null!;
        _hubService = null!;
        _assetSyncService = null!;
        _logger = NullLogger<ContentDisplayViewModel>.Instance;
        _isDesignInstance = true;

        ScreenName = "Lobby Display";
        ConnectionStatus = "Connected";
        ConnectionIndicatorColor = Brushes.LimeGreen;
        StatusText = "Waiting for content...";
        CurrentContentType = ContentType.None;
        CurrentCampaignName = "Welcome campaign";
        CurrentAssetName = "Welcome screen";
        CurrentAssetPosition = 1;
        TotalAssets = 3;
        PairingCode = "MIREYA-7F3A";
    }

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
        _hubService.OnCommandReceived += OnCommandReceived;
        _hubService.OnReconnecting += OnHubReconnecting;
        _hubService.OnReconnected += OnHubReconnected;
        _hubService.OnClosed += OnHubClosed;

        _logger.LogInformation("ContentDisplayViewModel initialized");

        // Start authentication and connection in background
        _ = InitializeAsync()
            .ContinueWith(
                t => _logger.LogError(t.Exception, "Background initialization failed"),
                TaskContinuationOptions.OnlyOnFaulted
            );
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

            if (!await EnsureAuthenticatedAndConnectedAsync(state))
                return;

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

    private async Task<bool> EnsureAuthenticatedAndConnectedAsync(AuthenticationState state)
    {
        if (state == AuthenticationState.NotAuthenticated)
        {
            StatusText = "Authenticating...";
            var loginResult = await _authenticationService.LoginAsync();
            if (!loginResult.Success)
            {
                StatusText = $"Authentication failed: {loginResult.ErrorMessage}";
                ConnectionStatus = "Authentication failed ✗";
                return false;
            }
        }
        else if (state == AuthenticationState.Authenticated)
        {
            await ConnectToSignalRAsync();
            return _hubService.IsConnected;
        }

        UpdateConnectionStatus();
        return _hubService.IsConnected;
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
        _logger.LogInformation(
            "Authentication completed, SignalR connected: {IsConnected}",
            _hubService.IsConnected
        );
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

            var approved = string.Equals(
                info.ApprovalStatus,
                "Approved",
                StringComparison.OrdinalIgnoreCase
            );
            var rejected = string.Equals(
                info.ApprovalStatus,
                "Rejected",
                StringComparison.OrdinalIgnoreCase
            );

            await Dispatcher.UIThread.InvokeAsync(() =>
                ApplyApprovalStatus(info.ScreenIdentifier, info.ScreenName, approved, rejected)
            );

            if (approved)
            {
                _logger.LogInformation("Screen approved; awaiting configuration push");
                return;
            }

            if (firstPass)
            {
                _logger.LogInformation(
                    "Screen not yet approved (status: {Status}); showing pairing code {Code}",
                    info.ApprovalStatus,
                    info.ScreenIdentifier
                );
                firstPass = false;
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }

    private void ApplyApprovalStatus(
        string? screenIdentifier,
        string? screenName,
        bool approved,
        bool rejected
    )
    {
        PairingCode = FormatPairingCode(screenIdentifier);
        if (!string.IsNullOrWhiteSpace(screenName))
            ScreenName = screenName;

        IsAwaitingApproval = !approved;
        if (!approved)
        {
            ApprovalStatusText = rejected
                ? "This screen was rejected. Please contact your administrator."
                : "Waiting for an administrator to approve this screen…";
            StatusText = ApprovalStatusText;
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
            this.ConnectionStatus = "Reconnecting...";
            this.ConnectionIndicatorColor = Brushes.Gold;
            this.StatusText = "Connection lost, reconnecting...";
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
            this.ConnectionStatus = "Disconnected ✗";
            this.ConnectionIndicatorColor = Brushes.OrangeRed;
            this.StatusText = "Disconnected from server";
        });
    }

    private void OnConfigurationUpdateReceived(ScreenConfiguration config)
    {
        _logger.LogInformation(
            "Configuration received: {ScreenName} with {CampaignCount} campaigns",
            config.ScreenName,
            config.Campaigns.Count
        );

        var approved = string.Equals(
            config.ApprovalStatus,
            "Approved",
            StringComparison.OrdinalIgnoreCase
        );
        if (!approved)
        {
            _pendingConfiguration = null;
            var rejected = string.Equals(
                config.ApprovalStatus,
                "Rejected",
                StringComparison.OrdinalIgnoreCase
            );
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ResetPlaybackState();
                IsAwaitingApproval = true;
                ScreenName = config.ScreenName;
                ApprovalStatusText = rejected
                    ? "This screen was rejected. Please contact your administrator."
                    : "Waiting for an administrator to approve this screen…";
                StatusText = ApprovalStatusText;
            });
            return;
        }

        // Store the approved configuration but DON'T build the playlist yet - wait for assets to sync.
        _pendingConfiguration = config;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsAwaitingApproval = false;
            ScreenName = config.ScreenName;
            StatusText = "Syncing assets...";
        });
    }

    private async Task OnStartAssetSync(List<Mireya.ApiClient.Models.CampaignSyncInfo> campaigns)
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
                    _ = FlashIdentifyAsync()
                        .ContinueWith(
                            t => _logger.LogError(t.Exception, "Identify flash faulted"),
                            TaskContinuationOptions.OnlyOnFaulted
                        );
                    break;
                case "next":
                    if (_playlist.Count > 0)
                        AdvanceToNext();
                    break;
                case "previous":
                    if (_playlist.Count > 0)
                        GoToPrevious();
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

        ResetPlaybackState();

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
            ClearCurrentImage();
        }
    }

    private void ResetPlaybackState()
    {
        _advanceTimer?.Stop();
        VideoStopRequested?.Invoke();
        _playlist.Clear();
        _currentIndex = 0;
        TotalAssets = 0;
        CurrentAssetName = "";
        CurrentCampaignName = "";
        CurrentAssetPosition = 0;
        CurrentContentType = ContentType.None;
        CurrentVideoPath = null;
        CurrentVideoUri = null;
        CurrentWebsiteUrl = null;
        CurrentWebsiteUri = null;
        ClearCurrentImage();
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
        Mireya.ApiClient.Models.CampaignAssetItem asset
    )
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
            LocalPath = hasLocalFile ? localPath : string.Empty,
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

        ApplyCurrentItem(_playlist[_currentIndex]);
    }

    /// <summary>Applies a playlist item to the view (status text, now-playing report, renderer swap).</summary>
    private void ApplyCurrentItem(PlaylistItem item)
    {
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
                SetCurrentImage(GetOrDecodeImage(item));
                TrimImageCache();
                FadeInImage();
            }
            else
            {
                _logger.LogWarning("Image file not found: {Path}", item.LocalPath);
                ClearCurrentImage();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load image: {Path}", item.LocalPath);
            ClearCurrentImage();
        }

        // Set timer to advance after duration
        StartAdvanceTimer(item.DurationSeconds);
    }

    private Bitmap GetOrDecodeImage(PlaylistItem item)
    {
        var file = new FileInfo(item.LocalPath);
        if (
            _decodedImageCache.TryGetValue(item.AssetId, out var cached)
            && cached.FileLength == file.Length
            && cached.LastWriteTimeUtc == file.LastWriteTimeUtc
        )
        {
            cached.LastAccess = ++_imageCacheUseCounter;
            return cached.Bitmap;
        }

        // Decode before removing a stale entry so a transient file error does not destroy
        // the last usable rendition. Once the new bitmap is cached, SetCurrentImage disposes
        // a stale currently displayed bitmap because the cache no longer owns it.
        var bitmap = DecodeForDisplay(item.LocalPath);
        if (
            _decodedImageCache.Remove(item.AssetId, out var stale)
            && !ReferenceEquals(stale.Bitmap, CurrentImage)
        )
            stale.Bitmap.Dispose();

        _decodedImageCache[item.AssetId] = new CachedImageEntry(
            bitmap,
            file.Length,
            file.LastWriteTimeUtc,
            ++_imageCacheUseCounter
        );
        return bitmap;
    }

    private Bitmap DecodeForDisplay(string path)
    {
        using var stream = File.OpenRead(path);
        var bitmap = Bitmap.DecodeToWidth(
            stream,
            MaxDecodeWidth,
            BitmapInterpolationMode.MediumQuality
        );
        _logger.LogDebug(
            "Decoded image {Path} to {Width}x{Height}",
            path,
            bitmap.PixelSize.Width,
            bitmap.PixelSize.Height
        );
        return bitmap;
    }

    private void TrimImageCache()
    {
        while (_decodedImageCache.Count > MaxCachedImages)
        {
            var candidate = _decodedImageCache
                .Where(pair => !ReferenceEquals(pair.Value.Bitmap, CurrentImage))
                .MinBy(pair => pair.Value.LastAccess);

            if (candidate.Value is null)
                return;

            _decodedImageCache.Remove(candidate.Key);
            candidate.Value.Bitmap.Dispose();
        }
    }

    private static Stretch MapStretch(ClientImageFit fit) =>
        fit switch
        {
            ClientImageFit.Cover => Stretch.UniformToFill,
            ClientImageFit.Fill => Stretch.Fill,
            _ => Stretch.Uniform,
        };

    /// <summary>
    ///     Restarts the image opacity at zero and animates it back to full on the next UI tick,
    ///     producing a fade-in via the Opacity transition declared on the image control.
    /// </summary>
    private void FadeInImage()
    {
        this.CurrentImageOpacity = 0;
        // Use Default (lowest foreground) priority rather than Background: Background work
        // is starved on Android under the continuous render loop, which would leave the
        // image stuck at opacity 0 (invisible). Default still defers to the next dispatcher
        // cycle so the Opacity transition animates the fade-in.
        Dispatcher.UIThread.Post(() => this.CurrentImageOpacity = 1, DispatcherPriority.Default);
    }

    /// <summary>
    ///     Binds a new bitmap to the image control and disposes the one it replaces.
    ///     An Avalonia <see cref="Bitmap" /> owns native (Skia) pixel memory that is only
    ///     released on disposal, so merely dropping the managed reference leaves a decoded
    ///     frame alive until a finalizer eventually runs. A single 2400x1600 image already
    ///     costs roughly 15 MiB, so on a memory-constrained device such as an Android TV box
    ///     a looping campaign accumulates hundreds of MiB and gets killed by the
    ///     low-memory killer. Every path that replaces or releases the current image must
    ///     therefore go through this helper (or <see cref="ClearCurrentImage" />).
    /// </summary>
    private void SetCurrentImage(Bitmap? image)
    {
        var oldImage = this.CurrentImage;
        if (ReferenceEquals(oldImage, image))
            return;

        // Unbind first so the control never draws a bitmap that is about to be disposed,
        // then release the native memory of an image not retained by the bounded cache.
        this.CurrentImage = image;
        if (oldImage is not null && !IsCachedImage(oldImage))
            oldImage.Dispose();
    }

    /// <summary>Unbinds and disposes the image currently displayed, if any.</summary>
    private void ClearCurrentImage() => SetCurrentImage(null);

    private bool IsCachedImage(Bitmap image) =>
        _decodedImageCache.Values.Any(entry => ReferenceEquals(entry.Bitmap, image));

    private void ShowVideo(PlaylistItem item)
    {
        _logger.LogDebug("Loading video: {Path}", item.LocalPath);

        CurrentContentType = ContentType.Video;
        CurrentVideoPath = item.LocalPath;
        CurrentVideoUri = TryCreateUri(item.LocalPath);
        ClearCurrentImage();
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
    }

    /// <summary>Advances when the active renderer reports that the video ended naturally.</summary>
    public void NotifyVideoPlaybackEnded(string path)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            CompleteVideoPlayback(path);
            return;
        }

        Dispatcher.UIThread.Post(() => CompleteVideoPlayback(path));
    }

    private void CompleteVideoPlayback(string path)
    {
        if (
            _disposed
            || CurrentContentType != ContentType.Video
            || !string.Equals(CurrentVideoPath, path, StringComparison.Ordinal)
        )
            return;

        AdvanceToNext();
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
        ClearCurrentImage();
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
            // Create the timer only once and reuse it to avoid event handler leaks.
            // Bind explicitly to the UI thread dispatcher and tick at Default priority:
            // the parameterless DispatcherTimer ctor uses DispatcherPriority.Background,
            // which sits below Input and gets starved on Android by the continuous
            // compositor/video render loop, so the playlist never advances automatically
            // there. Default ("lowest foreground") matches the priority used by the
            // remote-command path (Dispatcher.UIThread.Post) that already advances reliably.
            _advanceTimer = new DispatcherTimer(DispatcherPriority.Default, Dispatcher.UIThread);
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

    public void ShowScreenInfo()
    {
        if (IsScreenInfoVisible)
            return;

        IsScreenInfoVisible = true;
        _logger.LogDebug("Screen Info page opened");
    }

    public void HideScreenInfo()
    {
        if (!IsScreenInfoVisible)
            return;

        IsScreenInfoVisible = false;
        _logger.LogDebug("Screen Info page closed");
    }

    public void ToggleScreenInfo()
    {
        if (IsScreenInfoVisible)
            HideScreenInfo();
        else
            ShowScreenInfo();
    }

    [RelayCommand]
    private void CloseScreenInfo()
    {
        HideScreenInfo();
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
        GoToPrevious();
    }

    private void GoToPrevious()
    {
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
        if (_isDesignInstance)
            return;

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
        ClearCurrentImage();
        foreach (var cached in _decodedImageCache.Values)
            cached.Bitmap.Dispose();
        _decodedImageCache.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
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

    private sealed class CachedImageEntry(
        Bitmap bitmap,
        long fileLength,
        DateTime lastWriteTimeUtc,
        long lastAccess
    )
    {
        public Bitmap Bitmap { get; } = bitmap;
        public long FileLength { get; } = fileLength;
        public DateTime LastWriteTimeUtc { get; } = lastWriteTimeUtc;
        public long LastAccess { get; set; } = lastAccess;
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
    public ClientImageFit ImageFit { get; set; }
}

public enum ContentType
{
    None,
    Image,
    Video,
    Website,
}
