using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Mireya.ApiClient.Options;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
///     Service to manage API client configuration
/// </summary>
public interface IApiClientConfiguration
{
    /// <summary>
    ///     Update the base URL for the API client
    /// </summary>
    Task UpdateBaseUrlAsync(string baseUrl);

    /// <summary>
    ///     Get the current base URL
    /// </summary>
    string GetBaseUrl();
}

/// <summary>
///     Implementation of API client configuration service
/// </summary>
public class ApiClientConfiguration(IOptions<MireyaApiClientOptions> options)
    : IApiClientConfiguration
{
    public Task UpdateBaseUrlAsync(string baseUrl)
    {
        options.Value.BaseUrl = baseUrl;
        return Task.CompletedTask;
    }

    public string GetBaseUrl()
    {
        return options.Value.BaseUrl;
    }
}
