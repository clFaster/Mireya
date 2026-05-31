using System;

namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     Abstraction for the control that renders website assets on a given platform
///     (WebView2 on Windows, CEF/WebView elsewhere). Implemented by a control supplied
///     through <see cref="IAssetViewFactory" />.
/// </summary>
public interface IWebsiteRenderer
{
    /// <summary>
    ///     Navigate the renderer to the supplied URI, or clear it when <paramref name="uri" />
    ///     is <c>null</c>.
    /// </summary>
    void Navigate(Uri? uri);

    /// <summary>
    ///     Raised once the most recently navigated page has finished loading and has been
    ///     painted (ready to be drawn). Used by the transition layer to know when it is safe
    ///     to reveal the website without showing a loading flash.
    /// </summary>
    event Action? ContentReady;
}
