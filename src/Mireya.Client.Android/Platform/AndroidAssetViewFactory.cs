using Avalonia.Controls;
using Mireya.Client.Avalonia.Platform;
using Mireya.Client.Avalonia.AndroidTv.Views.Components;

namespace Mireya.Client.Avalonia.AndroidTv.Platform;

/// <summary>
///     Android implementation of <see cref="IAssetViewFactory" />. Provides the
///     System WebView-based website renderer and the libVLC-based video renderer used
///     on Android TV. Both controls are <see cref="NativeControlHost" />s that embed a
///     native Android <c>View</c> into the Avalonia visual tree.
/// </summary>
public sealed class AndroidAssetViewFactory : IAssetViewFactory
{
    public Control CreateWebsiteRenderer() => new AndroidWebsiteAssetDisplay();

    public Control CreateVideoRenderer() => new AndroidVideoAssetDisplay();
}
