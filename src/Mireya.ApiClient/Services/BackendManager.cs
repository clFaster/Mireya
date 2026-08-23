using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Data;

namespace Mireya.ApiClient.Services;

public interface IBackendManager
{
    Task<BackendInstance> GetOrCreateBackendAsync(string baseUrl, string? name = null);
    Task SetCurrentBackendAsync(Guid backendInstanceId);
    Task<BackendInstance?> GetCurrentBackendAsync();
    Task<List<BackendInstance>> GetAllBackendsAsync();
    Task<bool> HasCurrentBackendAsync();
    Task UpdateBackendNameAsync(Guid backendInstanceId, string name);
    Task DeleteBackendAsync(Guid backendInstanceId);
}

public class BackendManager : IBackendManager
{
    private readonly LocalDbContext _db;
    private readonly ILogger<BackendManager> _logger;

    public BackendManager(LocalDbContext db, ILogger<BackendManager> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<BackendInstance> GetOrCreateBackendAsync(string baseUrl, string? name = null)
    {
        var normalized = baseUrl.TrimEnd('/').ToLowerInvariant();

        _logger.LogInformation("Getting or creating backend for URL: {BaseUrl}", normalized);

        var backend = await _db.BackendInstances.FirstOrDefaultAsync(b => b.BaseUrl == normalized);

        if (backend == null)
        {
            backend = new BackendInstance
            {
                Id = Guid.NewGuid(),
                BaseUrl = normalized,
                Name = name,
                IsCurrentBackend = false,
                LastConnectedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            _db.BackendInstances.Add(backend);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Created new backend {BackendId} for URL: {BaseUrl}",
                backend.Id,
                normalized
            );
        }
        else
        {
            backend.LastConnectedAt = DateTime.UtcNow;
            if (name != null && backend.Name != name)
                backend.Name = name;
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Found existing backend {BackendId} for URL: {BaseUrl}",
                backend.Id,
                normalized
            );
        }

        return backend;
    }

    public async Task SetCurrentBackendAsync(Guid backendInstanceId)
    {
        _logger.LogInformation("Setting current backend to {BackendId}", backendInstanceId);

        var all = await _db.BackendInstances.ToListAsync();
        var current = all.FirstOrDefault(b => b.Id == backendInstanceId);
        if (current == null)
        {
            _logger.LogError("Backend {BackendId} not found!", backendInstanceId);
            return;
        }

        // Persist the switch in two phases inside one transaction. This satisfies
        // the unique index without exposing an intermediate "no current backend" state.
        await using var transaction = await _db.Database.BeginTransactionAsync();
        foreach (var backend in all)
            backend.IsCurrentBackend = false;
        await _db.SaveChangesAsync();

        current.IsCurrentBackend = true;
        current.LastConnectedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        _logger.LogInformation(
            "Current backend set to {BackendId} - {BaseUrl}",
            current.Id,
            current.BaseUrl
        );
    }

    public async Task<BackendInstance?> GetCurrentBackendAsync()
    {
        _logger.LogDebug("Getting current backend");

        var backend = await _db.BackendInstances.FirstOrDefaultAsync(b => b.IsCurrentBackend);

        if (backend != null)
            _logger.LogDebug(
                "Current backend: {BackendId} - {BaseUrl}",
                backend.Id,
                backend.BaseUrl
            );
        else
            _logger.LogDebug("No current backend set");

        return backend;
    }

    public async Task<List<BackendInstance>> GetAllBackendsAsync()
    {
        _logger.LogDebug("Getting all backends");

        var backends = await _db
            .BackendInstances.OrderByDescending(b => b.LastConnectedAt)
            .ToListAsync();

        _logger.LogDebug("Found {Count} backend(s)", backends.Count);
        return backends;
    }

    public async Task<bool> HasCurrentBackendAsync()
    {
        var current = await GetCurrentBackendAsync();
        return current != null;
    }

    public async Task UpdateBackendNameAsync(Guid backendInstanceId, string name)
    {
        _logger.LogInformation(
            "Updating backend {BackendId} name to: {Name}",
            backendInstanceId,
            name
        );

        var backend = await _db.BackendInstances.FindAsync(backendInstanceId);
        if (backend != null)
        {
            backend.Name = name;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Backend name updated successfully");
        }
        else
        {
            _logger.LogError("Backend {BackendId} not found!", backendInstanceId);
        }
    }

    public async Task DeleteBackendAsync(Guid backendInstanceId)
    {
        _logger.LogInformation("Deleting backend {BackendId}", backendInstanceId);

        var backend = await _db.BackendInstances.FindAsync(backendInstanceId);
        if (backend != null)
        {
            _db.BackendInstances.Remove(backend);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Backend {BackendId} deleted successfully", backendInstanceId);
        }
        else
        {
            _logger.LogWarning("Backend {BackendId} not found for deletion", backendInstanceId);
        }
    }
}
