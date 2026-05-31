using System;
using Android.Webkit;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Platform;
using Mireya.Client.Avalonia.Platform;
using AView = Android.Views.View;

namespace Mireya.Client.Avalonia.AndroidTv.Views.Components;

/// <summary>
///     Android implementation of <see cref="IWebsiteRenderer" />. Hosts a native
///     <see cref="WebView" /> through Avalonia's <see cref="NativeControlHost" /> and
///     applies the same "signage" hardening as the desktop WebView2 renderer: media is
///     muted, the page is made non-interactive and all scrollbars are hidden.
/// </summary>
public sealed class AndroidWebsiteAssetDisplay : NativeControlHost, IWebsiteRenderer
{
    /// <summary>Raised once a navigated page has finished loading (see <see cref="IWebsiteRenderer" />).</summary>
    public event Action? ContentReady;

    private WebView? _webView;
    private Uri? _pendingUri;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = global::Android.App.Application.Context;

        _webView = new WebView(context);
        var settings = _webView.Settings;
        settings.JavaScriptEnabled = true;
        settings.DomStorageEnabled = true;
        settings.MediaPlaybackRequiresUserGesture = false;
        settings.LoadWithOverviewMode = true;
        settings.UseWideViewPort = true;
        settings.BuiltInZoomControls = false;
        settings.DisplayZoomControls = false;

        _webView.HorizontalScrollBarEnabled = false;
        _webView.VerticalScrollBarEnabled = false;
        _webView.SetBackgroundColor(global::Android.Graphics.Color.Black);
        _webView.SetWebViewClient(new SignageWebViewClient(this));

        // Apply any navigation that was requested before the native control existed.
        if (_pendingUri != null)
            _webView.LoadUrl(_pendingUri.AbsoluteUri);

        return new AndroidViewControlHandle(_webView);
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        if (_webView != null)
        {
            _webView.StopLoading();
            _webView.SetWebViewClient(new WebViewClient());
            _webView.Destroy();
            _webView.Dispose();
            _webView = null;
        }

        base.DestroyNativeControlCore(control);
    }

    public void Navigate(Uri? uri)
    {
        _pendingUri = uri;

        if (_webView == null)
            return;

        _webView.LoadUrl(uri?.AbsoluteUri ?? "about:blank");
    }

    private void RaiseContentReady() => ContentReady?.Invoke();

    /// <summary>
    ///     WebView client that hardens the loaded page for unattended signage and notifies
    ///     the transition layer once the page has finished loading.
    /// </summary>
    private sealed class SignageWebViewClient : WebViewClient
    {
        private const string HardenScript =
            "javascript:(function(){" +
            "document.querySelectorAll('video,audio').forEach(function(e){e.muted=true;e.volume=0;});" +
            "var s=document.createElement('style');" +
            "s.textContent='*{pointer-events:none!important;user-select:none!important;}" +
            "html,body{overflow:hidden!important;scrollbar-width:none!important;}" +
            "::-webkit-scrollbar{display:none!important;width:0!important;height:0!important;}';" +
            "document.head.appendChild(s);})()";

        private readonly AndroidWebsiteAssetDisplay _owner;

        public SignageWebViewClient(AndroidWebsiteAssetDisplay owner) => _owner = owner;

        public override void OnPageFinished(WebView? view, string? url)
        {
            base.OnPageFinished(view, url);

            view?.EvaluateJavascript(HardenScript, null);

            // Signal the transition layer that the page is loaded so the crossfade
            // curtain can be lifted without revealing a loading flash.
            _owner.RaiseContentReady();
        }
    }
}
