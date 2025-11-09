namespace Mireya.ApiClient.Models;

/// <summary>
/// Result of a registration attempt
/// </summary>
public class RegisterResult
{
    public required bool Success { get; init; }
    public string? ScreenIdentifier { get; init; }
    public string? UserId { get; init; }
    public string? ErrorMessage { get; init; }
}
