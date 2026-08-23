using Microsoft.EntityFrameworkCore;
using Mireya.Application.Services;
using Mireya.Application.Services.Audit;
using Mireya.Application.Services.Campaign;
using Mireya.Database.Models;
using NSubstitute;

namespace Mireya.Application.Tests;

public class CampaignServiceTests
{
    private static Asset NewAsset(string name = "Asset") =>
        new()
        {
            Name = name,
            Type = AssetType.Image,
            Source = "/uploads/x.png",
        };

    private static Screen NewScreen(string name = "Screen") =>
        new()
        {
            Name = name,
            Location = "Lobby",
            ScreenIdentifier = Guid.NewGuid().ToString("N")[..10],
            UserId = Guid.NewGuid().ToString("N"),
            ApprovalStatus = ApprovalStatus.Approved,
        };

    [Fact]
    public async Task UpdateCampaign_WithNullScreenIds_PreservesExistingAssignments()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<IScreenSynchronizationService>();

        var asset = NewAsset();
        var screen = NewScreen();
        db.Context.Assets.Add(asset);
        db.AddScreen(screen);
        await db.Context.SaveChangesAsync();

        var service = new CampaignService(db.Context, sync, Substitute.For<IAuditService>());
        var created = await service.CreateCampaignAsync(
            new CreateCampaignRequest(
                "Campaign A",
                null,
                [new CampaignAssetDto(asset.Id, 1, 5)],
                [screen.Id]
            )
        );

        // Update content only, passing null ScreenIds (campaign editor behaviour)
        await service.UpdateCampaignAsync(
            created.Id,
            new UpdateCampaignRequest(
                "Campaign A renamed",
                "desc",
                [new CampaignAssetDto(asset.Id, 1, 9)],
                null
            )
        );

        await using var verify = db.NewContext();
        var assignments = await verify
            .CampaignAssignments.Where(ca => ca.CampaignId == created.Id)
            .ToListAsync();

        Assert.Single(assignments);
        Assert.Equal(screen.Id, assignments[0].ScreenId);
    }

    [Fact]
    public async Task UpdateCampaign_WithEmptyScreenIds_UnassignsAll()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<IScreenSynchronizationService>();

        var asset = NewAsset();
        var screen = NewScreen();
        db.Context.Assets.Add(asset);
        db.AddScreen(screen);
        await db.Context.SaveChangesAsync();

        var service = new CampaignService(db.Context, sync, Substitute.For<IAuditService>());
        var created = await service.CreateCampaignAsync(
            new CreateCampaignRequest(
                "Campaign B",
                null,
                [new CampaignAssetDto(asset.Id, 1, 5)],
                [screen.Id]
            )
        );

        await service.UpdateCampaignAsync(
            created.Id,
            new UpdateCampaignRequest(
                "Campaign B",
                null,
                [new CampaignAssetDto(asset.Id, 1, 5)],
                []
            )
        );

        await using var verify = db.NewContext();
        var count = await verify.CampaignAssignments.CountAsync(ca => ca.CampaignId == created.Id);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task UpdateCampaign_WithScreenIds_SyncsAffectedScreens()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<IScreenSynchronizationService>();

        var asset = NewAsset();
        var oldScreen = NewScreen("Old");
        var newScreen = NewScreen("New");
        db.Context.Assets.Add(asset);
        db.AddScreen(oldScreen);
        db.AddScreen(newScreen);
        await db.Context.SaveChangesAsync();

        var service = new CampaignService(db.Context, sync, Substitute.For<IAuditService>());
        var created = await service.CreateCampaignAsync(
            new CreateCampaignRequest(
                "Campaign C",
                null,
                [new CampaignAssetDto(asset.Id, 1, 5)],
                [oldScreen.Id]
            )
        );

        sync.ClearReceivedCalls();

        await service.UpdateCampaignAsync(
            created.Id,
            new UpdateCampaignRequest(
                "Campaign C",
                null,
                [new CampaignAssetDto(asset.Id, 1, 5)],
                [newScreen.Id]
            )
        );

        // Both old (removed) and new (added) screens must be re-synced
        await sync.Received(1)
            .SyncScreensAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.Contains(oldScreen.Id) && ids.Contains(newScreen.Id)
                )
            );
    }

    [Fact]
    public async Task SettingDefault_ClearsDefaultOnOtherCampaigns()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<IScreenSynchronizationService>();

        var asset = NewAsset();
        db.Context.Assets.Add(asset);
        await db.Context.SaveChangesAsync();

        var service = new CampaignService(db.Context, sync, Substitute.For<IAuditService>());

        var first = await service.CreateCampaignAsync(
            new CreateCampaignRequest(
                "First Default",
                null,
                [new CampaignAssetDto(asset.Id, 1, 5)],
                [],
                IsDefault: true
            )
        );

        var second = await service.CreateCampaignAsync(
            new CreateCampaignRequest(
                "Second Default",
                null,
                [new CampaignAssetDto(asset.Id, 1, 5)],
                [],
                IsDefault: true
            )
        );

        await using var verify = db.NewContext();
        var firstReloaded = await verify.Campaigns.FindAsync(first.Id);
        var secondReloaded = await verify.Campaigns.FindAsync(second.Id);

        Assert.False(firstReloaded!.IsDefault);
        Assert.True(secondReloaded!.IsDefault);
        Assert.Equal(1, await verify.Campaigns.CountAsync(c => c.IsDefault));
    }
}
