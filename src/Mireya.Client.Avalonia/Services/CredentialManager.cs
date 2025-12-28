using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mireya.Client.Avalonia.Data;

namespace Mireya.Client.Avalonia.Services;

public interface ICredentialManager
{
    Task SaveCredentialsAsync(
        Guid backendInstanceId,
        string username,
        string accessToken,
        string? refreshToken = null,
        DateTime? expiresAt = null
    );

    Task<BackendCredential?> GetCredentialsAsync(Guid backendInstanceId);
    Task<BackendCredential?> GetCurrentCredentialsAsync();
    Task<bool> HasValidCredentialsAsync(Guid backendInstanceId);
    Task DeleteCredentialsAsync(Guid backendInstanceId);
}

public class CredentialManager : ICredentialManager
{
    private readonly LocalDbContext _db;
    private readonly ILogger<CredentialManager> _logger;

    public CredentialManager(LocalDbContext db, ILogger<CredentialManager> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SaveCredentialsAsync(
        Guid backendInstanceId,
        string username,
        string accessToken,
        string? refreshToken = null,
        DateTime? expiresAt = null
    )
    {
        _logger.LogInformation(
            "Saving credentials for backend {BackendId}, username: {Username}",
            backendInstanceId,
            username
        );

        var credential = await _db.BackendCredentials.FindAsync(backendInstanceId);

        if (credential == null)
        {
            credential = new BackendCredential
            {
                BackendInstanceId = backendInstanceId,
                Username = username,
                AccessToken = accessToken, // Automatically encrypted via property setter
                RefreshToken = refreshToken,
                TokenExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.BackendCredentials.Add(credential);
            _logger.LogDebug("Created new credential record");
        }
        else
        {
            credential.Username = username;
            credential.AccessToken = accessToken;
            credential.RefreshToken = refreshToken;
            credential.TokenExpiresAt = expiresAt;
            credential.UpdatedAt = DateTime.UtcNow;
            _logger.LogDebug("Updated existing credential record");
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Credentials saved successfully (encrypted)");
    }

    public async Task<BackendCredential?> GetCredentialsAsync(Guid backendInstanceId)
    {
        _logger.LogDebug("Retrieving credentials for backend {BackendId}", backendInstanceId);
        return await _db.BackendCredentials.FindAsync(backendInstanceId);
    }

    public async Task<BackendCredential?> GetCurrentCredentialsAsync()
    {
        _logger.LogDebug("Retrieving credentials for current backend");

        var backend = await _db.BackendInstances.FirstOrDefaultAsync(b => b.IsCurrentBackend);

        if (backend == null)
        {
            _logger.LogWarning("No current backend set");
            return null;
        }

        return await GetCredentialsAsync(backend.Id);
    }

    public async Task<bool> HasValidCredentialsAsync(Guid backendInstanceId)
    {
        var credential = await GetCredentialsAsync(backendInstanceId);

        if (credential == null)
        {
            _logger.LogDebug("No credentials found for backend {BackendId}", backendInstanceId);
            return false;
        }

        if (string.IsNullOrEmpty(credential.AccessToken))
        {
            _logger.LogDebug(
                "Credentials exist but access token is empty for backend {BackendId}",
                backendInstanceId
            );
            return false;
        }

        // Check if token is expired
        if (
            credential.TokenExpiresAt.HasValue
            && credential.TokenExpiresAt.Value <= DateTime.UtcNow
        )
        {
            _logger.LogWarning(
                "Token expired for backend {BackendId} at {ExpiresAt}",
                backendInstanceId,
                credential.TokenExpiresAt
            );
            return false;
        }

        _logger.LogDebug("Valid credentials found for backend {BackendId}", backendInstanceId);
        return true;
    }

    public async Task DeleteCredentialsAsync(Guid backendInstanceId)
    {
        _logger.LogInformation("Deleting credentials for backend {BackendId}", backendInstanceId);

        var credential = await _db.BackendCredentials.FindAsync(backendInstanceId);
        if (credential != null)
        {
            _db.BackendCredentials.Remove(credential);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Credentials deleted successfully");
        }
        else
        {
            _logger.LogDebug(
                "No credentials found to delete for backend {BackendId}",
                backendInstanceId
            );
        }
    }
}
