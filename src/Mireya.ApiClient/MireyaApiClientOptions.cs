namespace Mireya.ApiClient;

/// <summary>
/// Configuration options for the Mireya API client
/// </summary>
public class MireyaApiClientOptions
{
    /// <summary>
    /// Base URL of the Mireya API (e.g., "https://api.example.com")
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Whether to automatically refresh tokens when they expire
    /// Default: true
    /// </summary>
    public bool AutoRefreshTokens { get; set; } = true;

    /// <summary>
    /// Time buffer before token expiry to trigger refresh (in seconds)
    /// Default: 60 seconds (refresh 1 minute before expiry)
    /// </summary>
    public int RefreshBufferSeconds { get; set; } = 60;
}