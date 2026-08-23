using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Mireya.Client.Avalonia.Platform;
using Mireya.Client.Avalonia.ViewModels;
using Mireya.Client.Avalonia.Views.Components;

namespace Mireya.Client.Avalonia.Views;

public partial class ContentDisplayView : UserControl
{
    private Window? _overlayWindow;
    private Window? _parentWindow;
    private ContentDisplayViewModel? _viewModel;
    private IWebsiteRenderer? _websiteRenderer;
    private IVideoRenderer? _videoRenderer;
    private ContentControl? _websiteHost;
    private ContentControl? _videoHost;
    private Control? _websiteControl;
    private Control? _videoControl;
    private bool _videoRendererHasBeenAttached;

    public ContentDisplayView()
    {
        InitializeComponent();
        CreatePlatformRenderers();
        DataContextChanged += OnDataContextChanged;
    }

    // ──────────────────────────────────────────────────────────────
    // Platform renderer hosting
    // ──────────────────────────────────────────────────────────────

    private void CreatePlatformRenderers()
    {
        // Website and video rendering is platform specific (WebView2 / LibVLC on
        // desktop, Android WebView / Media3 on Android). The active platform head supplies an
        // IAssetViewFactory through dependency injection; resolve it and host the
        // resulting controls inside the placeholders declared in XAML.
        var factory = App.Services?.GetService<IAssetViewFactory>();
        if (factory == null)
            return;

        _websiteHost = this.FindControl<ContentControl>("WebsiteHost");
        _websiteControl = _websiteHost is null ? null : factory.CreateWebsiteRenderer();
        _websiteRenderer = _websiteControl as IWebsiteRenderer;

        _videoHost = this.FindControl<ContentControl>("VideoHost");
        _videoControl = _videoHost is null ? null : factory.CreateVideoRenderer();
        _videoRenderer = _videoControl as IVideoRenderer;
    }

    // ──────────────────────────────────────────────────────────────
    // DataContext wiring
    // ──────────────────────────────────────────────────────────────

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Tear down any previous identify window and unsubscribe old VM
        CloseIdentifyWindow();

        if (DataContext is not ContentDisplayViewModel vm)
        {
            _viewModel = null;
            return;
        }

        _viewModel = vm;

        // Wire video component events to the platform video renderer
        if (_videoRenderer != null)
        {
            vm.VideoPlaybackRequested += _videoRenderer.Play;
            vm.VideoStopRequested += _videoRenderer.Stop;
            _videoRenderer.PlaybackEnded += vm.NotifyVideoPlaybackEnded;
        }

