namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     The device class the client is running on. It is supplied once at startup by the
///     active platform head and never changes for the lifetime of the process, so it
///     describes the <em>input model and viewing distance</em> rather than the current
///     window size. Use <see cref="SizeClass" /> for layout decisions that must react to
///     resizing or rotation.
/// </summary>
public enum FormFactor
{
    /// <summary>Windowed desktop client driven by mouse and keyboard.</summary>
    Desktop,

    /// <summary>Handheld Android device, touch first, frequently used in portrait.</summary>
    Phone,

    /// <summary>Large-screen Android device, touch first, usually landscape.</summary>
    Tablet,

    /// <summary>Android TV / set-top box driven by a D-pad remote at 10-foot distance.</summary>
    Tv,
}
