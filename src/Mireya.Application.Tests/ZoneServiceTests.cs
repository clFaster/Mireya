using Microsoft.EntityFrameworkCore;
using Mireya.Application.Services;
using Mireya.Application.Services.Audit;
using Mireya.Application.Services.Zones;
using Mireya.Database.Models;
using NSubstitute;

namespace Mireya.Application.Tests;

public class ZoneServiceTests
{
    private static Display NewDisplay(string name = "Screen") => new()
    {
        Name = name,
        Location = "Lobby",
        ScreenIdentifier = Guid.NewGuid().ToString("N")[..10],
        UserId = Guid.NewGuid().ToString("N"),
        ApprovalStatus = ApprovalStatus.Approved,
    };

    private static ZoneService CreateService(TestDatabase db, IScreenSynchronizationService sync) =>
        new(db.Context, sync, Substitute.For<IAuditService>());

    [Fact]
    public async Task CreateZone_WithCampaigns_PersistsAndReturnsDetail()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<IScreenSynchronizationService>();
        var campaign = new Campaign { Name = "Promo", IsEnabled = true };
        db.Context.Campaigns.Add(campaign);
        await db.Context.SaveChangesAsync();

        var service = CreateService(db, sync);
        var detail = await service.CreateZoneAsync(
            new CreateZoneRequest("Lobby", "Front desk screens", [campaign.Id]));

        Assert.Equal("Lobby", detail.Name);
        Assert.Single(detail.Campaigns);
        Assert.Equal(campaign.Id, detail.Campaigns[0].Id);

        await using var verify = db.NewContext();
        var zoneCampaigns = await verify.ZoneCampaigns.Where(zc => zc.ZoneId == detail.Id).ToListAsync();
        Assert.Single(zoneCampaigns);
    }

    [Fact]
    public async Task CreateZone_WithUnknownCampaign_Throws()
    {
        using var db = new TestDatabase();
        var service = CreateService(db, Substitute.For<IScreenSynchronizationService>());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateZoneAsync(new CreateZoneRequest("Lobby", null, [Guid.NewGuid()])));
    }

    [Fact]
    public async Task UpdateZone_ReplacesCampaigns_AndResyncsMembers()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<IScreenSynchronizationService>();
        var first = new Campaign { Name = "First", IsEnabled = true };
        var second = new Campaign { Name = "Second", IsEnabled = true };
        var display = NewDisplay();
        db.Context.Campaigns.AddRange(first, second);
        db.Context.Displays.Add(display);
        await db.Context.SaveChangesAsync();

        var service = CreateService(db, sync);
        var zone = await service.CreateZoneAsync(new CreateZoneRequest("Zone", null, [first.Id]));

        // Make the display a member of the zone.
        display.ZoneId = zone.Id;
        await db.Context.SaveChangesAsync();

        var updated = await service.UpdateZoneAsync(zone.Id,
            new UpdateZoneRequest("Zone", null, [second.Id]));

        Assert.Single(updated.Campaigns);
        Assert.Equal(second.Id, updated.Campaigns[0].Id);

        await sync.Received().SyncScreensAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(display.Id)));
    }

    [Fact]
    public async Task DeleteZone_DetachesMembers_AndResyncs()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<IScreenSynchronizationService>();
        var display = NewDisplay();
        db.Context.Displays.Add(display);
        await db.Context.SaveChangesAsync();

        var service = CreateService(db, sync);
        var zone = await service.CreateZoneAsync(new CreateZoneRequest("Zone", null, []));

        display.ZoneId = zone.Id;
        await db.Context.SaveChangesAsync();

        await service.DeleteZoneAsync(zone.Id);

        await using var verify = db.NewContext();
        var refreshed = await verify.Displays.FindAsync(display.Id);
        Assert.NotNull(refreshed);
        Assert.Null(refreshed!.ZoneId);
        Assert.False(await verify.Zones.AnyAsync(z => z.Id == zone.Id));
        await sync.Received().SyncScreensAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(display.Id)));
    }
}
