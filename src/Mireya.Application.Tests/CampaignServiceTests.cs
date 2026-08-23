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
    public async Task UpdateCampaign_PreservesExistingAssignmentSchedule()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<IScreenSynchronizationService>();
        var asset = NewAsset();
        var screen = NewScreen();
        var campaign = new Campaign { Name = "Campaign" };
        db.Context.Assets.Add(asset);
        db.AddScreen(screen);
        db.Context.Campaigns.Add(campaign);
        db.Context.CampaignAssignments.Add(
            new CampaignAssignment
            {
                Campaign = campaign,
                Screen = screen,
                TargetKind = CampaignAssignmentTargetKind.Screen,
                StartDateUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Priority = 7,
            }
        );
        await db.Context.SaveChangesAsync();

        var service = new CampaignService(db.Context, sync, Substitute.For<IAuditService>());
        await service.UpdateCampaignAsync(
            campaign.Id,
            new UpdateCampaignRequest(
                "Renamed",
                "Description",
                [new CampaignAssetDto(asset.Id, 1, 5)]
            )
        );

        await using var verify = db.NewContext();
        var assignment = await verify.CampaignAssignments.SingleAsync();
        Assert.Equal(7, assignment.Priority);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), assignment.StartDateUtc);
    }

    [Fact]
    public async Task UpdateCampaign_SyncsDirectlyAssignedScreens()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<IScreenSynchronizationService>();
        var asset = NewAsset();
        var screen = NewScreen();
        var campaign = new Campaign { Name = "Campaign" };
        db.Context.Assets.Add(asset);
        db.AddScreen(screen);
        db.Context.Campaigns.Add(campaign);
        db.Context.CampaignAssignments.Add(
            new CampaignAssignment
            {
                Campaign = campaign,
                Screen = screen,
                TargetKind = CampaignAssignmentTargetKind.Screen,
            }
        );
        await db.Context.SaveChangesAsync();

        var service = new CampaignService(db.Context, sync, Substitute.For<IAuditService>());
        await service.UpdateCampaignAsync(
            campaign.Id,
            new UpdateCampaignRequest("Campaign", null, [new CampaignAssetDto(asset.Id, 1, 5)])
        );

        await sync.Received(1)
            .SyncScreensAsync(
                Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { screen.Id }))
            );
    }

    [Fact]
    public async Task SettingGlobalFallback_ReusesSingleAssignment()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<IScreenSynchronizationService>();
        var first = new Campaign { Name = "First" };
        var second = new Campaign { Name = "Second" };
        db.Context.Campaigns.AddRange(first, second);
        await db.Context.SaveChangesAsync();

        var service = new CampaignService(db.Context, sync, Substitute.For<IAuditService>());
        await service.SetGlobalFallbackAsync(new CampaignAssignmentRequest(first.Id));
        await service.SetGlobalFallbackAsync(
            new CampaignAssignmentRequest(second.Id, Priority: 12)
        );

        await using var verify = db.NewContext();
        var fallback = await verify.CampaignAssignments.SingleAsync(a =>
            a.TargetKind == CampaignAssignmentTargetKind.GlobalFallback
        );
        Assert.Equal(second.Id, fallback.CampaignId);
        Assert.Equal(12, fallback.Priority);
    }

    [Fact]
    public async Task CreateCampaign_DoesNotCreatePlaybackAssignments()
    {
        using var db = new TestDatabase();
        var asset = NewAsset();
        db.Context.Assets.Add(asset);
        await db.Context.SaveChangesAsync();
        var service = new CampaignService(
            db.Context,
            Substitute.For<IScreenSynchronizationService>(),
            Substitute.For<IAuditService>()
        );

        var campaign = await service.CreateCampaignAsync(
            new CreateCampaignRequest(
                "Reusable content",
                null,
                [new CampaignAssetDto(asset.Id, 1, 5)]
            )
        );

        Assert.Empty(campaign.Assignments);
        Assert.Equal(0, await db.Context.CampaignAssignments.CountAsync());
    }
}
