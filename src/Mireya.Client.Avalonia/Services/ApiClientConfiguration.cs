using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Mireya.ApiClient.Options;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
/// Service to manage API client configuration
/// </summary>
public interface IApiClientConfiguration
{
    /// <summary>
    /// Update the base URL for the API client
    /// </summary>
    Task UpdateBaseUrlAsync(string baseUrl);
    
    /// <summary>
    /// Get the current base URL
    /// </summary>
    string GetBaseUrl();
}

/// <summary>
/// Implementation of API client configuration service
/// </summary>
public class ApiClientConfiguration : IApiClientConfiguration
{
    private readonly IOptions<MireyaApiClientOptions> _options;

    public ApiClientConfiguration(IOptions<MireyaApiClientOptions> options)
    {
        _options = options;
    }

    public Task UpdateBaseUrlAsync(string baseUrl)
    {
        _options.Value.BaseUrl = baseUrl;
        return Task.CompletedTask;
    }

    public string GetBaseUrl()
    {
        return _options.Value.BaseUrl;
    }
}
