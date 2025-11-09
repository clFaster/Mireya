using System.Security.Cryptography;
using Mireya.ApiClient.Models;

namespace Mireya.ApiClient.Services.Authentication;

/// <summary>
/// Generates secure credentials for screen registration
/// </summary>
public static class CredentialGenerator
{
    /// <summary>
    /// Generate a new set of secure credentials
    /// </summary>
    /// <returns>Screen credentials with generated username and password</returns>
    public static ScreenCredentials Generate()
    {
        // Generate username as a GUID (ensures uniqueness)
        var username = Guid.NewGuid().ToString();

        // Generate a secure random password (32 characters, alphanumeric + special chars)
        var password = GenerateSecurePassword(32);

        return new ScreenCredentials(username, password);
    }

    /// <summary>
    /// Generate a cryptographically secure random password
    /// </summary>
    /// <param name="length">Length of the password</param>
    /// <returns>Secure random password</returns>
    private static string GenerateSecurePassword(int length)
    {
        const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+-=";
        var result = new char[length];

        using var rng = RandomNumberGenerator.Create();
        var randomBytes = new byte[length];
        rng.GetBytes(randomBytes);

        for (var i = 0; i < length; i++)
        {
            result[i] = validChars[randomBytes[i] % validChars.Length];
        }

        return new string(result);
    }
}
