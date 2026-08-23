using System.ComponentModel.DataAnnotations;

namespace Mireya.Database.Models;

/// <summary>
///     Represents a physical digital-signage screen.
/// </summary>
public class Screen
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(100)]
    public string Location { get; set; } = string.Empty;

    /// <summary>
    ///     Short, human-readable pairing identifier that uniquely identifies the screen.
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string ScreenIdentifier { get; init; } = string.Empty;

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;

    /// <summary>
    ///     Identity user used by the screen client. A user can belong to at most one screen.
    /// </summary>
    [MaxLength(64)]
    public string? UserId { get; init; }

    public int? ResolutionWidth { get; init; }

    public int? ResolutionHeight { get; init; }

    public bool IsActive { get; set; }

    public DateTime? LastSeenAt { get; set; }

    /// <summary>
    ///     UTC instant at which an alert was sent for the current outage.
    /// </summary>
    public DateTime? OfflineAlertedAt { get; set; }

    public bool ShufflePlayback { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CampaignAssignment> CampaignAssignments { get; init; } = [];
}
