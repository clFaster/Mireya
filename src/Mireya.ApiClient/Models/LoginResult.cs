namespace Mireya.ApiClient.Models;

/// <summary>
/// Result of a login attempt
/// </summary>
public class LoginResult
{
    public required bool Success { get; init; }
    public AuthToken? Token { get; init; }
    public string? ErrorMessage { get; init; }
}
