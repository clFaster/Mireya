using Mireya.Database.Models;

namespace Mireya.Application.Tests;

public class CampaignRecurrenceTests
{
    private static CampaignAssignment EnabledAssignment() => new() { IsEnabled = true };

    private static int MaskFor(params DayOfWeek[] days)
    {
        var mask = 0;
        foreach (var day in days)
            mask |= 1 << (int)day;
        return mask;
    }

    [Fact]
    public void NoRecurrence_IsAlwaysActive()
    {
        var assignment = EnabledAssignment();
        Assert.True(assignment.IsActiveAt(new DateTime(2026, 1, 7, 3, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void DaysMask_ActiveOnlyOnMatchingDay()
    {
        var assignment = EnabledAssignment();
        assignment.RecurrenceDaysMask = MaskFor(DayOfWeek.Wednesday);

        Assert.True(assignment.IsActiveAt(new DateTime(2026, 1, 7, 12, 0, 0, DateTimeKind.Utc)));
        Assert.False(assignment.IsActiveAt(new DateTime(2026, 1, 8, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void DailyWindow_ActiveInsideAndInactiveOutside()
    {
        var assignment = EnabledAssignment();
        assignment.DailyStartTime = new TimeOnly(9, 0);
        assignment.DailyEndTime = new TimeOnly(17, 0);

        Assert.True(assignment.IsActiveAt(new DateTime(2026, 1, 7, 12, 0, 0, DateTimeKind.Utc)));
        Assert.False(assignment.IsActiveAt(new DateTime(2026, 1, 7, 8, 0, 0, DateTimeKind.Utc)));
        Assert.False(assignment.IsActiveAt(new DateTime(2026, 1, 7, 17, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void DailyWindow_SpanningMidnight_IsActiveOvernight()
    {
        var assignment = EnabledAssignment();
        assignment.DailyStartTime = new TimeOnly(22, 0);
        assignment.DailyEndTime = new TimeOnly(6, 0);

        Assert.True(assignment.IsActiveAt(new DateTime(2026, 1, 7, 23, 0, 0, DateTimeKind.Utc)));
        Assert.True(assignment.IsActiveAt(new DateTime(2026, 1, 7, 2, 0, 0, DateTimeKind.Utc)));
        Assert.False(assignment.IsActiveAt(new DateTime(2026, 1, 7, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void TimeZone_ShiftsWindowAndDayEvaluation()
    {
        var window = EnabledAssignment();
        window.DailyStartTime = new TimeOnly(9, 0);
        window.DailyEndTime = new TimeOnly(17, 0);
        window.RecurrenceTimeZoneId = "America/New_York";
        Assert.True(window.IsActiveAt(new DateTime(2026, 1, 7, 14, 0, 0, DateTimeKind.Utc)));
        Assert.False(window.IsActiveAt(new DateTime(2026, 1, 7, 13, 0, 0, DateTimeKind.Utc)));

        var day = EnabledAssignment();
        day.RecurrenceDaysMask = MaskFor(DayOfWeek.Tuesday);
        day.RecurrenceTimeZoneId = "America/New_York";
        Assert.True(day.IsActiveAt(new DateTime(2026, 1, 7, 3, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void InvalidTimeZone_FallsBackToUtc()
    {
        var assignment = EnabledAssignment();
        assignment.DailyStartTime = new TimeOnly(9, 0);
        assignment.DailyEndTime = new TimeOnly(17, 0);
        assignment.RecurrenceTimeZoneId = "Not/A_Zone";

        Assert.True(assignment.IsActiveAt(new DateTime(2026, 1, 7, 12, 0, 0, DateTimeKind.Utc)));
    }
}
