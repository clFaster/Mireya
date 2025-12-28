using System.ComponentModel.DataAnnotations;

namespace Mireya.Database.Models;

/// <summary>
///     Represents the assignment of an asset to a campaign with position and optional duration override
/// </summary>
public class CampaignAsset
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CampaignId { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    /// <summary>
    ///     Position/order of the asset within the campaign (1, 2, 3...)
    /// </summary>
    [Required]
    public int Position { get; set; }

    /// <summary>
    ///     Optional duration override in seconds for Image/Website assets.
    ///     Null means use asset's intrinsic duration (for videos) or default (10 seconds)
    /// </summary>
    public int? DurationSeconds { get; set; }

    // Navigation properties
    public Campaign Campaign { get; set; } = null!;
    public Asset Asset { get; set; } = null!;
}
