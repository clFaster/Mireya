using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Mireya.Database.Models;

/// <summary>
///     Represents the assignment of a campaign to a screen
/// </summary>
public class CampaignAssignment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CampaignId { get; set; }

    public Guid? ScreenId { get; set; }

    public CampaignAssignmentTargetKind TargetKind { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime? StartDateUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }

    public int Priority { get; set; }

    public int? RecurrenceDaysMask { get; set; }

    public TimeOnly? DailyStartTime { get; set; }

    public TimeOnly? DailyEndTime { get; set; }

    [MaxLength(100)]
    public string? RecurrenceTimeZoneId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActiveAt(DateTime utcNow) =>
        IsEnabled
        && (StartDateUtc is null || StartDateUtc.Value <= utcNow)
        && (EndDateUtc is null || EndDateUtc.Value >= utcNow)
        && IsWithinRecurrence(utcNow);

    private bool IsWithinRecurrence(DateTime utcNow)
    {
        var hasDays = RecurrenceDaysMask is > 0;
        var hasWindow = DailyStartTime.HasValue && DailyEndTime.HasValue;
        if (!hasDays && !hasWindow)
            return true;

        var local = ConvertToRecurrenceZone(utcNow);
        if (!IsWithinRecurrenceDays(local.DayOfWeek))
            return false;

        return IsWithinConfiguredDailyWindow(TimeOnly.FromDateTime(local));
    }

    private DateTime ConvertToRecurrenceZone(DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(RecurrenceTimeZoneId))
            return utcNow;

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(RecurrenceTimeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
                timeZone
            );
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return utcNow;
        }
    }

    private bool IsWithinRecurrenceDays(DayOfWeek dayOfWeek) =>
        RecurrenceDaysMask is not > 0 || (RecurrenceDaysMask.Value & (1 << (int)dayOfWeek)) != 0;

    private bool IsWithinConfiguredDailyWindow(TimeOnly nowLocal)
    {
        if (DailyStartTime is not TimeOnly start || DailyEndTime is not TimeOnly end)
            return true;

        return start <= end
            ? nowLocal >= start && nowLocal < end
            : nowLocal >= start || nowLocal < end;
    }

    // Navigation properties
    [AllowNull]
    public Campaign Campaign { get; set; } = null;

    [AllowNull]
    public Screen? Screen { get; set; }
}
