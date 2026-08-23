using Mireya.Application.Services.Campaign;

namespace Mireya.Application.Services.ScreenManagement;

/// <summary>
///     Response payload for screen details
/// </summary>
public class ScreenDetailsResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? ScreenIdentifier { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public int? ResolutionWidth { get; set; }
    public int? ResolutionHeight { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool ShufflePlayback { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
///     Response payload for screen details with assigned campaigns
/// </summary>
public class ScreenWithCampaignsResponse : ScreenDetailsResponse
{
    public List<CampaignAssignmentDetail> CampaignAssignments { get; set; } = [];
    public List<CampaignSummary> AllCampaigns { get; set; } = [];
}
