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
    ///     Determines whether the campaign is active (enabled and within its schedule) at the given UTC time.
    /// </summary>
    public bool IsActiveAt(DateTime utcNow) =>
        IsEnabled
        && (StartDateUtc is null || StartDateUtc.Value <= utcNow)
        && (EndDateUtc is null || EndDateUtc.Value >= utcNow);

    // Navigation properties
    public ICollection<CampaignAsset> CampaignAssets { get; set; } = [];
    public ICollection<CampaignAssignment> CampaignAssignments { get; set; } = [];
}
