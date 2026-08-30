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

    /// <summary>
    ///     The device class this head is running on. Defaults to <see cref="Platform.FormFactor.Desktop" />
    ///     so tests and the XAML previewer get the pointer-density layout without extra setup.
    /// </summary>
    public FormFactor FormFactor { get; init; } = FormFactor.Desktop;

    /// <summary>Whether the primary input is a D-pad remote rather than a pointer or finger.</summary>
    public bool IsTelevision => FormFactor == FormFactor.Tv;

    /// <summary>Whether the primary input is touch, so hit targets and spacing must grow.</summary>
    public bool IsTouchFirst => FormFactor is FormFactor.Phone or FormFactor.Tablet;

    /// <summary>
    ///     Whether the device can be rotated by the user. Desktop windows and televisions
    ///     have a fixed orientation, so orientation-dependent layout hints are pointless there.
    /// </summary>
    public bool SupportsRotation => IsTouchFirst;

    /// <summary>The design-token density profile this head should load at startup.</summary>
    public UiDensity Density =>
        FormFactor switch
        {
            FormFactor.Tv => UiDensity.Television,
            FormFactor.Phone or FormFactor.Tablet => UiDensity.Touch,
            _ => UiDensity.Pointer,
        };
}
