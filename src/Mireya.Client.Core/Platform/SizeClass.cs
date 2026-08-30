namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     The width band a surface currently occupies. Unlike <see cref="FormFactor" /> this
///     changes at runtime when a desktop window is resized or a handheld device rotates,
///     and it is what views should branch on when choosing between a single-column and a
///     multi-column layout.
/// </summary>
public enum SizeClass
{
    /// <summary>Single column. Phones in portrait and very narrow desktop windows.</summary>
    Compact,

    /// <summary>Two columns are comfortable. Phones in landscape, small tablets, default desktop windows.</summary>
    Medium,

    /// <summary>Wide layout with a persistent navigation rail. Tablets, maximised desktop windows, TVs.</summary>
    Expanded,
}
