using System.ComponentModel.DataAnnotations;

namespace Mireya.Database.Models;

/// <summary>
/// Represents a campaign - a planned collection of media rotations assigned to displays
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

    // Navigation properties
    public ICollection<CampaignAsset> CampaignAssets { get; set; } = new List<CampaignAsset>();
    public ICollection<CampaignAssignment> CampaignAssignments { get; set; } = new List<CampaignAssignment>();
}
