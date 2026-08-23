using Mireya.Application.Services.Audit;
using Mireya.Application.Services.Campaign;
using Mireya.Database.Models;
using NSubstitute;

namespace Mireya.Application.Tests;

public class CampaignSchedulingTests
{
    private static readonly DateTime Now = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AssignmentIsActiveAt_Disabled_ReturnsFalse()
    {
        var assignment = new CampaignAssignment { IsEnabled = false };
        Assert.False(assignment.IsActiveAt(Now));
    }

    [Fact]
    public void AssignmentIsActiveAt_EnabledWithoutDates_ReturnsTrue()
    {
        var assignment = new CampaignAssignment { IsEnabled = true };
        Assert.True(assignment.IsActiveAt(Now));
    }

    [Fact]
    public void AssignmentIsActiveAt_BeforeStart_ReturnsFalse()
    {
        var assignment = new CampaignAssignment { IsEnabled = true, StartDateUtc = Now.AddDays(1) };
        Assert.False(assignment.IsActiveAt(Now));
    }

    [Fact]
    public void AssignmentIsActiveAt_AfterEnd_ReturnsFalse()
    {
        var assignment = new CampaignAssignment { IsEnabled = true, EndDateUtc = Now.AddDays(-1) };
        Assert.False(assignment.IsActiveAt(Now));
    }

    [Fact]
    public async Task SetGlobalFallback_WithEndBeforeStart_Throws()
    {
        using var db = new TestDatabase();
        var campaign = new Campaign { Name = "Campaign" };
        db.Context.Campaigns.Add(campaign);
        await db.Context.SaveChangesAsync();
        var service = new CampaignService(
            db.Context,
            Substitute.For<Application.Services.IScreenSynchronizationService>(),
            Substitute.For<IAuditService>()
        );

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SetGlobalFallbackAsync(
                new CampaignAssignmentRequest(
                    campaign.Id,
                    StartDateUtc: Now,
                    EndDateUtc: Now.AddDays(-1)
                )
            )
        );
    }

    [Fact]
    public async Task SetGlobalFallback_PersistsSchedulingAndPriority()
    {
        using var db = new TestDatabase();
        var campaign = new Campaign { Name = "Campaign" };
        db.Context.Campaigns.Add(campaign);
        await db.Context.SaveChangesAsync();
        var service = new CampaignService(
            db.Context,
            Substitute.For<Application.Services.IScreenSynchronizationService>(),
            Substitute.For<IAuditService>()
        );

        var created = await service.SetGlobalFallbackAsync(
            new CampaignAssignmentRequest(
                campaign.Id,
                IsEnabled: false,
                StartDateUtc: Now,
                EndDateUtc: Now.AddDays(7),
                Priority: 42
            )
        );

        Assert.False(created.IsEnabled);
        Assert.Equal(Now, created.StartDateUtc);
        Assert.Equal(Now.AddDays(7), created.EndDateUtc);
        Assert.Equal(42, created.Priority);
        Assert.Equal(CampaignAssignmentTargetKind.GlobalFallback, created.TargetKind);
    }
}
