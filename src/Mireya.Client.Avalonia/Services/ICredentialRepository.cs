using System;
using System.Threading.Tasks;
using Mireya.ApiClient.Models;
using Mireya.Client.Avalonia.Data;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
///     Unified credential access combining legacy file storage and database-backed credential management
/// </summary>
public interface ICredentialRepository
{
    // Legacy (file-based) storage methods
    Task<bool> HasLegacyCredentialsAsync();
    Task SaveLegacyCredentialsAsync(Credentials credentials);
    Task<Credentials?> GetLegacyCredentialsAsync();

    // Backend-specific database methods
    Task SaveCredentialsAsync(Guid backendId, string username, string accessToken, string? refreshToken = null, DateTime? expiresAt = null);
    Task<BackendCredential?> GetCredentialsAsync(Guid backendId);
    Task<BackendCredential?> GetCurrentCredentialsAsync();
    Task<bool> HasValidCredentialsAsync(Guid backendId);
    Task DeleteCredentialsAsync(Guid backendId);
    string? GetAccessToken();
}
