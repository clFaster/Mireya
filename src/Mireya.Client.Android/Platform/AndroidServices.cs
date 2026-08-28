using System;

namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     Composition root for the Android TV head. Wires the shared services from
///     <c>Mireya.Client.Core</c> together with the Android-specific implementations
///     (System WebView / Media3 asset renderers) and reuses the platform-neutral
///     credential/settings storage and the local SQLite store from Core.
/// </summary>
public static class AndroidServices
{
    public static IServiceProvider Build()
    {
        return DisplayClientServiceProviderFactory.Build<AndroidAssetViewFactory>(
            App.DefaultBackendUrl,
            false
        );
    }
}
