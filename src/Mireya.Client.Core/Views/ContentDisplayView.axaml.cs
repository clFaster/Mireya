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
    private Window? _curtainWindow;
    private Window? _parentWindow;
    private ContentDisplayViewModel? _viewModel;
    private IWebsiteRenderer? _websiteRenderer;
    private IVideoRenderer? _videoRenderer;

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

        var websiteHost = this.FindControl<ContentControl>("WebsiteHost");
        if (websiteHost != null)
        {
            var websiteControl = factory.CreateWebsiteRenderer();
            websiteHost.Content = websiteControl;
            _websiteRenderer = websiteControl as IWebsiteRenderer;
        }

        var videoHost = this.FindControl<ContentControl>("VideoHost");
        if (videoHost != null)
        {
            var videoControl = factory.CreateVideoRenderer();
            videoHost.Content = videoControl;
            _videoRenderer = videoControl as IVideoRenderer;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // DataContext wiring
    // ──────────────────────────────────────────────────────────────

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Tear down any previous overlay and unsubscribe old VM
        CloseOverlayWindow();

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
            _videoRenderer.FirstFrameReady += vm.NotifyVideoFirstFrame;
        }

        // Forward website "page painted" notifications so the transition curtain can lift.
        if (_websiteRenderer != null)
            _websiteRenderer.ContentReady += vm.NotifyWebsiteContentReady;

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

        // Subscribe to VM changes that affect the overlay window visibility
        vm.PropertyChanged += OnViewModelPropertyChanged;

        // Create the floating overlay window once the parent Window is known.
        // If we're already in the visual tree, do it immediately; otherwise
        // wait for AttachedToVisualTree.
        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        if (parentWindow != null)
            SetupOverlayWindow(parentWindow);
        else
            AttachedToVisualTree += OnFirstAttach;
    }

    private void OnFirstAttach(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AttachedToVisualTree -= OnFirstAttach;

        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        if (parentWindow != null && _viewModel != null)
            SetupOverlayWindow(parentWindow);
    }

    // ──────────────────────────────────────────────────────────────
    // Floating overlay window (paints over the Win32 WebView2 HWND)
    // ──────────────────────────────────────────────────────────────

    private void SetupOverlayWindow(Window parentWindow)
    {
        if (_overlayWindow != null || _viewModel == null)
            return;

        _parentWindow = parentWindow;

        // Create a borderless, transparent, always-on-top window that mirrors the
        // overlay layer.  This is required because both WebView2 (websites) and the
        // LibVLC VideoView (videos) render into native child windows that always paint
        // over ordinary Avalonia visuals (the "airspace" problem). The window covers the
        // whole client area so both the status panel and the identify flash are visible
        // over native content (UA9 / UA10).
        _overlayWindow = new Window
        {
            Title               = string.Empty,
            // Remove all window chrome (title bar + border) using Avalonia 12 API
            WindowDecorations   = WindowDecorations.None,
            Topmost             = true,
            ShowInTaskbar       = false,
            CanResize           = false,
            Background          = Brushes.Transparent,
            TransparencyLevelHint = new[]
            {
                WindowTransparencyLevel.Transparent,
                WindowTransparencyLevel.AcrylicBlur,
            },
            Content = new OverlayLayer { DataContext = _viewModel },
        };

        // Track parent window movement / resize
        parentWindow.PositionChanged += (_, _) => UpdateFloatingWindowPositions();
        parentWindow.SizeChanged     += (_, _) => UpdateFloatingWindowPositions();

        // The transition curtain lives in its own top-most transparent window for the same
        // "airspace" reason as the overlay: it must cover the native website/video
        // surfaces while an asset is swapped underneath it. It is created above the overlay
        // window so the dip-to-black masks everything during a transition.
        _curtainWindow = new Window
        {
            Title               = string.Empty,
            WindowDecorations   = WindowDecorations.None,
            Topmost             = true,
            ShowInTaskbar       = false,
            CanResize           = false,
            Background          = Brushes.Transparent,
            TransparencyLevelHint = new[]
            {
                WindowTransparencyLevel.Transparent,
                WindowTransparencyLevel.AcrylicBlur,
            },
            Content = new TransitionCurtain { DataContext = _viewModel },
        };

        // Open (but initially hidden) owned by the main window so it moves with it
        _overlayWindow.Show(parentWindow);
        _overlayWindow.IsVisible = false;

        _curtainWindow.Show(parentWindow);
        _curtainWindow.IsVisible = false;

        UpdateOverlayVisibility();
        UpdateCurtainVisibility();

        // Measure the overlay content after the first layout pass so the
        // initial position calculation has valid size information.
        Dispatcher.UIThread.Post(UpdateFloatingWindowPositions, DispatcherPriority.Loaded);
    }

    // ──────────────────────────────────────────────────────────────
    // Overlay visibility / position helpers
    // ──────────────────────────────────────────────────────────────

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ContentDisplayViewModel.CurrentContentType)
                           or nameof(ContentDisplayViewModel.IsOverlayVisible)
                           or nameof(ContentDisplayViewModel.IsIdentifying))
        {
            UpdateOverlayVisibility();
        }
        else if (e.PropertyName is nameof(ContentDisplayViewModel.IsTransitionActive))
        {
            UpdateCurtainVisibility();
        }
    }

    private void UpdateCurtainVisibility()
    {
        if (_curtainWindow == null || _viewModel == null)
            return;

        var shouldShow = _viewModel.IsTransitionActive;

        if (shouldShow)
            UpdateCurtainPosition();

        _curtainWindow.IsVisible = shouldShow;
    }

    private void UpdateOverlayVisibility()
    {
        if (_overlayWindow == null || _viewModel == null)
            return;

        // The floating window is only needed over native surfaces (video / website),
        // which paint over the inline AXAML overlays. For image / idle content the inline
        // OverlayLayer in ContentDisplayView.axaml already sits above the content.
        var isNativeContent = _viewModel.CurrentContentType is ContentType.Website
                                                             or ContentType.Video;

        // Keep it hidden unless something actually needs to be shown, so the transparent
        // top-level window does not needlessly sit over the content.
        var hasOverlayContent = _viewModel.IsOverlayVisible || _viewModel.IsIdentifying;

        var shouldShow = isNativeContent && hasOverlayContent;

        _overlayWindow.IsVisible = shouldShow;

        if (shouldShow)
            UpdateOverlayPosition();
    }

    private void UpdateFloatingWindowPositions()
    {
        UpdateOverlayPosition();
        UpdateCurtainPosition();
    }

    private void UpdateOverlayPosition()
    {
        PositionWindowOverParent(_overlayWindow);
    }

    private void UpdateCurtainPosition()
    {
        PositionWindowOverParent(_curtainWindow);
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

            // Cover the full client area of the parent window so the status panel, the
            // identify flash and the transition curtain all render over native content.
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

        CloseOverlayWindow();
    }

    private void CloseOverlayWindow()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        if (_overlayWindow != null)
        {
            _overlayWindow.Close();
            _overlayWindow = null;
        }

        if (_curtainWindow != null)
        {
            _curtainWindow.Close();
            _curtainWindow = null;
        }

        _parentWindow = null;
    }
}
