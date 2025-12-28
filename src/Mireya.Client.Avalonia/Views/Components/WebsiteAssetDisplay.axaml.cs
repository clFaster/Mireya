using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Web.WebView2.Core;

namespace Mireya.Client.Avalonia.Views.Components;

public partial class WebsiteAssetDisplay : UserControl
{
    private Grid? _browserContainer;
    private StackPanel? _errorPanel;
    private CoreWebView2Environment? _webViewEnvironment;
    private CoreWebView2Controller? _webViewController;

    public WebsiteAssetDisplay()
    {
        InitializeComponent();
        InitializeWebView();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _browserContainer = this.FindControl<Grid>("BrowserContainer");
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
                    _webViewController = await _webViewEnvironment.CreateCoreWebView2ControllerAsync(hwnd);
                    
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

                    _browserContainer.IsVisible = true;
                    _errorPanel!.IsVisible = false;

                    System.Diagnostics.Debug.WriteLine("WebView2 controller created and initialized");
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
            // Set the bounds to fill the container
            // WebView2 controller manages its own bounds based on parent window size
            var bounds = _browserContainer.Bounds;
            System.Diagnostics.Debug.WriteLine($"Container bounds: {bounds.Width}x{bounds.Height}");
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
        if (uri == null || _webViewController?.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"Navigate called for URL: {uri.AbsoluteUri}");
            _browserContainer!.IsVisible = true;
            _errorPanel!.IsVisible = false;

            // Navigate to the URL
            _webViewController.CoreWebView2.Navigate(uri.AbsoluteUri);

            // Inject script to mute audio/video
            _webViewController.CoreWebView2.NavigationCompleted += async (_, __) =>
            {
                try
                {
                    var muteScript = @"
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
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to navigate to {uri}: {ex}");
            ShowError();
        }
    }

    private void ShowError()
    {
        if (_errorPanel != null)
        {
            _errorPanel.IsVisible = true;
        }

        if (_browserContainer != null)
        {
            _browserContainer.IsVisible = false;
        }
    }
}

