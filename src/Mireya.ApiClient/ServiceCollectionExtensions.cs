using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Options;
using Mireya.ApiClient.Services;

namespace Mireya.ApiClient;

/// <summary>
///     Extension methods for registering Mireya API client services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds Mireya API client services to the dependency injection container.
    ///     This registers all API-communication services; platform-specific implementations
    ///     of ICredentialStorage and ISettingsService must be registered by the host.
    /// </summary>
    public static IServiceCollection AddMireyaApiClient(this IServiceCollection services)
    {
        // Core token + credential management
        services.AddSingleton<IAccessTokenProvider, AccessTokenProvider>();
        services.AddSingleton<ICredentialManager, CredentialManager>();
        services.AddSingleton<ICredentialRepository, CredentialRepository>();
        services.AddSingleton<IBackendManager, BackendManager>();
        services.AddSingleton<IApiClientConfiguration, ApiClientConfiguration>();

        // HTTP authentication handler
        services.AddTransient<AuthenticationHandler>();

        // Named HttpClient with Bearer token handler
        services
            .AddHttpClient(
                "MireyaApiClient",
                (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<MireyaApiClientOptions>>();
                    client.BaseAddress = new Uri(options.Value.BaseUrl);
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
                }
            )
            .AddHttpMessageHandler<AuthenticationHandler>();

        // NSwag-generated API client
        services.AddTransient<IMireyaApiClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var options = sp.GetRequiredService<IOptions<MireyaApiClientOptions>>();
            var httpClient = httpClientFactory.CreateClient("MireyaApiClient");
            return new MireyaApiClient(options.Value.BaseUrl, httpClient);
        });

        // Authentication service
        services.AddSingleton<IAuthenticationService, AuthenticationService>();

        // SignalR hub
        services.AddSingleton<IScreenHubService, ScreenHubService>();

        // Asset sync
        services.AddScoped<ILocalAssetSyncService, LocalAssetSyncService>();

        return services;
    }
}
