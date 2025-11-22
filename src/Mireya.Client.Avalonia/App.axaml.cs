using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Options;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.Services;
using Mireya.Client.Avalonia.ViewModels;
using Mireya.Client.Avalonia.Views;

namespace Mireya.Client.Avalonia;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    public override void Initialize()
    {
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
        
        // Configure API client options (will be set by user via settings)
        services.Configure<MireyaApiClientOptions>(options =>
        {
            options.BaseUrl = "http://localhost:5000"; // Default, will be overridden by settings
        });
        
        // Register platform-specific services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ICredentialStorage, AvaloniaCredentialStorage>();
        services.AddSingleton<IApiClientConfiguration, ApiClientConfiguration>();
        
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
        
        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();
        
        return services.BuildServiceProvider();
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