using System;

namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     Composition root for the Windows/Linux desktop head. Wires the shared services
///     from <c>Mireya.Client.Core</c> together with the desktop-specific implementations
///     (DPAPI credential storage, WebView2 / LibVLC asset renderers, local SQLite store).
/// </summary>
public static class DesktopServices
{
    public static IServiceProvider Build()
    {
        return DisplayClientServiceProviderFactory.Build<DesktopAssetViewFactory>(
            App.DefaultBackendUrl,
            new ClientPlatformCapabilities
            {
                SupportsFullscreen = true,
                FormFactor = FormFactor.Desktop,
            }
        );
    }
}
