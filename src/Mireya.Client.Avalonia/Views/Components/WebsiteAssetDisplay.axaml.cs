using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Microsoft.Web.WebView2.Core;

namespace Mireya.Client.Avalonia.Views.Components;

public partial class WebsiteAssetDisplay : UserControl
{
    private Grid? _browserContainer;
    private StackPanel? _loadingPanel;
    private StackPanel? _errorPanel;
    private CoreWebView2Environment? _webViewEnvironment;
    private CoreWebView2Controller? _webViewController;
    private bool _isInitialized;
    private Uri? _pendingUri;

    public WebsiteAssetDisplay()
    {
        InitializeComponent();
        InitializeWebView();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _browserContainer = this.FindControl<Grid>("BrowserContainer");
        _loadingPanel = this.FindControl<StackPanel>("LoadingPanel");
        _errorPanel = this.FindControl<StackPanel>("ErrorPanel");
    }

    private void InitializeWebView()
    {
        if (_browserContainer == null || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ShowError();
            return;
        }

        try
        {
            // Wait for the control to be loaded and get its window handle
            this.Loaded += async (_, __) => await CreateWebViewControllerAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize WebView2: {ex}");
            ShowError();
        }
    }

    private async System.Threading.Tasks.Task CreateWebViewControllerAsync()
    {
        try
        {
            // Get the user data folder for WebView2
            var userDataFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Mireya",
                "WebView2"
            );

            // Create WebView2 environment
            _webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

            // Get the parent window handle from the visual root
            if (this.VisualRoot is TopLevel topLevel)
            {
                var hwnd = GetWindowHandleFromTopLevel(topLevel);

                if (hwnd != IntPtr.Zero)
                {
                    // Create the WebView2 controller
                    _webViewController =
                        await _webViewEnvironment.CreateCoreWebView2ControllerAsync(hwnd);

                    // Configure the WebView2 settings
                    var settings = _webViewController.CoreWebView2.Settings;
                    settings.IsScriptEnabled = true;
                    settings.IsStatusBarEnabled = false;
                    settings.AreDefaultContextMenusEnabled = false;
                    settings.IsZoomControlEnabled = false;

                    // Set bounds to fill the container
                    UpdateWebViewBounds();

                    // Subscribe to size changes
                    _browserContainer!.SizeChanged += (_, __) => UpdateWebViewBounds();

                    // Hide WebView initially until Navigate is called
                    _webViewController.IsVisible = false;
                    _isInitialized = true;

                    // If there's a pending URI, navigate to it now
                    if (_pendingUri != null)
                    {
                        NavigateInternal(_pendingUri);
                        _pendingUri = null;
                    }

                    _loadingPanel!.IsVisible = false;
                    _browserContainer.IsVisible = true;
                    _errorPanel!.IsVisible = false;

                    System.Diagnostics.Debug.WriteLine(
                        "WebView2 controller created and initialized"
                    );
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to get window handle for WebView2");
                    ShowError();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No VisualRoot found");
                ShowError();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create WebView2 controller: {ex}");
            ShowError();
        }
    }

    private static IntPtr GetWindowHandleFromTopLevel(TopLevel topLevel)
    {
        try
        {
            // Try to get HWND through TryGetPlatformHandle
            var platformHandle = topLevel.TryGetPlatformHandle();
            if (platformHandle != null)
            {
                // platformHandle is an IPlatformHandle - get the handle directly
                return new IntPtr(platformHandle.Handle.ToInt64());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get window handle: {ex}");
        }

        return IntPtr.Zero;
    }

    private void UpdateWebViewBounds()
    {
        if (_webViewController == null || _browserContainer == null)
        {
            return;
        }

        try
        {
            // Get the position of the container relative to the window
            if (this.VisualRoot is TopLevel topLevel)
            {
                // Transform the container's position to window coordinates
                var containerBounds = _browserContainer.Bounds;
                var transformedPoint = _browserContainer.TranslatePoint(new Point(0, 0), topLevel);

                if (transformedPoint.HasValue)
                {
                    // Get the scaling factor for the window
                    var scaling = topLevel.RenderScaling;

                    // Calculate pixel coordinates (WebView2 uses physical pixels)
                    var x = (int)(transformedPoint.Value.X * scaling);
                    var y = (int)(transformedPoint.Value.Y * scaling);
                    var width = (int)(containerBounds.Width * scaling);
                    var height = (int)(containerBounds.Height * scaling);

                    // Only update if we have valid dimensions
                    if (width > 0 && height > 0)
                    {
                        _webViewController.Bounds = new System.Drawing.Rectangle(
                            x,
                            y,
                            width,
                            height
                        );
                        System.Diagnostics.Debug.WriteLine(
                            $"WebView2 bounds set to: {x}, {y}, {width}x{height}"
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update WebView bounds: {ex}");
        }
    }

    /// <summary>
    /// Navigate to a specific URL and mute audio.
    /// </summary>
    public void Navigate(Uri? uri)
    {
        if (uri == null)
        {
            // Hide webview when no URI
            if (_webViewController != null)
            {
                _webViewController.IsVisible = false;
            }
            return;
        }

        System.Diagnostics.Debug.WriteLine(
            $"Navigate called for URL: {uri.AbsoluteUri}, initialized: {_isInitialized}"
        );

        if (!_isInitialized || _webViewController?.CoreWebView2 == null)
        {
            // Store the URI to navigate once initialized
            _pendingUri = uri;
            _loadingPanel!.IsVisible = true;
            _errorPanel!.IsVisible = false;
            return;
        }

        NavigateInternal(uri);
    }

    private void NavigateInternal(Uri uri)
    {
        if (_webViewController?.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"NavigateInternal to URL: {uri.AbsoluteUri}");
            _loadingPanel!.IsVisible = false;
            _browserContainer!.IsVisible = true;
            _errorPanel!.IsVisible = false;

            // Make sure WebView is visible and bounds are updated
            _webViewController.IsVisible = true;
            UpdateWebViewBounds();

            // Navigate to the URL
            _webViewController.CoreWebView2.Navigate(uri.AbsoluteUri);

            // Inject script to mute audio/video after navigation completes
            _webViewController.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to navigate to {uri}: {ex}");
            ShowError();
        }
    }

    private async void OnNavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs args
    )
    {
        if (_webViewController?.CoreWebView2 == null)
        {
            return;
        }

        // Unsubscribe to prevent multiple calls
        _webViewController.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;

        try
        {
            var muteScript =
                @"
                (function() {
                    var videos = document.getElementsByTagName('video');
                    for (var i = 0; i < videos.length; i++) {
                        videos[i].muted = true;
                        videos[i].volume = 0;
                    }
                    var audios = document.getElementsByTagName('audio');
                    for (var i = 0; i < audios.length; i++) {
                        audios[i].muted = true;
                        audios[i].volume = 0;
                    }
                })();
            ";
            await _webViewController.CoreWebView2.ExecuteScriptAsync(muteScript);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to execute mute script: {ex}");
        }
    }

    private void ShowError()
    {
        if (_loadingPanel != null)
        {
            _loadingPanel.IsVisible = false;
        }

        if (_errorPanel != null)
        {
            _errorPanel.IsVisible = true;
        }

        if (_browserContainer != null)
        {
            _browserContainer.IsVisible = false;
        }

        if (_webViewController != null)
        {
            _webViewController.IsVisible = false;
        }
    }
}
