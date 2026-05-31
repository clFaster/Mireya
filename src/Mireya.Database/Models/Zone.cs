using System.ComponentModel.DataAnnotations;

namespace Mireya.Database.Models;

/// <summary>
///     A named group of screens. Campaigns assigned to a zone automatically apply to every screen
///     that is a member of the zone, so fleets can be managed together rather than screen by screen.
/// </summary>
public class Zone
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Display> Displays { get; set; } = [];
    public ICollection<ZoneCampaign> ZoneCampaigns { get; set; } = [];
}
