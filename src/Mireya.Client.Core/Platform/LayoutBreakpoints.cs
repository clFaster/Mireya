namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     Resolves the responsive <see cref="SizeClass" /> for a surface. Kept as pure
///     functions so the breakpoints are unit-testable without a rendering surface, and so
///     every adaptive view agrees on where the layout switches.
/// </summary>
public static class LayoutBreakpoints
{
    /// <summary>Below this width only a single content column fits comfortably.</summary>
    public const double MediumMinWidth = 640;

    /// <summary>At or above this width a persistent navigation rail plus content fits.</summary>
    public const double ExpandedMinWidth = 1008;

    /// <summary>
    ///     Maps a raw surface width in device-independent pixels onto a size class.
    /// </summary>
    public static SizeClass FromWidth(double width)
    {
        if (double.IsNaN(width) || width < MediumMinWidth)
            return SizeClass.Compact;

        return width < ExpandedMinWidth ? SizeClass.Medium : SizeClass.Expanded;
    }

    /// <summary>
    ///     Maps a surface width onto a size class, honouring form-factor overrides.
    ///     Televisions always use the expanded layout: a 1080p panel reports roughly
    ///     960 device-independent pixels, which would otherwise be treated as a phone in
    ///     landscape even though it is viewed from across the room.
    /// </summary>
    public static SizeClass Resolve(double width, FormFactor formFactor) =>
        formFactor == FormFactor.Tv ? SizeClass.Expanded : FromWidth(width);
}
