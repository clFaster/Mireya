using System;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Options;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.Services;
using Mireya.Client.Avalonia.ViewModels;
using Mireya.Client.Avalonia.Views;
using Serilog;

namespace Mireya.Client.Avalonia;

public class App : Application
{
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

            // Setup dependency injection (AppSettings singleton registered inside)
            var serviceProvider = ConfigureServices();

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
                    serviceProvider.DisposeAsync().AsTask()
                        .Wait(TimeSpan.FromSeconds(3));
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

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Register app settings singleton — DI resolves IServiceScopeFactory automatically
        services.AddSingleton<AppSettings>();

        // Add Serilog logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, true);
        });

        // Configure API client options (will be set by user via settings)
        services.Configure<MireyaApiClientOptions>(options =>
        {
            options.BaseUrl = "http://localhost:5000"; // Default, will be overridden by settings
        });

        // Configure local SQLite database
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbPath = Path.Combine(appDataPath, "Mireya", "mireya_client.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddDbContext<LocalDbContext>(options => { options.UseSqlite($"Data Source={dbPath}"); });

        // Register platform-specific implementations (these must be registered
        // before AddMireyaApiClient which depends on them)
        services.AddSingleton<ICredentialStorage, AvaloniaCredentialStorage>();
        services.AddSingleton<ISettingsService, SettingsService>();

        // Register all API client services (token management, auth, SignalR, sync, etc.)
        services.AddMireyaApiClient();

        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ContentDisplayViewModel>();
        services.AddTransient<BackendSelectionViewModel>();

        var serviceProvider = services.BuildServiceProvider();

        return serviceProvider;
    }
}