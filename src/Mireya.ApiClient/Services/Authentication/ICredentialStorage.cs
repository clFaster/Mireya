using Mireya.ApiClient.Models;

namespace Mireya.ApiClient.Services.Authentication;

/// <summary>
/// Platform-agnostic interface for securely storing screen credentials
/// </summary>
public interface ICredentialStorage
{
    /// <summary>
    /// Load stored credentials
    /// </summary>
    /// <returns>Credentials if they exist, null otherwise</returns>
    Task<ScreenCredentials?> LoadAsync();

    /// <summary>
    /// Save credentials securely
    /// </summary>
    /// <param name="credentials">Credentials to save</param>
    Task SaveAsync(ScreenCredentials credentials);

    /// <summary>
    /// Delete stored credentials
    /// </summary>
    Task DeleteAsync();
}
