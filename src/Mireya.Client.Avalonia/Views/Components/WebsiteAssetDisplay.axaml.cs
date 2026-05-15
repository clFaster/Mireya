using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Web.WebView2.Core;

namespace Mireya.Client.Avalonia.Views.Components;

public partial class WebsiteAssetDisplay : UserControl
{
    private Grid? _browserContainer;
    private StackPanel? _loadingPanel;
    private StackPanel? _errorPanel;
    private TextBlock? _errorMessage;
    private CoreWebView2Environment? _webViewEnvironment;
    private CoreWebView2Controller? _webViewController;
    private bool _isInitialized;
    private bool _isCreating;
    private Uri? _pendingUri;
    private IntPtr _cachedParentHwnd = IntPtr.Zero;
    private bool _windowOpened;
    private Window? _parentWindow;

    public WebsiteAssetDisplay()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _browserContainer = this.FindControl<Grid>("BrowserContainer");
        _loadingPanel = this.FindControl<StackPanel>("LoadingPanel");
        _errorPanel = this.FindControl<StackPanel>("ErrorPanel");
        _errorMessage = this.FindControl<TextBlock>("ErrorMessage");
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        // Use TopLevel.GetTopLevel (Avalonia 12+ recommended API) instead of
        // the deprecated e.Root which no longer reliably returns a Window.
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window window)
        {
            _parentWindow = window;
            var hwnd = TryGetHwnd(window);
            if (hwnd != IntPtr.Zero)
            {
                _cachedParentHwnd = hwnd;
                _windowOpened = true;
                System.Diagnostics.Debug.WriteLine(
                    $"WebsiteAssetDisplay attached — HWND ready: 0x{hwnd:X}"
                );
            }
            else
            {
                // The native Win32 window hasn't been created yet.
                // Subscribe to Opened, which fires after the HWND exists.
                System.Diagnostics.Debug.WriteLine(
                    "WebsiteAssetDisplay attached — HWND not ready, waiting for Window.Opened"
                );
                window.Opened += OnWindowOpened;
            }
        }
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.Opened -= OnWindowOpened;

            _cachedParentHwnd = TryGetHwnd(window);
            _windowOpened = true;

            System.Diagnostics.Debug.WriteLine(
                $"Window.Opened — HWND = 0x{_cachedParentHwnd:X}"
            );

            // If the control is already visible and waiting for the HWND,
            // kick off WebView2 creation now.
            if (IsEffectivelyVisible && !_isInitialized && !_isCreating)
            {
                Dispatcher.UIThread.Post(
                    async () => await CreateWebViewControllerAsync(),
                    DispatcherPriority.Render
                );
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Unhook the window event if we subscribed
        if (_parentWindow != null)
        {
            _parentWindow.Opened -= OnWindowOpened;
            _parentWindow = null;
        }

        // Dispose the WebView2 controller to free the HWND child window and COM resources
        DisposeWebView();

        _cachedParentHwnd = IntPtr.Zero;
        _windowOpened = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty)
            OnEffectiveVisibilityChanged(IsEffectivelyVisible);
    }

    private void OnEffectiveVisibilityChanged(bool isVisible)
    {
        if (isVisible)
        {
            if (!_isInitialized && !_isCreating && _windowOpened)
            {
                // Window is open, HWND should be available — create the WebView2 controller.
                Dispatcher.UIThread.Post(
                    async () => await CreateWebViewControllerAsync(),
                    DispatcherPriority.Render
                );
            }
            else if (_isInitialized && _webViewController != null)
            {
                // Already created — show and resize.
                _webViewController.IsVisible = true;
                Dispatcher.UIThread.Post(UpdateWebViewBounds, DispatcherPriority.Render);

                // If a navigation was queued while hidden, apply it now.
                if (_pendingUri != null)
                {
                    var uri = _pendingUri;
                    _pendingUri = null;
                    NavigateInternal(uri);
                }
            }
            // else: window not opened yet — OnWindowOpened will trigger creation later.
        }
        else
        {
            if (_webViewController != null)
                _webViewController.IsVisible = false;
        }
    }

    private async System.Threading.Tasks.Task CreateWebViewControllerAsync()
    {
        if (_isCreating || _isInitialized)
            return;

        if (_browserContainer == null || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ShowError("Not running on Windows or browser container is missing.");
            return;
        }

        // Final attempt to get the HWND if we still don't have it.
        if (_cachedParentHwnd == IntPtr.Zero)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window window)
                _cachedParentHwnd = TryGetHwnd(window);
        }

        if (_cachedParentHwnd == IntPtr.Zero)
        {
            ShowError(
                "Could not obtain the parent window handle (HWND).\n"
                + "The native window may not have been created yet."
            );
            return;
        }

        _isCreating = true;

        try
        {
            if (!TryGetWebView2RuntimeVersion(out var errorMessage))
            {
                ShowError(errorMessage);
                return;
            }

            var userDataFolder = System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "WebView2Data"
            );

            _webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                null, userDataFolder
            );

            System.Diagnostics.Debug.WriteLine(
                $"Creating controller for HWND 0x{_cachedParentHwnd:X}"
            );

            _webViewController = await _webViewEnvironment.CreateCoreWebView2ControllerAsync(
                _cachedParentHwnd
            );

            // Configure
            var settings = _webViewController.CoreWebView2.Settings;
            settings.IsScriptEnabled = true;
            settings.IsStatusBarEnabled = false;
            settings.AreDefaultContextMenusEnabled = false;
            settings.IsZoomControlEnabled = false;

            _browserContainer.SizeChanged += (_, _) =>
            {
                if (IsEffectivelyVisible) UpdateWebViewBounds();
            };

            this.LayoutUpdated += (_, _) =>
            {
                if (IsEffectivelyVisible && _webViewController != null) UpdateWebViewBounds();
            };

            _isInitialized = true;

            _loadingPanel!.IsVisible = false;
            _browserContainer.IsVisible = true;
            _errorPanel!.IsVisible = false;

            ApplyControllerVisibility();

            if (_pendingUri != null)
            {
                var uri = _pendingUri;
                _pendingUri = null;
                NavigateInternal(uri);
            }

            System.Diagnostics.Debug.WriteLine("WebView2 controller created successfully.");
        }
        catch (Exception ex)
        {
            var msg = $"WebView2 creation failed:\n{ex.GetType().Name}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine(msg);
            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            ShowError(msg);
        }
        finally
        {
            _isCreating = false;
        }
    }

    private static bool TryGetWebView2RuntimeVersion(out string errorMessage)
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            System.Diagnostics.Debug.WriteLine($"WebView2 runtime: {version}");
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"WebView2 runtime not found or not accessible.\n{ex.Message}";
            return false;
        }
    }

    private void ApplyControllerVisibility()
    {
        if (IsEffectivelyVisible)
        {
            _webViewController!.IsVisible = true;
            Dispatcher.UIThread.Post(UpdateWebViewBounds, DispatcherPriority.Render);
        }
        else
        {
            _webViewController!.IsVisible = false;
        }
    }

    private static IntPtr TryGetHwnd(TopLevel topLevel)
    {
        try
        {
            var handle = topLevel.TryGetPlatformHandle();
            if (handle != null)
                return handle.Handle;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TryGetHwnd failed: {ex}");
        }
        return IntPtr.Zero;
    }

    private void UpdateWebViewBounds()
    {
        if (_webViewController == null || _browserContainer == null)
            return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var transformedPoint = _browserContainer.TranslatePoint(new Point(0, 0), topLevel);
                var containerBounds = _browserContainer.Bounds;

                if (transformedPoint.HasValue)
                {
                    var scaling = topLevel.RenderScaling;
                    var x = (int)(transformedPoint.Value.X * scaling);
                    var y = (int)(transformedPoint.Value.Y * scaling);
                    var width = (int)(containerBounds.Width * scaling);
                    var height = (int)(containerBounds.Height * scaling);

                    if (width > 0 && height > 0)
                    {
                        _webViewController.Bounds = new System.Drawing.Rectangle(x, y, width, height);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateWebViewBounds failed: {ex}");
        }
    }

    public void Navigate(Uri? uri)
    {
        if (uri == null)
        {
            _pendingUri = null;
            if (_webViewController != null)
                _webViewController.IsVisible = false;
            return;
        }

        if (!_isInitialized || _webViewController?.CoreWebView2 == null)
        {
            _pendingUri = uri;
            if (_loadingPanel != null) _loadingPanel.IsVisible = true;
            if (_errorPanel != null) _errorPanel.IsVisible = false;
            return;
        }

        NavigateInternal(uri);
    }

    private void NavigateInternal(Uri uri)
    {
        if (_webViewController?.CoreWebView2 == null)
            return;

        try
        {
            _loadingPanel!.IsVisible = false;
            _browserContainer!.IsVisible = true;
            _errorPanel!.IsVisible = false;

            _webViewController.IsVisible = IsEffectivelyVisible;
            Dispatcher.UIThread.Post(UpdateWebViewBounds, DispatcherPriority.Render);

            // Remove any previous handler to prevent accumulation
            _webViewController.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
            _webViewController.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            _webViewController.CoreWebView2.Navigate(uri.AbsoluteUri);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NavigateInternal failed: {ex}");
            ShowError($"Navigation failed:\n{ex.Message}");
        }
    }

    private async void OnNavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs args
    )
    {
        if (_webViewController?.CoreWebView2 == null)
            return;

        _webViewController.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;

        try
        {
            const string muteScript = """
                (function() {
                    document.querySelectorAll('video, audio').forEach(el => {
                        el.muted = true;
                        el.volume = 0;
                    });
                })();
                """;
            await _webViewController.CoreWebView2.ExecuteScriptAsync(muteScript);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mute script failed: {ex}");
        }
    }

    private void ShowError(string? reason = null)
    {
        if (_loadingPanel != null)
            _loadingPanel.IsVisible = false;

        if (_errorPanel != null)
            _errorPanel.IsVisible = true;

        if (_errorMessage != null)
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                _errorMessage.Text = reason;
                _errorMessage.IsVisible = true;
            }
            else
            {
                _errorMessage.IsVisible = false;
            }
        }

        if (_browserContainer != null)
            _browserContainer.IsVisible = false;

        if (_webViewController != null)
            _webViewController.IsVisible = false;
    }

    private void DisposeWebView()
    {
        try
        {
            if (_webViewController != null)
            {
                _webViewController.IsVisible = false;

                if (_webViewController.CoreWebView2 != null)
                    _webViewController.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;

                _webViewController.Close();
                _webViewController = null;
            }

            _webViewEnvironment = null;
            _isInitialized = false;
            _isCreating = false;
            _pendingUri = null;

            System.Diagnostics.Debug.WriteLine("WebView2 resources disposed.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DisposeWebView failed: {ex}");
        }
    }
}
