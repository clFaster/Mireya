using System.ComponentModel.DataAnnotations;

namespace Mireya.Database.Models;

/// <summary>
///     Associates a campaign with a zone. Every screen that is a member of the zone effectively
///     plays this campaign in addition to its directly assigned campaigns.
/// </summary>
public class ZoneCampaign
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ZoneId { get; set; }

    [Required]
    public Guid CampaignId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Zone Zone { get; set; } = null!;
    public Campaign Campaign { get; set; } = null!;
}
