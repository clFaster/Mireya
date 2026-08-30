namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     How the current shared surface wants the native window presented.
/// </summary>
public enum DisplayPresentation
{
    /// <summary>
    ///     Setup, settings, and diagnostics surfaces. The device may follow its natural
    ///     orientation and the system chrome stays reachable so a handheld user can leave
    ///     the app, read notifications, and use gesture navigation.
    /// </summary>
    Interactive,

    /// <summary>
    ///     Asset playback. Signage content is authored landscape, so the surface is pinned
    ///     to landscape, the system bars are hidden, and the screen is kept awake.
    /// </summary>
    Playback,
}

/// <summary>
///     Lets shared navigation state drive native window presentation without the shared
///     code referencing any platform API. Heads that have nothing to configure — the
///     desktop window, tests, and the XAML previewer — use
///     <see cref="NoopDisplayPresentationController" />.
/// </summary>
public interface IDisplayPresentationController
{
    /// <summary>The presentation currently applied to the native surface.</summary>
    DisplayPresentation Current { get; }

    /// <summary>
    ///     Applies the requested presentation. Implementations must be idempotent: shared
    ///     code re-applies the current presentation whenever navigation state changes.
    /// </summary>
    void Apply(DisplayPresentation presentation);
}

/// <summary>
///     Records the requested presentation and does nothing else. Used by heads whose
///     window orientation and chrome are not theirs to control.
/// </summary>
public sealed class NoopDisplayPresentationController : IDisplayPresentationController
{
    public DisplayPresentation Current { get; private set; } = DisplayPresentation.Interactive;

    public void Apply(DisplayPresentation presentation) => Current = presentation;
}
