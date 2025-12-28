using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Options;
using Mireya.ApiClient.Services;

namespace Mireya.ApiClient;

/// <summary>
/// Extension methods for registering Mireya API client services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Mireya API client services to the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMireyaApiClient(
        this IServiceCollection services)
    {
        var httpClientBuilder = services.AddHttpClient("UpdateServerApiClient");

        httpClientBuilder
            .ConfigureHttpClient((sp, httpClient) =>
            {
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip");
            });
        
        services.AddTransient<IMireyaApiClient, MireyaApiClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var opts = sp.GetRequiredService<IOptions<MireyaApiClientOptions>>();
            return new MireyaApiClient(opts.Value.BaseUrl, factory.CreateClient("MireyaApiClient"));
        });
        
        services.AddTransient<IMireyaService, MireyaService>();
        services.AddSingleton<IAssetSyncService, AssetSyncService>();
        
        return services;
    }
}