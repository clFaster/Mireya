using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace Mireya.Client.Avalonia.AndroidTv;

/// <summary>
///     Android application entry point (Avalonia 12 hosting model). It names the shared <see cref="App" /> as the Avalonia
///     application and supplies the Android
///     composition root before Avalonia starts, so the activity lifetime can build the
///     service provider with the Android-specific implementations.
/// </summary>
[Application]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer) { }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Supply the Android composition root before the Avalonia application starts so
        // App can build the service provider with the Android-specific implementations.
        App.ServiceProviderFactory = AndroidServices.Build;

        return base.CustomizeAppBuilder(builder).WithInterFont();
    }
}
