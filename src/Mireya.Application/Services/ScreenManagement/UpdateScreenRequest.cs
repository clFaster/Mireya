namespace Mireya.Application.Services.ScreenManagement;

/// <summary>
///     Request payload for updating screen details
/// </summary>
public class UpdateScreenRequest
{
    /// <summary>
    ///     Screen name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Screen description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Screen location
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    ///     When set, toggles randomised (shuffle) playback order on the screen.
    /// </summary>
    public bool? ShufflePlayback { get; set; }

    /// <summary>
    ///     When true, the screen's zone membership is updated to <see cref="ZoneId" />
    ///     (which may be null to remove the screen from any zone).
    /// </summary>
    public bool ZoneAssignmentProvided { get; set; }

    /// <summary>
    ///     Zone the screen should belong to. Only applied when <see cref="ZoneAssignmentProvided" />
    ///     is true. Null removes the screen from its current zone.
    /// </summary>
    public Guid? ZoneId { get; set; }
}
