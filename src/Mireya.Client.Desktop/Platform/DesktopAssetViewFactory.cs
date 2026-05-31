using Avalonia.Controls;
using Mireya.Client.Avalonia.Platform;
using Mireya.Client.Avalonia.Views.Components;

namespace Mireya.Client.Avalonia.Desktop;

/// <summary>
///     Desktop implementation of <see cref="IAssetViewFactory" />. Provides the
///     WebView2-based website renderer and the LibVLC-based video renderer used on
///     Windows and Linux.
/// </summary>
public sealed class DesktopAssetViewFactory : IAssetViewFactory
{
    public Control CreateWebsiteRenderer() => new WebsiteAssetDisplay();

    public Control CreateVideoRenderer() => new VideoAssetDisplay();
}
