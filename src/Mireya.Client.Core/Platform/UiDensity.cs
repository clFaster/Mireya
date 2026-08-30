namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     Selects the design-token density profile that is merged over the base
///     <c>Styles/Spacing.axaml</c> and <c>Styles/Typography.axaml</c> dictionaries at
///     startup. Each profile retunes the same token keys, so views never need to know
///     which platform they are running on.
/// </summary>
public enum UiDensity
{
    /// <summary>Compact desktop density for precise pointer input at arm's length.</summary>
    Pointer,

    /// <summary>Roomier spacing and larger hit targets for finger input.</summary>
    Touch,

    /// <summary>10-foot density: large type, generous padding, oversized focus targets.</summary>
    Television,
}
