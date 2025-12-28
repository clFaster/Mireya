using System.ComponentModel.DataAnnotations;

namespace Mireya.Database.Models;

/// <summary>
///     Represents a digital signage Screen
/// </summary>
public class Display
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
    ///     Screen identifier which uniquely identifies the display device
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string ScreenIdentifier { get; init; } = string.Empty;

    /// <summary>
    ///     Approval status of the display
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;

    /// <summary>
    ///     User ID of the associated user account (created upon approval)
    /// </summary>
    [MaxLength(64)]
    public string? UserId { get; init; }

    /// <summary>
    ///     Screen resolution width in pixels
    /// </summary>
    public int? ResolutionWidth { get; init; }

    /// <summary>
    ///     Screen resolution height in pixels
    /// </summary>
    public int? ResolutionHeight { get; init; }

    /// <summary>
    ///     Indicates if the display is currently online or offline
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Last time the display checked in or was seen online
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<CampaignAssignment> CampaignAssignments { get; init; } = [];
}
