using Microsoft.Extensions.Logging.Abstractions;
using Mireya.Application.Services.AssetSync;
using Mireya.Database.Models;

namespace Mireya.Application.Tests;

public class AssetSyncServiceTests
{
    private static (Screen screen, Asset asset) Seed(TestDatabase db)
    {
        var screen = new Screen
        {
            Name = "Screen",
            Location = "Lobby",
            ScreenIdentifier = Guid.NewGuid().ToString("N")[..10],
            UserId = Guid.NewGuid().ToString("N"),
            ApprovalStatus = ApprovalStatus.Approved,
        };
        var asset = new Asset
        {
            Name = "A",
            Type = AssetType.Image,
            Source = "/x.png",
        };
        db.AddScreen(screen);
        db.Context.Assets.Add(asset);
        db.Context.SaveChanges();
        return (screen, asset);
    }

    private static AssetSyncService CreateService(TestDatabase db) =>
        new(db.Context, NullLogger<AssetSyncService>.Instance);

    [Fact]
    public async Task UpdateStatus_WhenNoRowExists_ReturnsNotFound()
    {
        using var db = new TestDatabase();
        var (screen, asset) = Seed(db);
        var service = CreateService(db);

        var result = await service.UpdateAssetSyncStatusAsync(
            screen.Id,
            new UpdateAssetSyncRequest(asset.Id, "Downloaded", 100, null)
        );

        Assert.Equal(AssetSyncUpdateResult.NotFound, result);
    }

    [Fact]
    public async Task UpdateStatus_WithInvalidState_ReturnsInvalidState()
    {
        using var db = new TestDatabase();
        var (screen, asset) = Seed(db);
        var service = CreateService(db);
        await service.InitializeSyncStatusForScreenAsync(screen.Id, [asset.Id]);

        var result = await service.UpdateAssetSyncStatusAsync(
            screen.Id,
            new UpdateAssetSyncRequest(asset.Id, "NotARealState", 50, null)
        );

        Assert.Equal(AssetSyncUpdateResult.InvalidState, result);
    }

    [Fact]
    public async Task UpdateStatus_WithValidState_ReturnsUpdatedAndPersists()
    {
        using var db = new TestDatabase();
        var (screen, asset) = Seed(db);
        var service = CreateService(db);
        await service.InitializeSyncStatusForScreenAsync(screen.Id, [asset.Id]);

        var result = await service.UpdateAssetSyncStatusAsync(
            screen.Id,
            new UpdateAssetSyncRequest(asset.Id, "Downloaded", 100, null)
        );

        Assert.Equal(AssetSyncUpdateResult.Updated, result);

        await using var verify = db.NewContext();
        var status = verify.AssetSyncStatuses.Single();
        Assert.Equal(SyncState.Downloaded, status.SyncState);
        Assert.Equal(100, status.Progress);
    }

    [Fact]
    public async Task GetCampaignsToSync_IncludesFutureAssignments()
    {
        using var db = new TestDatabase();
        var (screen, asset) = Seed(db);
        var scheduled = new Campaign { Name = "Scheduled" };
        scheduled.CampaignAssets.Add(new CampaignAsset { Asset = asset, Position = 1 });
        scheduled.CampaignAssignments.Add(
            new CampaignAssignment { Screen = screen, StartDateUtc = DateTime.UtcNow.AddDays(1) }
        );
        db.Context.Campaigns.Add(scheduled);
        await db.Context.SaveChangesAsync();

        var campaigns = await CreateService(db).GetCampaignsToSyncAsync(screen.Id);

        var campaign = Assert.Single(campaigns);
        Assert.Equal(scheduled.Id, campaign.CampaignId);
        Assert.Single(campaign.Assets);
    }

    [Theory]
    [InlineData(ApprovalStatus.Pending)]
    [InlineData(ApprovalStatus.Rejected)]
    public async Task GetCampaignsToSync_WithoutApproval_ReturnsNoCampaigns(
        ApprovalStatus approvalStatus
    )
    {
        using var db = new TestDatabase();
        var (screen, asset) = Seed(db);
        screen.ApprovalStatus = approvalStatus;
        var campaign = new Campaign { Name = "Assigned" };
        campaign.CampaignAssets.Add(new CampaignAsset { Asset = asset, Position = 1 });
        campaign.CampaignAssignments.Add(new CampaignAssignment { Screen = screen });
        db.Context.Campaigns.Add(campaign);
        await db.Context.SaveChangesAsync();

        var campaigns = await CreateService(db).GetCampaignsToSyncAsync(screen.Id);

        Assert.Empty(campaigns);
    }
}
