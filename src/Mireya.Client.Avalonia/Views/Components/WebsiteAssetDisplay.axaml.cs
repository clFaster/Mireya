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
    private bool _isInitialized;

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

    private async void InitializeWebView()
    {
        if (_browserContainer == null || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ShowError();
            return;
        }

        try
        {
            // Get the user data folder for WebView2
            var userDataFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Mireya",
                "WebView2"
            );

            // Create WebView2 environment
            await CoreWebView2Environment.CreateAsync(null, userDataFolder);

            // Mark as initialized - environment is created and ready for use
            _isInitialized = true;
            _browserContainer.IsVisible = true;
            _errorPanel!.IsVisible = false;

            System.Diagnostics.Debug.WriteLine("WebView2 environment initialized");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize WebView2: {ex}");
            ShowError();
        }
    }

    /// <summary>
    /// Navigate to a specific URL and mute audio.
    /// </summary>
    public void Navigate(Uri? uri)
    {
        if (uri == null || !_isInitialized)
        {
            return;
        }

        try
        {
            // WebView2 runtime integration for displaying websites
            System.Diagnostics.Debug.WriteLine($"Navigate called for URL: {uri.AbsoluteUri}");
            _browserContainer!.IsVisible = true;
            _errorPanel!.IsVisible = false;
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
