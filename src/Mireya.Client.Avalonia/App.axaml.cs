using System;
using System.IO;
using Avalonia;
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
            // Setup dependency injection
            var serviceProvider = ConfigureServices();

            // Create main window with dependency-injected ViewModel
            var mainViewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainViewModel };

            // Ensure ServiceProvider is disposed when application exits
            desktop.Exit += (_, _) => serviceProvider.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

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

        // Apply database migrations automatically at startup
        Log.Information("Initializing database and applying migrations...");
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

        try
        {
            // Apply all pending migrations automatically
            db.Database.Migrate();
            Log.Information("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to apply database migrations. See inner exception for details.",
                ex);
        }

        return serviceProvider;
    }
}