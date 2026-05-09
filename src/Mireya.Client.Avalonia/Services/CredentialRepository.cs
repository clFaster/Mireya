using System;
using System.Threading.Tasks;
using Mireya.ApiClient.Models;
using Mireya.Client.Avalonia.Data;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
///     Combines legacy credential storage and database-backed credential management
/// </summary>
public class CredentialRepository(
    ICredentialStorage legacyStorage,
    ICredentialManager manager) : ICredentialRepository
{
    public Task<bool> HasLegacyCredentialsAsync() => legacyStorage.HasCredentialsAsync();
    public Task SaveLegacyCredentialsAsync(Credentials credentials) => legacyStorage.SaveCredentialsAsync(credentials);
    public Task<Credentials?> GetLegacyCredentialsAsync() => legacyStorage.GetCredentialsAsync();

    public Task SaveCredentialsAsync(Guid backendId, string username, string accessToken, string? refreshToken = null, DateTime? expiresAt = null) =>
        manager.SaveCredentialsAsync(backendId, username, accessToken, refreshToken, expiresAt);

    public Task<BackendCredential?> GetCredentialsAsync(Guid backendId) => manager.GetCredentialsAsync(backendId);
    public Task<BackendCredential?> GetCurrentCredentialsAsync() => manager.GetCurrentCredentialsAsync();
    public Task<bool> HasValidCredentialsAsync(Guid backendId) => manager.HasValidCredentialsAsync(backendId);
    public Task DeleteCredentialsAsync(Guid backendId) => manager.DeleteCredentialsAsync(backendId);

    public string? GetAccessToken()
    {
        var credential = Task.Run(async () => await manager.GetCurrentCredentialsAsync()).Result;
        return credential?.AccessToken;
    }
}
