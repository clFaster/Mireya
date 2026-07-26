using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Data;

namespace Mireya.ApiClient.Services;

public interface ICredentialManager
{
    Task SaveRegistrationAsync(Guid backendInstanceId, string username, string password);
    Task SaveTokensAsync(
        Guid backendInstanceId,
        string accessToken,
        string? refreshToken = null,
        DateTime? expiresAt = null
    );
    Task<bool> TryMigrateLegacyCredentialsAsync(string username, string password);

    Task<BackendCredential?> GetCredentialsAsync(Guid backendInstanceId);
    Task<BackendCredential?> GetCurrentCredentialsAsync();

    /// <summary>
    /// Synchronous version of GetCurrentCredentialsAsync for use in non-async contexts
    /// (e.g. token providers called from synchronous HTTP pipeline code).
    /// Avoids sync-over-async deadlocks by using synchronous EF Core queries.
    /// </summary>
    BackendCredential? GetCurrentCredentialsSynchronous();

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

    public async Task SaveRegistrationAsync(
        Guid backendInstanceId,
        string username,
        string password
    )
    {
        _logger.LogInformation(
            "Saving registration credentials for backend {BackendId}, username: {Username}",
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
                Password = password,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.BackendCredentials.Add(credential);
            _logger.LogDebug("Created new backend registration");
        }
        else
        {
            credential.Username = username;
            credential.Password = password;
            credential.AccessToken = null;
            credential.RefreshToken = null;
            credential.TokenExpiresAt = null;
            credential.UpdatedAt = DateTime.UtcNow;
            _logger.LogDebug("Replaced backend registration");
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Registration credentials saved successfully (encrypted)");
    }

    public async Task SaveTokensAsync(
        Guid backendInstanceId,
        string accessToken,
        string? refreshToken = null,
        DateTime? expiresAt = null
    )
    {
        var credential =
            await _db.BackendCredentials.FindAsync(backendInstanceId)
            ?? throw new InvalidOperationException(
                $"Cannot save tokens before backend {backendInstanceId} is registered."
            );

        credential.AccessToken = accessToken;
        credential.RefreshToken = refreshToken;
        credential.TokenExpiresAt = expiresAt;
        credential.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        _logger.LogInformation(
            "Authentication tokens saved for backend {BackendId}",
            backendInstanceId
        );
    }

    public async Task<bool> TryMigrateLegacyCredentialsAsync(string username, string password)
    {
        var credential = await _db.BackendCredentials.FirstOrDefaultAsync(c =>
            c.Username == username
        );

        if (credential == null)
            return false;

        if (string.IsNullOrEmpty(credential.Password))
        {
            credential.Password = password;
            credential.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            _logger.LogInformation(
                "Migrated legacy password to backend {BackendId}",
                credential.BackendInstanceId
            );
        }

        return true;
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

    public BackendCredential? GetCurrentCredentialsSynchronous()
    {
        var backend = _db.BackendInstances.FirstOrDefault(b => b.IsCurrentBackend);
        if (backend == null)
            return null;

        return _db.BackendCredentials.Find(backend.Id);
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
