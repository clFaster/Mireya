using Microsoft.Extensions.Logging.Abstractions;
using Mireya.Application.Services.Audit;
using NSubstitute;

namespace Mireya.Application.Tests;

public class AuditServiceTests
{
    private static AuditService CreateService(TestDatabase db, string? userId, string? userName)
    {
        var userContext = Substitute.For<ICurrentUserContext>();
        userContext.GetCurrentUserAsync().Returns(Task.FromResult((userId, userName)));
        return new AuditService(db.Context, userContext, NullLogger<AuditService>.Instance);
    }

    [Fact]
    public async Task LogAsync_PersistsEntryWithActor()
    {
        using var db = new TestDatabase();
        var service = CreateService(db, "user-1", "admin@mireya.local");

        await service.LogAsync("Created", "Campaign", "abc", "Created campaign 'Promo'");

        var entries = await service.GetRecentAsync();
        var entry = Assert.Single(entries);
        Assert.Equal("admin@mireya.local", entry.ActorName);
        Assert.Equal("Created", entry.Action);
        Assert.Equal("Campaign", entry.EntityType);
        Assert.Equal("abc", entry.EntityId);
        Assert.Equal("Created campaign 'Promo'", entry.Summary);
    }

    [Fact]
    public async Task LogAsync_WithUnknownActor_PersistsNullActor()
    {
        using var db = new TestDatabase();
        var service = CreateService(db, null, null);

        await service.LogAsync("Deleted", "Asset");

        var entry = Assert.Single(await service.GetRecentAsync());
        Assert.Null(entry.ActorName);
        Assert.Equal("Deleted", entry.Action);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestFirst()
    {
        using var db = new TestDatabase();
        var service = CreateService(db, "user-1", "admin");

        await service.LogAsync("Created", "Campaign", "1");
        await service.LogAsync("Updated", "Campaign", "1");
        await service.LogAsync("Deleted", "Campaign", "1");

        var entries = await service.GetRecentAsync();
        Assert.Equal(3, entries.Count);
        Assert.Equal("Deleted", entries[0].Action);
        Assert.Equal("Created", entries[2].Action);
    }

    [Fact]
    public async Task GetRecentAsync_RespectsTakeLimit()
    {
        using var db = new TestDatabase();
        var service = CreateService(db, "user-1", "admin");

        for (var i = 0; i < 5; i++)
            await service.LogAsync("Created", "Asset", i.ToString());

        var entries = await service.GetRecentAsync(2);
        Assert.Equal(2, entries.Count);
    }
}
