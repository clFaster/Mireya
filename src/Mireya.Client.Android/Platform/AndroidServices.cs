using System;
using Android.Content.Res;
using Microsoft.Extensions.DependencyInjection;

namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     Composition root for the Android TV head. Wires the shared services from
///     <c>Mireya.Client.Core</c> together with the Android-specific implementations
///     (System WebView / Media3 asset renderers) and reuses the platform-neutral
///     credential/settings storage and the local SQLite store from Core.
/// </summary>
public static class AndroidServices
{
    internal static AndroidDisplayPresentationController PresentationController { get; private set; } =
        new(FormFactor.Phone);

    public static IServiceProvider Build()
    {
        var configuration = global::Android.App.Application.Context.Resources?.Configuration;
        var uiMode = configuration?.UiMode ?? UiMode.TypeNormal;
        var formFactor =
            (uiMode & UiMode.TypeMask) == UiMode.TypeTelevision
                ? FormFactor.Tv
                : configuration?.SmallestScreenWidthDp >= 600
                    ? FormFactor.Tablet
                    : FormFactor.Phone;

        PresentationController = new AndroidDisplayPresentationController(formFactor);

        return DisplayClientServiceProviderFactory.Build<AndroidAssetViewFactory>(
            App.DefaultBackendUrl,
            new ClientPlatformCapabilities
            {
                SupportsFullscreen = false,
                FormFactor = formFactor,
            },
            services =>
                services.AddSingleton<IDisplayPresentationController>(PresentationController)
        );
    }
}
