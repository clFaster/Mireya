namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     Describes client features supplied by the active platform head.
/// </summary>
public sealed class ClientPlatformCapabilities
{
    /// <summary>
    ///     Whether the client has a window whose fullscreen state can be configured.
    /// </summary>
    public bool SupportsFullscreen { get; init; }
}