        // Drive the platform website renderer on URI changes (kept as anonymous lambda
        // for symmetry with the original design; unsubscription not required in practice
        // because the VM lifetime matches the View lifetime here)
        if (_websiteRenderer != null)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ContentDisplayViewModel.CurrentWebsiteUri))
                    _websiteRenderer.Navigate(vm.CurrentWebsiteUri);
            };
        }

        // Subscribe to VM changes that affect native renderer and identify visibility.
        vm.PropertyChanged += OnViewModelPropertyChanged;
        UpdatePlatformRendererHosts();

        // Create the floating overlay window once the parent Window is known.
        // If we're already in the visual tree, do it immediately; otherwise
        // wait for AttachedToVisualTree.
        if (TopLevel.GetTopLevel(this) is Window parentWindow)
            SetupIdentifyWindow(parentWindow);
        else
            AttachedToVisualTree += OnFirstAttach;
    }

    private void OnFirstAttach(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AttachedToVisualTree -= OnFirstAttach;

        if (TopLevel.GetTopLevel(this) is Window parentWindow && _viewModel != null)
            SetupIdentifyWindow(parentWindow);
    }

    // ──────────────────────────────────────────────────────────────
    // Floating identify window (paints over the Win32 WebView2 HWND)
    // ──────────────────────────────────────────────────────────────

    private void SetupIdentifyWindow(Window parentWindow)
    {
        if (_overlayWindow != null || _viewModel == null)
            return;

        _parentWindow = parentWindow;

        // Create a borderless, transparent, always-on-top window that mirrors the
        // identify flash. This is required because both WebView2 (websites) and the
        // LibVLC VideoView (videos) render into native child windows that always paint
        // over ordinary Avalonia visuals (the "airspace" problem). The window covers the
        // whole client area so the identify flash is visible over native content.
        _overlayWindow = new Window
        {
            Title = string.Empty,
            // Remove all window chrome (title bar + border) using Avalonia 12 API
            WindowDecorations = WindowDecorations.None,
            Topmost = true,
            ShowInTaskbar = false,
            CanResize = false,
            Background = Brushes.Transparent,
            TransparencyLevelHint = new[]
            {
                WindowTransparencyLevel.Transparent,
                WindowTransparencyLevel.AcrylicBlur,
            },
            Content = new IdentifyOverlay { DataContext = _viewModel },
        };

        // Track parent window movement / resize
        parentWindow.PositionChanged += (_, _) => UpdateFloatingWindowPositions();
        parentWindow.SizeChanged += (_, _) => UpdateFloatingWindowPositions();

        // Open (but initially hidden) owned by the main window so it moves with it
        _overlayWindow.Show(parentWindow);
        _overlayWindow.IsVisible = false;

        UpdateIdentifyVisibility();

        // Measure the overlay content after the first layout pass so the
        // initial position calculation has valid size information.
        Dispatcher.UIThread.Post(UpdateFloatingWindowPositions, DispatcherPriority.Loaded);
    }

    // ──────────────────────────────────────────────────────────────
    // Identify visibility / position helpers
    // ──────────────────────────────────────────────────────────────

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ContentDisplayViewModel.CurrentContentType))
        {
            UpdatePlatformRendererHosts();
            UpdateIdentifyVisibility();
        }
        else if (e.PropertyName is nameof(ContentDisplayViewModel.IsScreenInfoVisible))
        {
            UpdatePlatformRendererHosts();
        }
        else if (e.PropertyName is nameof(ContentDisplayViewModel.IsIdentifying))
        {
            UpdateIdentifyVisibility();
        }
    }

    private void UpdatePlatformRendererHosts()
    {
        if (_viewModel == null)
            return;

        // NativeControlHost creates the underlying WebView / media player as soon as it is
        // attached to the visual tree, even when its Avalonia host is invisible. Attach only
        // the renderer that is actively needed so image-only campaigns do not keep two
        // hidden native surfaces and their composition loops alive.
        var showPlayback = !_viewModel.IsScreenInfoVisible;

        if (_websiteHost != null)
        {
            _websiteHost.IsVisible =
                showPlayback && _viewModel.CurrentContentType == ContentType.Website;
            _websiteHost.Content =
                _viewModel.CurrentContentType == ContentType.Website ? _websiteControl : null;
        }

        if (_videoHost != null)
        {
            var isVideoActive = _viewModel.CurrentContentType == ContentType.Video;
            _videoHost.IsVisible = showPlayback && isVideoActive;
            if (isVideoActive)
                _videoRendererHasBeenAttached = true;

            // LibVLCSharp's Avalonia VideoView owns a native surface that must stay parented
            // to the original host. Detaching and reattaching it for every playlist item can
            // create a new top-level video window. The host's IsVisible binding still hides
            // the retained control while non-video content is active. Android opts out and
            // continues detaching its native player to release resources.
            var retainInactiveRenderer =
                _videoRendererHasBeenAttached && _videoRenderer?.KeepAttachedWhenInactive == true;
            _videoHost.Content = isVideoActive || retainInactiveRenderer ? _videoControl : null;
        }
    }

    private void UpdateIdentifyVisibility()
    {
        if (_overlayWindow == null || _viewModel == null)
            return;

        // The floating window is only needed over native surfaces (video / website),
        // which paint over the inline Avalonia identify flash.
        var isNativeContent =
            _viewModel.CurrentContentType is ContentType.Website or ContentType.Video;

        var shouldShow = isNativeContent && _viewModel.IsIdentifying;

        _overlayWindow.IsVisible = shouldShow;

        if (shouldShow)
            UpdateOverlayPosition();
    }

    private void UpdateFloatingWindowPositions()
    {
        UpdateOverlayPosition();
    }

    private void UpdateOverlayPosition()
    {
        PositionWindowOverParent(_overlayWindow);
    }

    private void PositionWindowOverParent(Window? window)
    {
        if (window == null || _parentWindow == null)
            return;

        try
        {
            var clientW = _parentWindow.ClientSize.Width;
            var clientH = _parentWindow.ClientSize.Height;
            if (clientW <= 0 || clientH <= 0)
                return;

            // Cover the full client area of the parent window so the status panel and the
            // identify flash render over native content.
            window.Width = clientW;
            window.Height = clientH;
            window.Position = _parentWindow.PointToScreen(new Point(0, 0));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PositionWindowOverParent failed: {ex}");
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Cleanup
    // ──────────────────────────────────────────────────────────────

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Cancel any pending first-attach subscription
        AttachedToVisualTree -= OnFirstAttach;

        CloseIdentifyWindow();
    }

    private void CloseIdentifyWindow()
    {
        if (_viewModel != null)
        {
            if (_videoRenderer != null)
            {
                _viewModel.VideoPlaybackRequested -= _videoRenderer.Play;
                _viewModel.VideoStopRequested -= _videoRenderer.Stop;
                _videoRenderer.PlaybackEnded -= _viewModel.NotifyVideoPlaybackEnded;
            }

            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        if (_overlayWindow != null)
        {
            _overlayWindow.Close();
            _overlayWindow = null;
        }

        _parentWindow = null;
    }
}
