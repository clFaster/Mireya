using Microsoft.EntityFrameworkCore;
using Mireya.Application.Services;
using Mireya.Application.Services.Campaign;
using Mireya.Database.Models;
using NSubstitute;

namespace Mireya.Application.Tests;

public class CampaignServiceTests
{
    private static Asset NewAsset(string name = "Asset") => new()
    {
        Name = name,
        Type = AssetType.Image,
        Source = "/uploads/x.png",
    };

    private static Display NewDisplay(string name = "Screen") => new()
    {
        Name = name,
        Location = "Lobby",
        ScreenIdentifier = Guid.NewGuid().ToString("N")[..10],
        UserId = Guid.NewGuid().ToString("N"),
        ApprovalStatus = ApprovalStatus.Approved,
    };

    [Fact]
    public async Task UpdateCampaign_WithNullDisplayIds_PreservesExistingAssignments()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<IScreenSynchronizationService>();

        var asset = NewAsset();
        var display = NewDisplay();
        db.Context.Assets.Add(asset);
        db.Context.Displays.Add(display);
        await db.Context.SaveChangesAsync();

        var service = new CampaignService(db.Context, sync);
        var created = await service.CreateCampaignAsync(new CreateCampaignRequest(
            "Campaign A", null,
            [new CampaignAssetDto(asset.Id, 1, 5)],
            [display.Id]));

        // Update content only, passing null DisplayIds (campaign editor behaviour)
        await service.UpdateCampaignAsync(created.Id, new UpdateCampaignRequest(
            "Campaign A renamed", "desc",
            [new CampaignAssetDto(asset.Id, 1, 9)],
            null));

        await using var verify = db.NewContext();
        var assignments = await verify.CampaignAssignments
            .Where(ca => ca.CampaignId == created.Id).ToListAsync();

        Assert.Single(assignments);
        Assert.Equal(display.Id, assignments[0].DisplayId);
    }

    [Fact]
    public async Task UpdateCampaign_WithEmptyDisplayIds_UnassignsAll()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<IScreenSynchronizationService>();

        var asset = NewAsset();
        var display = NewDisplay();
        db.Context.Assets.Add(asset);
        db.Context.Displays.Add(display);
        await db.Context.SaveChangesAsync();

        var service = new CampaignService(db.Context, sync);
        var created = await service.CreateCampaignAsync(new CreateCampaignRequest(
            "Campaign B", null,
            [new CampaignAssetDto(asset.Id, 1, 5)],
            [display.Id]));

        await service.UpdateCampaignAsync(created.Id, new UpdateCampaignRequest(
            "Campaign B", null,
            [new CampaignAssetDto(asset.Id, 1, 5)],
            []));

        await using var verify = db.NewContext();
        var count = await verify.CampaignAssignments
            .CountAsync(ca => ca.CampaignId == created.Id);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task UpdateCampaign_WithDisplayIds_SyncsAffectedScreens()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<IScreenSynchronizationService>();

        var asset = NewAsset();
        var oldDisplay = NewDisplay("Old");
        var newDisplay = NewDisplay("New");
        db.Context.Assets.Add(asset);
        db.Context.Displays.AddRange(oldDisplay, newDisplay);
        await db.Context.SaveChangesAsync();

        var service = new CampaignService(db.Context, sync);
        var created = await service.CreateCampaignAsync(new CreateCampaignRequest(
            "Campaign C", null,
            [new CampaignAssetDto(asset.Id, 1, 5)],
            [oldDisplay.Id]));

        sync.ClearReceivedCalls();

        await service.UpdateCampaignAsync(created.Id, new UpdateCampaignRequest(
            "Campaign C", null,
            [new CampaignAssetDto(asset.Id, 1, 5)],
            [newDisplay.Id]));

        // Both old (removed) and new (added) screens must be re-synced
        await sync.Received(1).SyncScreensAsync(
            Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Contains(oldDisplay.Id) && ids.Contains(newDisplay.Id)));
    }

    [Fact]
    public async Task SettingDefault_ClearsDefaultOnOtherCampaigns()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<IScreenSynchronizationService>();

        var asset = NewAsset();
        db.Context.Assets.Add(asset);
        await db.Context.SaveChangesAsync();

        var service = new CampaignService(db.Context, sync);

        var first = await service.CreateCampaignAsync(new CreateCampaignRequest(
            "First Default", null, [new CampaignAssetDto(asset.Id, 1, 5)], [], IsDefault: true));

        var second = await service.CreateCampaignAsync(new CreateCampaignRequest(
            "Second Default", null, [new CampaignAssetDto(asset.Id, 1, 5)], [], IsDefault: true));

        await using var verify = db.NewContext();
        var firstReloaded = await verify.Campaigns.FindAsync(first.Id);
        var secondReloaded = await verify.Campaigns.FindAsync(second.Id);

        Assert.False(firstReloaded!.IsDefault);
        Assert.True(secondReloaded!.IsDefault);
        Assert.Equal(1, await verify.Campaigns.CountAsync(c => c.IsDefault));
    }
}
