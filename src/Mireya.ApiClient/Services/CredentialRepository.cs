using Mireya.ApiClient.Data;
using Mireya.ApiClient.Models;

namespace Mireya.ApiClient.Services;

/// <summary>
///     Unified credential access - combines legacy file storage and database-backed storage
/// </summary>
public interface ICredentialRepository
{
    Task MigrateLegacyCredentialsAsync();

    // Database-backed storage (per-backend)
    Task SaveRegistrationAsync(Guid backendId, string username, string password);
    Task SaveTokensAsync(
        Guid backendId,
        string accessToken,
        string? refreshToken,
        DateTime? expiresAt
    );
    Task<BackendCredential?> GetCredentialsAsync(Guid backendId);
    Task<bool> HasValidCredentialsAsync(Guid backendId);
    Task DeleteCredentialsAsync(Guid backendId);

    // Token access
    string? GetAccessToken();
}

public class CredentialRepository(ICredentialStorage storage, ICredentialManager manager)
    : ICredentialRepository
{
    public async Task MigrateLegacyCredentialsAsync()
    {
        var legacy = await storage.GetCredentialsAsync();
        if (legacy == null)
            return;

        // Only migrate when the legacy username matches an existing backend registration.
        // This prevents credentials from one server being applied to a newly added server.
        if (await manager.TryMigrateLegacyCredentialsAsync(legacy.Username, legacy.Password))
            await storage.DeleteCredentialsAsync();
    }

    // Database
    public Task SaveRegistrationAsync(Guid backendId, string username, string password) =>
        manager.SaveRegistrationAsync(backendId, username, password);

    public Task SaveTokensAsync(
        Guid backendId,
        string accessToken,
        string? refreshToken,
        DateTime? expiresAt
    ) => manager.SaveTokensAsync(backendId, accessToken, refreshToken, expiresAt);

    public Task<BackendCredential?> GetCredentialsAsync(Guid backendId) =>
        manager.GetCredentialsAsync(backendId);

    public Task<bool> HasValidCredentialsAsync(Guid backendId) =>
        manager.HasValidCredentialsAsync(backendId);

    public Task DeleteCredentialsAsync(Guid backendId) => manager.DeleteCredentialsAsync(backendId);

    // Token
    public string? GetAccessToken()
    {
        var credential = manager.GetCurrentCredentialsSynchronous();
        return credential?.AccessToken;
    }
}
