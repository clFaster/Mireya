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
}
