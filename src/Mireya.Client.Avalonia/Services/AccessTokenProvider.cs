namespace Mireya.Client.Avalonia.Services;

/// <summary>
/// Simple token accessor to break circular dependency between AuthenticationHandler and AuthenticationService
/// </summary>
public interface IAccessTokenProvider
{
    /// <summary>
    /// Get the current access token
    /// </summary>
    string? GetAccessToken();
    
    /// <summary>
    /// Set the current access token
    /// </summary>
    void SetAccessToken(string? token);
}

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
