namespace Mireya.Application.Constants;

/// <summary>
///     Identifiers for remote commands an administrator can push to a connected screen over SignalR.
/// </summary>
public static class ScreenCommands
{
    /// <summary>Restart playback of the current playlist from the first item.</summary>
    public const string RestartPlayback = "restart";

    /// <summary>Reload the currently displayed content (re-render the active asset).</summary>
    public const string ReloadContent = "reload";

    /// <summary>Briefly flash the screen so an operator can locate it within a fleet.</summary>
    public const string Identify = "identify";

    /// <summary>All commands a client is expected to understand.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        RestartPlayback,
        ReloadContent,
        Identify,
    };

    public static bool IsValid(string? command) => command != null && All.Contains(command);
}
