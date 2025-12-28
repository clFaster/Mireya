namespace Mireya.ApiClient.Options;

/// <summary>
///     Configuration options for the Mireya API client
/// </summary>
public class MireyaApiClientOptions
{
    /// <summary>
    ///     Base URL of the Mireya API (e.g., "https://api.example.com")
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
