using System.Threading.Tasks;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
///     Authentication state enumeration
/// </summary>
public enum AuthenticationState
{
    /// <summary>No credentials found, need to register</summary>
    NotRegistered,

    /// <summary>Credentials found but not authenticated</summary>
    NotAuthenticated,

    /// <summary>Successfully authenticated with valid token</summary>
    Authenticated,

    /// <summary>Authentication failed or token expired</summary>
    Failed,
}

/// <summary>
///     Result of a registration attempt
/// </summary>
public record RegisterResult(
    bool Success,
    string? ScreenIdentifier,
    string? UserId,
    string? ErrorMessage
);

/// <summary>
///     Result of a login attempt
/// </summary>
public record LoginResult(bool Success, string? AccessToken, string? ErrorMessage);

/// <summary>
///     Screen information from Bonjour endpoint
/// </summary>
public record ScreenInfo(
    string ScreenIdentifier,
    string ScreenName,
    string? Description,
    string ApprovalStatus
);

/// <summary>
///     Service for managing screen authentication
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    ///     Check current authentication state
    /// </summary>
    Task<AuthenticationState> GetAuthenticationStateAsync();

    /// <summary>
    ///     Register a new screen with the backend
    /// </summary>
    Task<RegisterResult> RegisterAsync(string? deviceName = null);

    /// <summary>
    ///     Login with stored credentials
    /// </summary>
    Task<LoginResult> LoginAsync();

    /// <summary>
    ///     Fetch screen information from the backend
    /// </summary>
    Task<ScreenInfo?> GetScreenInfoAsync();

    /// <summary>
    ///     Logout and clear stored credentials
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    ///     Get the current access token if authenticated
    /// </summary>
    string? GetAccessToken();
}
