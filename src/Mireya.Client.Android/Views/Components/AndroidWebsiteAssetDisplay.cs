using System;
using Android.App;
using Android.Graphics;
using Android.Webkit;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Platform;
using Mireya.Client.Avalonia.Platform;

namespace Mireya.Client.Avalonia.AndroidTv.Views.Components;

/// <summary>
///     Android implementation of <see cref="IWebsiteRenderer" />. Hosts a native
///     <see cref="WebView" /> through Avalonia's <see cref="NativeControlHost" /> and
///     applies the same "signage" hardening as the desktop WebView2 renderer: media is
///     muted, the page is made non-interactive and all scrollbars are hidden.
/// </summary>
public sealed class AndroidWebsiteAssetDisplay : NativeControlHost, IWebsiteRenderer
{
    private Uri? _pendingUri;
    private WebView? _webView;

    public void Navigate(Uri? uri)
    {
        _pendingUri = uri;

        if (_webView == null)
            return;

        _webView.LoadUrl(uri?.AbsoluteUri ?? "about:blank");
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = Application.Context;

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
        _webView.SetBackgroundColor(Color.Black);
        _webView.SetWebViewClient(new SignageWebViewClient());

        // Apply any navigation that was requested before the native control existed.
        if (_pendingUri != null)
            _webView.LoadUrl(_pendingUri.AbsoluteUri);

        return new AndroidViewControlHandle(_webView);
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        // Avalonia can dispose the JNI peer before this callback runs. Detach our
        // reference first so repeated or re-entrant teardown never reuses that peer.
        var webView = _webView;
        _webView = null;

        try
        {
            try
            {
                if (webView?.PeerReference.IsValid ?? false)
                {
                    webView.StopLoading();
                    webView.SetWebViewClient(new WebViewClient());
                    webView.Destroy();
                }
            }
            catch (ObjectDisposedException)
            {
                // The native-control handle may already have disposed the managed
                // wrapper. In that case there is no remaining WebView work to do.
            }
        }
        finally
        {
            // AndroidViewControlHandle owns disposal of the wrapped Android View.
            base.DestroyNativeControlCore(control);
        }
    }

    /// <summary>
    ///     WebView client that hardens the loaded page for unattended signage once it has
    ///     finished loading.
    /// </summary>
    private sealed class SignageWebViewClient : WebViewClient
    {
        private const string HardenScript =
            "javascript:(function(){"
            + "document.querySelectorAll('video,audio').forEach(function(e){e.muted=true;e.volume=0;});"
            + "var s=document.createElement('style');"
            + "s.textContent='*{pointer-events:none!important;user-select:none!important;}"
            + "html,body{overflow:hidden!important;scrollbar-width:none!important;}"
            + "::-webkit-scrollbar{display:none!important;width:0!important;height:0!important;}';"
            + "document.head.appendChild(s);})()";

        public override void OnPageFinished(WebView? view, string? url)
        {
            base.OnPageFinished(view, url);

            view?.EvaluateJavascript(HardenScript, null);
        }
    }
}
