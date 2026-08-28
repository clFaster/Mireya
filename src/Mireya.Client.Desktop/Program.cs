using System;
using Avalonia;
using Mireya.Client.Avalonia.Platform;

namespace Mireya.Client.Avalonia;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Supply the desktop composition root before the Avalonia application starts so
        // App can build the service provider with the desktop-specific implementations.
        App.ServiceProviderFactory = DesktopServices.Build;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
    }
}
