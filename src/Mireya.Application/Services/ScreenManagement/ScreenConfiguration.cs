using Mireya.Application.Services.Campaign;

namespace Mireya.Application.Services.ScreenManagement;

public class ScreenConfiguration
{
    public Guid DisplayId { get; set; }
    public required string ScreenName { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public required string ApprovalStatus { get; set; }
    public int? ResolutionWidth { get; set; }
    public int? ResolutionHeight { get; set; }
    public bool ShufflePlayback { get; set; }
    public List<CampaignDetail> Campaigns { get; set; } = [];
}
