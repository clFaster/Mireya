namespace Mireya.ApiClient.Models;

/// <summary>
/// Screen credentials for authentication
/// </summary>
/// <param name="Username">Client-generated username</param>
/// <param name="Password">Client-generated password</param>
public record ScreenCredentials(string Username, string Password);
