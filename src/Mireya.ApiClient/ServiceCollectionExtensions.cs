using Microsoft.Extensions.DependencyInjection;

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
    /// <param name="configure">Configuration action for the client options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMireyaApiClient(
        this IServiceCollection services,
        Action<MireyaApiClientOptions> configure)
    {
        
        return services;
    }

    /// <summary>
    /// Adds Mireya API client services with a simple base URL configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="baseUrl">The base URL of the Mireya API</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMireyaApiClient(
        this IServiceCollection services,
        string baseUrl)
    {
        return services.AddMireyaApiClient(options =>
        {
            options.BaseUrl = baseUrl;
        });
    }
}