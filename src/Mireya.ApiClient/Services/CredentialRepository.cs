using Mireya.ApiClient.Data;
using Mireya.ApiClient.Models;

namespace Mireya.ApiClient.Services;

/// <summary>
///     Unified credential access - combines legacy file storage and database-backed storage
/// </summary>
public interface ICredentialRepository
{
    // Legacy file-based storage
    Task<bool> HasLegacyCredentialsAsync();
    Task SaveLegacyCredentialsAsync(Credentials credentials);
    Task<Credentials?> GetLegacyCredentialsAsync();

    // Database-backed storage (per-backend)
    Task SaveCredentialsAsync(Guid backendId, string username, string accessToken,
        string? refreshToken, DateTime? expiresAt);
    Task<BackendCredential?> GetCredentialsAsync(Guid backendId);
    Task<bool> HasValidCredentialsAsync(Guid backendId);
    Task DeleteCredentialsAsync(Guid backendId);

    // Token access
    string? GetAccessToken();
}

public class CredentialRepository(
    ICredentialStorage storage,
    ICredentialManager manager
) : ICredentialRepository
{
    // Legacy
    public Task<bool> HasLegacyCredentialsAsync() => storage.HasCredentialsAsync();
    public Task SaveLegacyCredentialsAsync(Credentials credentials) => storage.SaveCredentialsAsync(credentials);
    public Task<Credentials?> GetLegacyCredentialsAsync() => storage.GetCredentialsAsync();

    // Database
    public Task SaveCredentialsAsync(Guid backendId, string username, string accessToken,
        string? refreshToken, DateTime? expiresAt) =>
        manager.SaveCredentialsAsync(backendId, username, accessToken, refreshToken, expiresAt);
    public Task<BackendCredential?> GetCredentialsAsync(Guid backendId) => manager.GetCredentialsAsync(backendId);
    public Task<bool> HasValidCredentialsAsync(Guid backendId) => manager.HasValidCredentialsAsync(backendId);
    public Task DeleteCredentialsAsync(Guid backendId) => manager.DeleteCredentialsAsync(backendId);

    // Token
    public string? GetAccessToken()
    {
        var credential = manager.GetCurrentCredentialsSynchronous();
        return credential?.AccessToken;
    }
}
