using Avalonia.Controls;

namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     Creates the platform-specific asset renderer controls. Each platform head
///     (Desktop, Android, …) registers an implementation in the dependency-injection
///     container so the shared <c>ContentDisplayView</c> can host the right controls
///     without referencing platform-only packages (WebView2, LibVLC, …).
/// </summary>
public interface IAssetViewFactory
{
    /// <summary>
    ///     Create the control that renders website assets. The returned control also
    ///     implements <see cref="IWebsiteRenderer" />.
    /// </summary>
    Control CreateWebsiteRenderer();

    /// <summary>
    ///     Create the control that renders video assets. The returned control also
    ///     implements <see cref="IVideoRenderer" />.
    /// </summary>
    Control CreateVideoRenderer();
}
