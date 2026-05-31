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
    public const string DefaultBackendUrl = "http://localhost:5000";

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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Ensure the application shuts down when the main window is closed
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // Setup dependency injection (AppSettings singleton registered inside).
            // The platform head provides the composition root so that platform-only
            // implementations (credential storage, asset renderers, …) are wired in.
            var serviceProvider = ServiceProviderFactory?.Invoke()
                ?? throw new InvalidOperationException(
                    "App.ServiceProviderFactory must be set by the platform head before startup.");
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
                        "Failed to apply database migrations. See inner exception for details.", ex);
                }
            }

            // Load app settings from DB now that migrations have run.
            // GetAwaiter().GetResult() is intentional: startup must be synchronous
            // so that Fullscreen/AutoStart are available when the main window is created.
            var appSettings = serviceProvider.GetRequiredService<AppSettings>();
            appSettings.LoadAsync().GetAwaiter().GetResult();
            Log.Information(
                "App settings loaded — Fullscreen={Fullscreen}, AutoStart={AutoStart}, HideScreenInfo={HideScreenInfo}",
                appSettings.Fullscreen, appSettings.AutoStart, appSettings.HideScreenInfo
            );

            // Create main window with dependency-injected ViewModel
            var mainViewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow { DataContext = mainViewModel };
            desktop.MainWindow = mainWindow;

            // Apply fullscreen / kiosk mode if configured
            if (appSettings.Fullscreen)
            {
                mainWindow.WindowState = WindowState.FullScreen;
            }

            // Wire the immediate-apply callback so the settings UI can toggle
            // fullscreen without a restart.  The callback is always invoked from the
            // UI thread (RelayCommand preserves the Avalonia SynchronizationContext).
            appSettings.ApplyFullscreen = fullscreen =>
                mainWindow.WindowState = fullscreen
                    ? WindowState.FullScreen
                    : WindowState.Normal;

            // Gracefully shut down services when the application exits:
            // 1. Disconnect SignalR (stops auto-reconnect background threads)
            // 2. Dispose the DI container (disposes all singletons/scoped services)
            desktop.Exit += (_, _) =>
            {
                Log.Information("Application exiting, shutting down services...");

                try
                {
                    // Disconnect SignalR first to stop auto-reconnect threads.
                    // Use a timeout to prevent hanging on unresponsive connections.
                    var hubService = serviceProvider.GetRequiredService<IScreenHubService>();
                    hubService.DisconnectAsync()
                        .Wait(TimeSpan.FromSeconds(3));
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error disconnecting SignalR during shutdown");
                }

                try
                {
                    // Dispose the service provider with a timeout to prevent hanging
                    if (serviceProvider is IAsyncDisposable asyncDisposable)
                    {
                        asyncDisposable.DisposeAsync().AsTask()
                            .Wait(TimeSpan.FromSeconds(3));
                    }
                    else if (serviceProvider is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error disposing services during shutdown");
                }

                Log.Information("Shutdown complete");
                Log.CloseAndFlush();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}