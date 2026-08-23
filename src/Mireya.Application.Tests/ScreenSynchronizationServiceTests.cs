using Microsoft.Extensions.Logging.Abstractions;
using Mireya.Application.Hubs;
using Mireya.Application.Services;
using Mireya.Application.Services.AssetSync;
using Mireya.Application.Services.ScreenManagement;
using Mireya.Database.Models;
using NSubstitute;

namespace Mireya.Application.Tests;

public class ScreenSynchronizationServiceTests
{
    private static Screen NewScreen() =>
        new()
        {
            Name = "Screen",
            Location = "Lobby",
            ScreenIdentifier = Guid.NewGuid().ToString("N")[..10],
            UserId = Guid.NewGuid().ToString("N"),
            ApprovalStatus = ApprovalStatus.Approved,
        };

    private static (
        ScreenSynchronizationService service,
        Func<ScreenConfiguration?> captured
    ) CreateService(TestDatabase db)
    {
        var (service, captured, _) = CreateServiceWithHub(db);
        return (service, captured);
    }

    private static (
        ScreenSynchronizationService service,
        Func<ScreenConfiguration?> captured,
        IScreenHubContext hub
    ) CreateServiceWithHub(TestDatabase db)
    {
        ScreenConfiguration? config = null;
        var hub = Substitute.For<IScreenHubContext>();
        hub.SendConfigurationUpdateAsync(
                Arg.Any<string>(),
                Arg.Do<ScreenConfiguration>(c => config = c)
            )
            .Returns(Task.CompletedTask);

        var assetSync = Substitute.For<IAssetSyncService>();
        assetSync.GetCampaignsToSyncAsync(Arg.Any<Guid>()).Returns(new List<CampaignSyncInfo>());

        var service = new ScreenSynchronizationService(
            db.Context,
            hub,
            assetSync,
            NullLogger<ScreenSynchronizationService>.Instance
        );
        return (service, () => config, hub);
    }

    [Fact]
    public async Task SyncScreen_WithNoActiveCampaign_FallsBackToDefaultCampaign()
    {
        using var db = new TestDatabase();
        var screen = NewScreen();
        var asset = new Asset
        {
            Name = "Default Asset",
            Type = AssetType.Image,
            Source = "/uploads/d.png",
        };
        var defaultCampaign = new Campaign
        {
            Name = "House Ads",
            IsDefault = true,
            IsEnabled = true,
        };
        defaultCampaign.CampaignAssets.Add(new CampaignAsset { Asset = asset, Position = 1 });
        db.AddScreen(screen);
        db.Context.Campaigns.Add(defaultCampaign);
        await db.Context.SaveChangesAsync();

        var (service, captured) = CreateService(db);
        await service.SyncScreenAsync(screen.Id);

        var config = captured();
        Assert.NotNull(config);
        Assert.Single(config!.Campaigns);
        Assert.Equal(defaultCampaign.Id, config.Campaigns[0].Id);
    }

    [Fact]
    public async Task SyncScreen_WithActiveAssignedCampaign_DoesNotUseDefault()
    {
        using var db = new TestDatabase();
        var screen = NewScreen();
        var asset = new Asset
        {
            Name = "Asset",
            Type = AssetType.Image,
            Source = "/uploads/a.png",
        };
        var assigned = new Campaign { Name = "Assigned", IsEnabled = true };
        assigned.CampaignAssets.Add(new CampaignAsset { Asset = asset, Position = 1 });
        assigned.CampaignAssignments.Add(new CampaignAssignment { Screen = screen });
        var defaultCampaign = new Campaign
        {
            Name = "House Ads",
            IsDefault = true,
            IsEnabled = true,
        };
        db.AddScreen(screen);
        db.Context.Campaigns.AddRange(assigned, defaultCampaign);
        await db.Context.SaveChangesAsync();

        var (service, captured) = CreateService(db);
        await service.SyncScreenAsync(screen.Id);

        var config = captured();
        Assert.NotNull(config);
        Assert.Single(config!.Campaigns);
        Assert.Equal(assigned.Id, config.Campaigns[0].Id);
    }

    [Fact]
    public async Task SyncScreen_WithDisabledDefaultCampaign_SendsNoCampaigns()
    {
        using var db = new TestDatabase();
        var screen = NewScreen();
        var defaultCampaign = new Campaign
        {
            Name = "House Ads",
            IsDefault = true,
            IsEnabled = false,
        };
        db.AddScreen(screen);
        db.Context.Campaigns.Add(defaultCampaign);
        await db.Context.SaveChangesAsync();

        var (service, captured) = CreateService(db);
        await service.SyncScreenAsync(screen.Id);

        var config = captured();
        Assert.NotNull(config);
        Assert.Empty(config!.Campaigns);
    }

    [Fact]
    public async Task SendCommand_ToConnectedScreen_DeliversToScreenUser()
    {
        using var db = new TestDatabase();
        var screen = NewScreen();
        db.AddScreen(screen);
        await db.Context.SaveChangesAsync();

        var (service, _, hub) = CreateServiceWithHub(db);
        var delivered = await service.SendCommandAsync(screen.Id, "restart");

        Assert.True(delivered);
        await hub.Received(1).SendCommandAsync(screen.UserId!, "restart");
    }

    [Fact]
    public async Task SendCommand_ToScreenWithoutUser_ReturnsFalse()
    {
        using var db = new TestDatabase();
        var screen = new Screen
        {
            Name = "Screen",
            Location = "Lobby",
            ScreenIdentifier = Guid.NewGuid().ToString("N")[..10],
            UserId = null,
            ApprovalStatus = ApprovalStatus.Approved,
        };
        db.AddScreen(screen);
        await db.Context.SaveChangesAsync();

        var (service, _, hub) = CreateServiceWithHub(db);
        var delivered = await service.SendCommandAsync(screen.Id, "restart");

        Assert.False(delivered);
        await hub.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<string>());
    }
}
