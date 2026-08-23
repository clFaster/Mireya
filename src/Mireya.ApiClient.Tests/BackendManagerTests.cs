using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Services;

namespace Mireya.ApiClient.Tests;

public class BackendManagerTests
{
    [Fact]
    public async Task SetCurrentBackend_SwitchesTheUniqueCurrentBackendAtomically()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LocalDbContext>().UseSqlite(connection).Options;

        await using var db = new LocalDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var first = NewBackend("https://one.example", isCurrent: true);
        var second = NewBackend("https://two.example", isCurrent: false);
        db.BackendInstances.AddRange(first, second);
        await db.SaveChangesAsync();

        var manager = new BackendManager(db, NullLogger<BackendManager>.Instance);
        await manager.SetCurrentBackendAsync(second.Id);

        Assert.Equal(1, await db.BackendInstances.CountAsync(b => b.IsCurrentBackend));
        Assert.True((await db.BackendInstances.FindAsync(second.Id))!.IsCurrentBackend);
    }

    private static BackendInstance NewBackend(string url, bool isCurrent) =>
        new()
        {
            Id = Guid.NewGuid(),
            BaseUrl = url,
            IsCurrentBackend = isCurrent,
            LastConnectedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
}
