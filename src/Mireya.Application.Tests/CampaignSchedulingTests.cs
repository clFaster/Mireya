using Mireya.Database.Models;

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
}
