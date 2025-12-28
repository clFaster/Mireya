using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Options;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.Data;
using Mireya.Client.Avalonia.Services;
using Mireya.Client.Avalonia.ViewModels;
using Mireya.Client.Avalonia.Views;

namespace Mireya.Client.Avalonia;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    public override void Initialize()
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Application starting...");
        
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            
            // Setup dependency injection
            _serviceProvider = ConfigureServices();
            
            // Create main window with dependency-injected ViewModel
            var mainViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        
        // Add Serilog logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: true);
        });
        
        // Configure API client options (will be set by user via settings)
        services.Configure<MireyaApiClientOptions>(options =>
        {
            options.BaseUrl = "http://localhost:5000"; // Default, will be overridden by settings
        });
        
        // Configure local SQLite database
        var appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        var dbPath = System.IO.Path.Combine(appDataPath, "Mireya", "mireya_client.db");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dbPath)!);
        
        services.AddDbContext<LocalDbContext>(options =>
        {
            options.UseSqlite($"Data Source={dbPath}");
        });
        
        // Register platform-specific services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ICredentialStorage, AvaloniaCredentialStorage>();
        services.AddSingleton<IApiClientConfiguration, ApiClientConfiguration>();
        
        // Register backend and credential management services
        services.AddSingleton<IBackendManager, BackendManager>();
        services.AddSingleton<ICredentialManager, CredentialManager>();
        
        // Register token provider (singleton to share state)
        services.AddSingleton<IAccessTokenProvider, AccessTokenProvider>();
        
        // Register authentication service
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        
        // Register the authentication handler
        services.AddTransient<AuthenticationHandler>();
        
        // Register HttpClient factory with authentication handler
        services.AddHttpClient("MireyaApiClient", (sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MireyaApiClientOptions>>();
            client.BaseAddress = new System.Uri(options.Value.BaseUrl);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        })
        .AddHttpMessageHandler<AuthenticationHandler>();
        
        // Register MireyaApiClient
        services.AddTransient<IMireyaApiClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>();
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MireyaApiClientOptions>>();
            var httpClient = httpClientFactory.CreateClient("MireyaApiClient");
            return new MireyaApiClient(options.Value.BaseUrl, httpClient);
        });
        
        // Register SignalR Hub Service
        services.AddSingleton<IScreenHubService, ScreenHubService>();
        
        // Register Asset Sync Service with local database
        services.AddScoped<ILocalAssetSyncService, LocalAssetSyncService>();
        
        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ClientStatusViewModel>();
        services.AddTransient<BackendSelectionViewModel>();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Apply database migrations automatically at startup
        Log.Information("Initializing database and applying migrations...");
        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
            
            try
            {
                // Apply all pending migrations automatically
                db.Database.Migrate();
                Log.Information("Database migrations applied successfully");
            }
            catch (System.Exception ex)
            {
                Log.Fatal(ex, "Failed to apply database migrations");
                throw;
            }
        }
        
        return serviceProvider;
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}