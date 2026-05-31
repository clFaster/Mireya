using Microsoft.Extensions.Logging.Abstractions;
using Mireya.Application.Services.Reporting;
using Mireya.Database.Models;

namespace Mireya.Application.Tests;

public class PlaybackReportingServiceTests
{
    private static PlaybackReportingService CreateService(TestDatabase db) =>
        new(db.Context, NullLogger<PlaybackReportingService>.Instance);

    private static Display SeedDisplay(TestDatabase db, string userId, string name)
    {
        var display = new Display
        {
            Name = name,
            Location = "Lobby",
            ScreenIdentifier = userId[..Math.Min(userId.Length, 10)],
            UserId = userId,
        };
        db.Context.Displays.Add(display);
        db.Context.SaveChanges();
        return display;
    }

    [Fact]
    public async Task RecordAsync_PersistsPlayForKnownScreen()
    {
        using var db = new TestDatabase();
        SeedDisplay(db, "screen-1", "Front Window");
        var service = CreateService(db);

        await service.RecordAsync("screen-1", Guid.NewGuid(), "Promo Video");

        var recent = await service.GetRecentAsync();
        var entry = Assert.Single(recent);
        Assert.Equal("Front Window", entry.DisplayName);
        Assert.Equal("Promo Video", entry.AssetName);
    }

    [Fact]
    public async Task RecordAsync_WithoutAsset_DoesNotPersist()
    {
        using var db = new TestDatabase();
        SeedDisplay(db, "screen-1", "Front Window");
        var service = CreateService(db);

        await service.RecordAsync("screen-1", null, null);

        Assert.Empty(await service.GetRecentAsync());
    }

    [Fact]
    public async Task RecordAsync_ForUnknownScreen_DoesNotPersist()
    {
        using var db = new TestDatabase();
        var service = CreateService(db);

        await service.RecordAsync("ghost", Guid.NewGuid(), "Promo");

        Assert.Empty(await service.GetRecentAsync());
    }

    [Fact]
    public async Task GetReportAsync_AggregatesByAssetAndScreen()
    {
        using var db = new TestDatabase();
        SeedDisplay(db, "screen-1", "Front Window");
        SeedDisplay(db, "screen-2", "Back Wall");
        var service = CreateService(db);

        var promo = Guid.NewGuid();
        var sale = Guid.NewGuid();

        await service.RecordAsync("screen-1", promo, "Promo");
        await service.RecordAsync("screen-1", promo, "Promo");
        await service.RecordAsync("screen-1", sale, "Sale");
        await service.RecordAsync("screen-2", promo, "Promo");

        var report = await service.GetReportAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));

        Assert.Equal(4, report.TotalPlays);
        Assert.Equal(2, report.DistinctAssets);
        Assert.Equal(2, report.DistinctScreens);

        // Promo (3 plays) should rank above Sale (1 play).
        Assert.Equal("Promo", report.ByAsset[0].AssetName);
        Assert.Equal(3, report.ByAsset[0].Plays);

        // Front Window (3 plays) should rank above Back Wall (1 play).
        Assert.Equal("Front Window", report.ByScreen[0].DisplayName);
        Assert.Equal(3, report.ByScreen[0].Plays);
    }

    [Fact]
    public async Task GetReportAsync_ExcludesEventsOutsideWindow()
    {
        using var db = new TestDatabase();
        SeedDisplay(db, "screen-1", "Front Window");
        var service = CreateService(db);

        // An old event written directly with a past timestamp.
        db.Context.PlaybackEvents.Add(new PlaybackEvent
        {
            DisplayId = db.Context.Displays.First().Id,
            DisplayName = "Front Window",
            AssetId = Guid.NewGuid(),
            AssetName = "Old",
            PlayedAtUtc = DateTime.UtcNow.AddDays(-30),
        });
        db.Context.SaveChanges();

        await service.RecordAsync("screen-1", Guid.NewGuid(), "Recent");

        var report = await service.GetReportAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddHours(1));

        Assert.Equal(1, report.TotalPlays);
        Assert.Equal("Recent", report.ByAsset[0].AssetName);
    }
}
