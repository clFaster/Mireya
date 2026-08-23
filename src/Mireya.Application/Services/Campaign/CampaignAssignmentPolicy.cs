using Mireya.Database.Models;

namespace Mireya.Application.Services.Campaign;

internal static class CampaignAssignmentPolicy
{
    public static void Validate(CampaignAssignmentRequest request)
    {
        if (
            request.StartDateUtc.HasValue
            && request.EndDateUtc.HasValue
            && request.EndDateUtc.Value < request.StartDateUtc.Value
        )
            throw new ArgumentException(
                "Assignment end date must not be earlier than its start date"
            );

        if (request.DailyStartTime.HasValue != request.DailyEndTime.HasValue)
            throw new ArgumentException(
                "Daily start and end time must both be set or both be empty"
            );

        if (string.IsNullOrWhiteSpace(request.RecurrenceTimeZoneId))
            return;

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(request.RecurrenceTimeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException($"Unknown time zone '{request.RecurrenceTimeZoneId}'");
        }
    }

    public static void Apply(CampaignAssignment assignment, CampaignAssignmentRequest request)
    {
        Validate(request);
        assignment.IsEnabled = request.IsEnabled;
        assignment.StartDateUtc = request.StartDateUtc;
        assignment.EndDateUtc = request.EndDateUtc;
        assignment.Priority = request.Priority;
        assignment.RecurrenceDaysMask = NormalizeDaysMask(request.RecurrenceDaysMask);
        assignment.DailyStartTime = request.DailyStartTime;
        assignment.DailyEndTime = request.DailyEndTime;
        assignment.RecurrenceTimeZoneId = string.IsNullOrWhiteSpace(request.RecurrenceTimeZoneId)
            ? null
            : request.RecurrenceTimeZoneId;
        assignment.UpdatedAt = DateTime.UtcNow;
    }

    public static CampaignAssignmentDetail ToDetail(
        CampaignAssignment assignment,
        DateTime utcNow
    ) =>
        new(
            assignment.Id,
            assignment.CampaignId,
            assignment.Campaign.Name,
            assignment.TargetKind,
            assignment.ScreenId,
            assignment.Screen?.Name,
            assignment.Screen?.Location,
            assignment.IsEnabled,
            assignment.StartDateUtc,
            assignment.EndDateUtc,
            assignment.Priority,
            assignment.RecurrenceDaysMask,
            assignment.DailyStartTime,
            assignment.DailyEndTime,
            assignment.RecurrenceTimeZoneId,
            assignment.IsActiveAt(utcNow)
        );

    private static int? NormalizeDaysMask(int? mask) =>
        mask is null or 0 or 0b111_1111 ? null : mask & 0b111_1111;
}
