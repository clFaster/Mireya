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
    }

    public static void Apply(CampaignAssignment assignment, CampaignAssignmentRequest request)
    {
        Validate(request);
        assignment.IsEnabled = request.IsEnabled;
        assignment.StartDateUtc = request.StartDateUtc;
        assignment.EndDateUtc = request.EndDateUtc;
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
            assignment.ScreenId,
            assignment.Screen.Name,
            assignment.Screen.Location,
            assignment.IsEnabled,
            assignment.StartDateUtc,
            assignment.EndDateUtc,
            assignment.IsActiveAt(utcNow)
        );
}
