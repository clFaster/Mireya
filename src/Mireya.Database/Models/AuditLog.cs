using System.ComponentModel.DataAnnotations;

namespace Mireya.Database.Models;

/// <summary>
///     An immutable record of a mutating admin action (who did what, to which entity, when).
/// </summary>
public class AuditLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    ///     UTC instant the action occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Identity user id of the actor, when known. Null for unauthenticated/system actions.
    /// </summary>
    [MaxLength(450)]
    public string? ActorUserId { get; set; }

    /// <summary>
    ///     Human-readable actor name (username or email) captured at the time of the action.
    /// </summary>
    [MaxLength(256)]
    public string? ActorName { get; set; }

    /// <summary>
    ///     The action performed, e.g. "Created", "Updated", "Deleted", "Approved".
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    ///     The kind of entity affected, e.g. "Campaign", "Asset", "Screen".
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    ///     Identifier of the affected entity (typically its Guid), when applicable.
    /// </summary>
    [MaxLength(100)]
    public string? EntityId { get; set; }

    /// <summary>
    ///     Optional short, human-readable summary of the change.
    /// </summary>
    [MaxLength(2000)]
    public string? Summary { get; set; }
}
