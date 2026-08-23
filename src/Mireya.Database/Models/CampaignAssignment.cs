using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Mireya.Database.Models;

/// <summary>
///     Represents the assignment of a campaign to a screen
/// </summary>
public class CampaignAssignment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CampaignId { get; set; }

    [Required]
    public Guid ScreenId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime? StartDateUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActiveAt(DateTime utcNow) =>
        IsEnabled
        && (StartDateUtc is null || StartDateUtc.Value <= utcNow)
        && (EndDateUtc is null || EndDateUtc.Value >= utcNow);

    // Navigation properties
    [AllowNull]
    public Campaign Campaign { get; set; } = null;

    [AllowNull]
    public Screen Screen { get; set; } = null;
}
