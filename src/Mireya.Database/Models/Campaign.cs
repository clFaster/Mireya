using System.ComponentModel.DataAnnotations;

namespace Mireya.Database.Models;

/// <summary>
///     Represents a campaign - a planned collection of media rotations assigned to displays
/// </summary>
public class Campaign
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     When false, the campaign is never shown on screens regardless of its schedule.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     Optional UTC instant before which the campaign is not shown. Null means no start bound.
    /// </summary>
    public DateTime? StartDateUtc { get; set; }

    /// <summary>
    ///     Optional UTC instant after which the campaign is no longer shown. Null means no end bound.
    /// </summary>
    public DateTime? EndDateUtc { get; set; }

    /// <summary>
    ///     Relative ordering priority. Campaigns with a higher priority are played first on a screen.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    ///     When true, this campaign is the global fallback shown on any screen that has no other
    ///     active campaign assigned. At most one campaign should be marked as default.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    ///     Optional weekday recurrence as a bitmask over <see cref="DayOfWeek" />
    ///     (bit <c>1 &lt;&lt; (int)DayOfWeek</c>). Null or 0 means every day.
    /// </summary>
    public int? RecurrenceDaysMask { get; set; }

    /// <summary>
    ///     Optional daily start time (in <see cref="RecurrenceTimeZoneId" />). Requires
    ///     <see cref="DailyEndTime" /> to also be set. Null means no daily start bound.
    /// </summary>
    public TimeOnly? DailyStartTime { get; set; }

    /// <summary>
    ///     Optional daily end time (in <see cref="RecurrenceTimeZoneId" />). When earlier than
    ///     <see cref="DailyStartTime" /> the window is treated as spanning midnight.
    /// </summary>
    public TimeOnly? DailyEndTime { get; set; }

    /// <summary>
    ///     Time zone used to evaluate the weekday/daily-time recurrence. Null or empty means UTC.
    /// </summary>
    [MaxLength(100)]
    public string? RecurrenceTimeZoneId { get; set; }

    /// <summary>
    ///     Determines whether the campaign is active (enabled and within its schedule) at the given UTC time.
    /// </summary>
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

        if (hasDays && (RecurrenceDaysMask!.Value & (1 << (int)local.DayOfWeek)) == 0)
            return false;

        if (hasWindow && !IsWithinDailyWindow(TimeOnly.FromDateTime(local)))
            return false;

        return true;
    }

    private DateTime ConvertToRecurrenceZone(DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(RecurrenceTimeZoneId))
            return utcNow;
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(RecurrenceTimeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return utcNow;
        }
    }

    private bool IsWithinDailyWindow(TimeOnly nowLocal)
    {
        var start = DailyStartTime!.Value;
        var end = DailyEndTime!.Value;
        return start <= end
            ? nowLocal >= start && nowLocal < end
            : nowLocal >= start || nowLocal < end; // window spans midnight
    }

    // Navigation properties
    public ICollection<CampaignAsset> CampaignAssets { get; set; } = [];
    public ICollection<CampaignAssignment> CampaignAssignments { get; set; } = [];
}
