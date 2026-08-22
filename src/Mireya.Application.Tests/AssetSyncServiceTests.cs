using Microsoft.Extensions.Logging.Abstractions;
using Mireya.Application.Services.AssetSync;
using Mireya.Database.Models;

namespace Mireya.Application.Tests;

public class AssetSyncServiceTests
{
    private static (Display display, Asset asset) Seed(TestDatabase db)
    {
        var display = new Display
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
        db.Context.Displays.Add(display);
        db.Context.Assets.Add(asset);
        db.Context.SaveChanges();
        return (display, asset);
    }

    private static AssetSyncService CreateService(TestDatabase db) =>
        new(db.Context, NullLogger<AssetSyncService>.Instance);

    [Fact]
    public async Task UpdateStatus_WhenNoRowExists_ReturnsNotFound()
    {
        using var db = new TestDatabase();
        var (display, asset) = Seed(db);
        var service = CreateService(db);

        var result = await service.UpdateAssetSyncStatusAsync(
            display.Id,
            new UpdateAssetSyncRequest(asset.Id, "Downloaded", 100, null)
        );

        Assert.Equal(AssetSyncUpdateResult.NotFound, result);
    }

    [Fact]
    public async Task UpdateStatus_WithInvalidState_ReturnsInvalidState()
    {
        using var db = new TestDatabase();
        var (display, asset) = Seed(db);
        var service = CreateService(db);
        await service.InitializeSyncStatusForDisplayAsync(display.Id, [asset.Id]);

        var result = await service.UpdateAssetSyncStatusAsync(
            display.Id,
            new UpdateAssetSyncRequest(asset.Id, "NotARealState", 50, null)
        );

        Assert.Equal(AssetSyncUpdateResult.InvalidState, result);
    }

    [Fact]
    public async Task UpdateStatus_WithValidState_ReturnsUpdatedAndPersists()
    {
        using var db = new TestDatabase();
        var (display, asset) = Seed(db);
        var service = CreateService(db);
        await service.InitializeSyncStatusForDisplayAsync(display.Id, [asset.Id]);

        var result = await service.UpdateAssetSyncStatusAsync(
            display.Id,
            new UpdateAssetSyncRequest(asset.Id, "Downloaded", 100, null)
        );

        Assert.Equal(AssetSyncUpdateResult.Updated, result);

        await using var verify = db.NewContext();
        var status = verify.AssetSyncStatuses.Single();
        Assert.Equal(SyncState.Downloaded, status.SyncState);
        Assert.Equal(100, status.Progress);
    }
}
