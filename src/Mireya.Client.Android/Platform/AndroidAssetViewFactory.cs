using Avalonia.Controls;
using Mireya.Client.Avalonia.Views.Components;

namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     Android implementation of <see cref="IAssetViewFactory" />. Provides the
///     System WebView-based website renderer and the Media3-based video renderer used
///     on Android TV. Both controls are <see cref="NativeControlHost" />s that embed a
///     native Android <c>View</c> into the Avalonia visual tree.
/// </summary>
public sealed class AndroidAssetViewFactory : IAssetViewFactory
{
    public Control CreateWebsiteRenderer()
    {
        return new AndroidWebsiteAssetDisplay();
    }

    public Control CreateVideoRenderer()
    {
        return new AndroidVideoAssetDisplay();
    }
}
