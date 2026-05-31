using Mireya.Database.Models;

namespace Mireya.Application.Tests;

public class CampaignRecurrenceTests
{
    private static Campaign EnabledCampaign() => new()
    {
        Name = "Recurring",
        IsEnabled = true,
    };

    private static int MaskFor(params DayOfWeek[] days)
    {
        var mask = 0;
        foreach (var d in days)
            mask |= 1 << (int)d;
        return mask;
    }

    [Fact]
    public void NoRecurrence_IsAlwaysActive()
    {
        var campaign = EnabledCampaign();

        // A Wednesday, 03:00 UTC.
        Assert.True(campaign.IsActiveAt(new DateTime(2026, 1, 7, 3, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void DaysMask_ActiveOnMatchingDay()
    {
        var campaign = EnabledCampaign();
        campaign.RecurrenceDaysMask = MaskFor(DayOfWeek.Wednesday);

        // 2026-01-07 is a Wednesday.
        Assert.True(campaign.IsActiveAt(new DateTime(2026, 1, 7, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void DaysMask_InactiveOnNonMatchingDay()
    {
        var campaign = EnabledCampaign();
        campaign.RecurrenceDaysMask = MaskFor(DayOfWeek.Wednesday);

        // 2026-01-08 is a Thursday.
        Assert.False(campaign.IsActiveAt(new DateTime(2026, 1, 8, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void DailyWindow_ActiveInsideAndInactiveOutside()
    {
        var campaign = EnabledCampaign();
        campaign.DailyStartTime = new TimeOnly(9, 0);
        campaign.DailyEndTime = new TimeOnly(17, 0);

        Assert.True(campaign.IsActiveAt(new DateTime(2026, 1, 7, 12, 0, 0, DateTimeKind.Utc)));
        Assert.False(campaign.IsActiveAt(new DateTime(2026, 1, 7, 8, 0, 0, DateTimeKind.Utc)));
        Assert.False(campaign.IsActiveAt(new DateTime(2026, 1, 7, 17, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void DailyWindow_SpanningMidnight_IsActiveOvernight()
    {
        var campaign = EnabledCampaign();
        campaign.DailyStartTime = new TimeOnly(22, 0);
        campaign.DailyEndTime = new TimeOnly(6, 0);

        Assert.True(campaign.IsActiveAt(new DateTime(2026, 1, 7, 23, 0, 0, DateTimeKind.Utc)));
        Assert.True(campaign.IsActiveAt(new DateTime(2026, 1, 7, 2, 0, 0, DateTimeKind.Utc)));
        Assert.False(campaign.IsActiveAt(new DateTime(2026, 1, 7, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void TimeZone_ShiftsWindowEvaluation()
    {
        var campaign = EnabledCampaign();
        campaign.DailyStartTime = new TimeOnly(9, 0);
        campaign.DailyEndTime = new TimeOnly(17, 0);
        campaign.RecurrenceTimeZoneId = "America/New_York";

        // 14:00 UTC == 09:00 EST (UTC-5) in January -> inside the window.
        Assert.True(campaign.IsActiveAt(new DateTime(2026, 1, 7, 14, 0, 0, DateTimeKind.Utc)));
        // 13:00 UTC == 08:00 EST -> before the window.
        Assert.False(campaign.IsActiveAt(new DateTime(2026, 1, 7, 13, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void TimeZone_ShiftsDayEvaluation()
    {
        var campaign = EnabledCampaign();
        campaign.RecurrenceDaysMask = MaskFor(DayOfWeek.Tuesday);
        campaign.RecurrenceTimeZoneId = "America/New_York";

        // 2026-01-07 03:00 UTC is still Tuesday 22:00 in New York (UTC-5).
        Assert.True(campaign.IsActiveAt(new DateTime(2026, 1, 7, 3, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void InvalidTimeZone_FallsBackToUtc()
    {
        var campaign = EnabledCampaign();
        campaign.DailyStartTime = new TimeOnly(9, 0);
        campaign.DailyEndTime = new TimeOnly(17, 0);
        campaign.RecurrenceTimeZoneId = "Not/A_Zone";

        // Falls back to UTC, so 12:00 UTC is inside the window.
        Assert.True(campaign.IsActiveAt(new DateTime(2026, 1, 7, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Disabled_IsNeverActive()
    {
        var campaign = EnabledCampaign();
        campaign.IsEnabled = false;
        campaign.RecurrenceDaysMask = MaskFor(DayOfWeek.Wednesday);

        Assert.False(campaign.IsActiveAt(new DateTime(2026, 1, 7, 12, 0, 0, DateTimeKind.Utc)));
    }
}
