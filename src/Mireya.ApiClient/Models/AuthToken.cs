namespace Mireya.ApiClient.Models;

/// <summary>
/// JWT authentication token response
/// </summary>
public class AuthToken
{
    public required string AccessToken { get; init; }
    public required string TokenType { get; init; }
    public required int ExpiresIn { get; init; }
    public string? RefreshToken { get; init; }
}
