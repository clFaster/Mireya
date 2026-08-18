using Avalonia;
using Avalonia.Headless;

namespace Mireya.Client.Core.Tests;

/// <summary>
///     Entry point for the headless Avalonia application used by the tests. The view models
///     rely on <see cref="Avalonia.Threading.Dispatcher" /> and on Avalonia's bitmap loading,
///     both of which need an initialized platform. The headless drawing backend keeps the
///     tests free of native graphics dependencies so they also run on CI.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
