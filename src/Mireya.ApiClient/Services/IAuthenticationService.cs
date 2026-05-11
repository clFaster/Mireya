using Mireya.ApiClient.Data;
using Mireya.ApiClient.Models;

namespace Mireya.ApiClient.Services;

/// <summary>
///     Authentication state for screen clients
/// </summary>
public enum AuthenticationState
{
    NotRegistered,
    NotAuthenticated,
    Authenticated,
    Failed,
}

public record RegisterResult(bool Success, string? ScreenIdentifier, string? UserId, string? ErrorMessage);
public record LoginResult(bool Success, string? AccessToken, string? ErrorMessage);
public record ScreenInfo(string ScreenIdentifier, string ScreenName, string? Description, string? ApprovalStatus);

/// <summary>
///     Authentication service interface for screen clients
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    ///     Check the current authentication state
    /// </summary>
    Task<AuthenticationState> GetAuthenticationStateAsync();

    /// <summary>
    ///     Register the screen with the backend
    /// </summary>
    Task<RegisterResult> RegisterAsync(string? deviceName = null);

    /// <summary>
    ///     Login to the backend using stored credentials
    /// </summary>
    Task<LoginResult> LoginAsync();

    /// <summary>
    ///     Get information about the current screen
    /// </summary>
    Task<ScreenInfo?> GetScreenInfoAsync();

    /// <summary>
    ///     Logout and clear stored credentials
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    ///     Get the current access token
    /// </summary>
    string? GetAccessToken();
}
