namespace Mireya.ApiClient.Models;

/// <summary>
/// Authentication state of the client
/// </summary>
public enum AuthenticationState
{
    /// <summary>
    /// No credentials exist - needs to register
    /// </summary>
    NotRegistered,

    /// <summary>
    /// Has credentials stored but not currently logged in
    /// </summary>
    Registered,

    /// <summary>
    /// Logged in with valid token
    /// </summary>
    Authenticated
}
