using Mireya.ApiClient.Services;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
/// Thread-safe implementation of access token provider
/// </summary>
public class AccessTokenProvider : IAccessTokenProvider
{
    private string? _accessToken;
    private readonly object _lock = new();

    public string? GetAccessToken()
    {
        lock (_lock)
        {
            return _accessToken;
        }
    }

    public void SetAccessToken(string? token)
    {
        lock (_lock)
        {
            _accessToken = token;
        }
    }
}
