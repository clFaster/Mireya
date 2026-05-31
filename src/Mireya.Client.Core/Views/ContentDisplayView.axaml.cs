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
        // desktop, native components elsewhere). The active platform head supplies an
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

        // Create a borderless, transparent, always-on-top window containing
        // the same StatusOverlay control.  This is required because WebView2
        // renders into a Win32 HWND child window that always paints over
        // ordinary Avalonia visuals (the "airspace" problem).
        _overlayWindow = new Window
        {
            Title               = string.Empty,
            SizeToContent       = SizeToContent.WidthAndHeight,
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
            Content = new StatusOverlay { DataContext = _viewModel },
        };

        // Reposition whenever the overlay content changes size
        _overlayWindow.SizeChanged += (_, _) => UpdateOverlayPosition();

        // Track parent window movement / resize
        parentWindow.PositionChanged += (_, _) => UpdateOverlayPosition();
        parentWindow.SizeChanged     += (_, _) => UpdateOverlayPosition();

        // Open (but initially hidden) owned by the main window so it moves with it
        _overlayWindow.Show(parentWindow);
        _overlayWindow.IsVisible = false;

        UpdateOverlayVisibility();

        // Measure the overlay content after the first layout pass so the
        // initial position calculation has valid size information.
        Dispatcher.UIThread.Post(UpdateOverlayPosition, DispatcherPriority.Loaded);
    }

    // ──────────────────────────────────────────────────────────────
    // Overlay visibility / position helpers
    // ──────────────────────────────────────────────────────────────

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ContentDisplayViewModel.CurrentContentType)
                           or nameof(ContentDisplayViewModel.IsOverlayVisible))
        {
            UpdateOverlayVisibility();
        }
    }

    private void UpdateOverlayVisibility()
    {
        if (_overlayWindow == null || _viewModel == null)
            return;

        // Only show the floating window when a website is being displayed
        // AND the overlay itself is visible.  For image/video/none content the
        // AXAML StatusOverlay (in ContentDisplayView.axaml) is above the content
        // and does not need the separate top-level window.
        var shouldShow = _viewModel.CurrentContentType == ContentType.Website
                         && _viewModel.IsOverlayVisible;

        _overlayWindow.IsVisible = shouldShow;

        if (shouldShow)
            UpdateOverlayPosition();
    }

    private void UpdateOverlayPosition()
    {
        if (_overlayWindow == null || _parentWindow == null)
            return;

        try
        {
            var overlayW = _overlayWindow.ClientSize.Width;
            var overlayH = _overlayWindow.ClientSize.Height;
            if (overlayW <= 0 || overlayH <= 0)
                return;

            // Match the AXAML StatusOverlay margin of 20 px (logical)
            const double margin = 20.0;

            // Compute the logical point in the parent window that corresponds to
            // the desired bottom-right placement of the overlay.
            var logicalX = _parentWindow.ClientSize.Width  - margin - overlayW;
            var logicalY = _parentWindow.ClientSize.Height - margin - overlayH;

            // Convert to screen pixels using the parent window's coordinate system
            var screenPos = _parentWindow.PointToScreen(new Point(logicalX, logicalY));
            _overlayWindow.Position = screenPos;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateOverlayPosition failed: {ex}");
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

        _parentWindow = null;
    }
}
