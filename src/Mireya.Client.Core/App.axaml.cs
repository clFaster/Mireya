using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.Services;
using Mireya.Client.Avalonia.ViewModels;
using Mireya.Client.Avalonia.Views;
using Serilog;

namespace Mireya.Client.Avalonia;

public class App : Application
{
    /// <summary>
    ///     Default backend URL used for unattended/kiosk deployments when neither the
    ///     <c>MIREYA_BACKEND_URL</c> environment variable nor a stored URL is present.
    /// </summary>
    public static string DefaultBackendUrl =>
        new UriBuilder(Uri.UriSchemeHttp, "localhost", 5000).Uri.GetLeftPart(UriPartial.Authority);

    /// <summary>
    ///     The composition root, supplied by the active platform head (Desktop, Android, …).
    ///     It must be assigned before the Avalonia application starts so that
    ///     <see cref="OnFrameworkInitializationCompleted" /> can build the service provider
    ///     with the correct platform-specific implementations.
    /// </summary>
    public static Func<IServiceProvider>? ServiceProviderFactory { get; set; }

    /// <summary>
    ///     The application-wide service provider, available to views that cannot receive
    ///     their dependencies through the constructor (e.g. controls created by XAML).
    /// </summary>
    public static IServiceProvider? Services { get; private set; }

    /// <summary>
    ///     The active shared root view model. Platform hosts use this only to translate
    ///     native remote, Back, and touch input into shared Screen Info navigation.
    /// </summary>
    public static MainWindowViewModel? RootViewModel { get; private set; }

    public override void Initialize()
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        Log.Information("Application starting...");

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Build the composition root, run migrations and load settings. This shared
        // startup is identical for every platform head (Desktop, Android, …); only the
        // way the resulting ViewModel is presented (Window vs single MainView) differs.
        var (serviceProvider, appSettings, mainViewModel) = InitializeCore();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            ConfigureDesktopLifetime(desktop, serviceProvider, appSettings, mainViewModel);
        else if (ApplicationLifetime is IActivityApplicationLifetime activity)
            activity.MainViewFactory = () => new MainView { DataContext = mainViewModel };
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            singleView.MainView = new MainView { DataContext = mainViewModel };

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureDesktopLifetime(
        IClassicDesktopStyleApplicationLifetime desktop,
        IServiceProvider serviceProvider,
        AppSettings appSettings,
        MainWindowViewModel mainViewModel
    )
    {
        // Ensure the application shuts down when the main window is closed
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Create main window with dependency-injected ViewModel
        var mainWindow = new MainWindow { DataContext = mainViewModel };
        desktop.MainWindow = mainWindow;

        if (appSettings.Fullscreen)
            mainWindow.WindowState = WindowState.FullScreen;

        appSettings.ApplyFullscreen = fullscreen =>
            mainWindow.WindowState = fullscreen ? WindowState.FullScreen : WindowState.Normal;

        desktop.Exit += (_, _) => ShutdownServices(serviceProvider);
    }

    private static void ShutdownServices(IServiceProvider serviceProvider)
    {
        Log.Information("Application exiting, shutting down services...");

        try
        {
            var hubService = serviceProvider.GetRequiredService<IScreenHubService>();
            hubService.DisconnectAsync().Wait(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error disconnecting SignalR during shutdown");
        }

        try
        {
            if (serviceProvider is IAsyncDisposable asyncDisposable)
                asyncDisposable.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3));
            else if (serviceProvider is IDisposable disposable)
                disposable.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error disposing services during shutdown");
        }

        Log.Information("Shutdown complete");
        Log.CloseAndFlush();
    }

    /// <summary>
    ///     Shared, platform-agnostic startup: build the composition root supplied by the
    ///     active platform head, apply database migrations, and load persisted settings.
    ///     Returns the service provider together with the loaded settings and the root
    ///     ViewModel so each lifetime branch can present it appropriately.
    /// </summary>
    private static (
        IServiceProvider Services,
        AppSettings Settings,
        MainWindowViewModel ViewModel
    ) InitializeCore()
    {
        // Setup dependency injection (AppSettings singleton registered inside).
        // The platform head provides the composition root so that platform-only
        // implementations (credential storage, asset renderers, …) are wired in.
        var serviceProvider =
            ServiceProviderFactory?.Invoke()
            ?? throw new InvalidOperationException(
                "App.ServiceProviderFactory must be set by the platform head before startup."
            );
        Services = serviceProvider;

        // Apply database migrations and load settings in the same startup scope
        Log.Information("Initializing database and applying migrations...");
        using (var startupScope = serviceProvider.CreateScope())
        {
            var db = startupScope.ServiceProvider.GetRequiredService<LocalDbContext>();
            try
            {
                db.Database.Migrate();
                Log.Information("Database migrations applied successfully");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to apply database migrations. See inner exception for details.",
                    ex
                );
            }
        }

        // Load app settings from DB now that migrations have run.
        // GetAwaiter().GetResult() is intentional: startup must be synchronous
        // so that Fullscreen/AutoStart are available when the root view is created.
        var appSettings = serviceProvider.GetRequiredService<AppSettings>();
        appSettings.LoadAsync().GetAwaiter().GetResult();
        Log.Information(
            "App settings loaded — Fullscreen={Fullscreen}, AutoStart={AutoStart}",
            appSettings.Fullscreen,
            appSettings.AutoStart
        );

        var mainViewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
        RootViewModel = mainViewModel;
        return (serviceProvider, appSettings, mainViewModel);
    }
}
