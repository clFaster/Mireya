using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Models;

namespace Mireya.ApiClient.Services.Authentication;

/// <summary>
/// Service for managing screen authentication with the Mireya API
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Get the current authentication state
    /// </summary>
    Task<AuthenticationState> GetStateAsync();

    /// <summary>
    /// Register a new screen with the API (first-time setup)
    /// Generates credentials, registers with API, and logs in automatically
    /// </summary>
    Task<RegisterResult> RegisterAsync(string? deviceName = null, int? resolutionWidth = null, int? resolutionHeight = null);

    /// <summary>
    /// Login with stored credentials
    /// </summary>
    Task<LoginResult> LoginAsync();

    /// <summary>
    /// Get screen data from the Bonjour endpoint (requires authentication)
    /// </summary>
    Task<BonjourResponse> GetBonjourDataAsync();

    /// <summary>
    /// Logout and clear stored tokens (credentials remain for re-login)
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// Clear all credentials and tokens (requires re-registration)
    /// </summary>
    Task ResetAsync();

    /// <summary>
    /// Get the current access token (if authenticated)
    /// </summary>
    string? GetAccessToken();
}
