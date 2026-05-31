using Mireya.Application.Services.Audit;
using Mireya.Application.Services.Campaign;
using Mireya.Database.Models;
using NSubstitute;

namespace Mireya.Application.Tests;

public class CampaignSchedulingTests
{
    private static readonly DateTime Now = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsActiveAt_Disabled_ReturnsFalse()
    {
        var campaign = new Campaign { IsEnabled = false };
        Assert.False(campaign.IsActiveAt(Now));
    }

    [Fact]
    public void IsActiveAt_EnabledWithoutDates_ReturnsTrue()
    {
        var campaign = new Campaign { IsEnabled = true };
        Assert.True(campaign.IsActiveAt(Now));
    }

    [Fact]
    public void IsActiveAt_BeforeStart_ReturnsFalse()
    {
        var campaign = new Campaign { IsEnabled = true, StartDateUtc = Now.AddDays(1) };
        Assert.False(campaign.IsActiveAt(Now));
    }

    [Fact]
    public void IsActiveAt_AfterEnd_ReturnsFalse()
    {
        var campaign = new Campaign { IsEnabled = true, EndDateUtc = Now.AddDays(-1) };
        Assert.False(campaign.IsActiveAt(Now));
    }

    [Fact]
    public void IsActiveAt_WithinWindow_ReturnsTrue()
    {
        var campaign = new Campaign
        {
            IsEnabled = true,
            StartDateUtc = Now.AddDays(-1),
            EndDateUtc = Now.AddDays(1),
        };
        Assert.True(campaign.IsActiveAt(Now));
    }

    [Fact]
    public async Task CreateCampaign_WithEndBeforeStart_Throws()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<Application.Services.IScreenSynchronizationService>();

        var asset = new Asset { Name = "A", Type = AssetType.Image, Source = "/uploads/a.png" };
        db.Context.Assets.Add(asset);
        await db.Context.SaveChangesAsync();

        var service = new CampaignService(db.Context, sync, Substitute.For<IAuditService>());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCampaignAsync(
            new CreateCampaignRequest(
                "Bad schedule", null,
                [new CampaignAssetDto(asset.Id, 1, 5)],
                [],
                IsEnabled: true,
                StartDateUtc: Now,
                EndDateUtc: Now.AddDays(-1))));
    }

    [Fact]
    public async Task CreateCampaign_PersistsSchedulingFields()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<Application.Services.IScreenSynchronizationService>();

        var asset = new Asset { Name = "A", Type = AssetType.Image, Source = "/uploads/a.png" };
        db.Context.Assets.Add(asset);
        await db.Context.SaveChangesAsync();

        var service = new CampaignService(db.Context, sync, Substitute.For<IAuditService>());
        var created = await service.CreateCampaignAsync(new CreateCampaignRequest(
            "Scheduled", null,
            [new CampaignAssetDto(asset.Id, 1, 5)],
            [],
            IsEnabled: false,
            StartDateUtc: Now,
            EndDateUtc: Now.AddDays(7)));

        Assert.False(created.IsEnabled);
        Assert.Equal(Now, created.StartDateUtc);
        Assert.Equal(Now.AddDays(7), created.EndDateUtc);
    }

    [Fact]
    public async Task CreateCampaign_PersistsPriority()
    {
        using var db = new TestDatabase();
        var sync = Substitute.For<Application.Services.IScreenSynchronizationService>();

        var asset = new Asset { Name = "A", Type = AssetType.Image, Source = "/uploads/a.png" };
        db.Context.Assets.Add(asset);
        await db.Context.SaveChangesAsync();

        var service = new CampaignService(db.Context, sync, Substitute.For<IAuditService>());
        var created = await service.CreateCampaignAsync(new CreateCampaignRequest(
            "Prioritised", null,
            [new CampaignAssetDto(asset.Id, 1, 5)],
            [],
            Priority: 42));

        Assert.Equal(42, created.Priority);
    }
}
