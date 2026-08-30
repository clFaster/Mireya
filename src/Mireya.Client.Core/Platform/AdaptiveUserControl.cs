using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     Shared root for setup and diagnostic surfaces that need to react to both the
///     current width and the active input model. It exposes those decisions as Avalonia
///     style classes so XAML remains declarative.
/// </summary>
public class AdaptiveUserControl : UserControl
{
    private SizeClass? _sizeClass;

    private FormFactor FormFactor =>
        App.Services?.GetService<ClientPlatformCapabilities>()?.FormFactor
        ?? FormFactor.Desktop;

    public AdaptiveUserControl()
    {
        Loaded += (_, _) => UpdateAdaptiveClasses(Bounds.Width);
        SizeChanged += (_, args) => UpdateAdaptiveClasses(args.NewSize.Width);
    }

    private void UpdateAdaptiveClasses(double width)
    {
        var formFactor = FormFactor;
        var sizeClass = LayoutBreakpoints.Resolve(width, formFactor);

        Classes.Set("compact", sizeClass == SizeClass.Compact);
        Classes.Set("medium", sizeClass == SizeClass.Medium);
        Classes.Set("expanded", sizeClass == SizeClass.Expanded);
        Classes.Set("desktop", formFactor == FormFactor.Desktop);
        Classes.Set("touch", formFactor is FormFactor.Phone or FormFactor.Tablet);
        Classes.Set("tv", formFactor == FormFactor.Tv);

        if (_sizeClass == sizeClass)
            return;

        _sizeClass = sizeClass;
        OnSizeClassChanged(sizeClass);
    }

    /// <summary>
    ///     Called after the width crosses a breakpoint. Derived views use this only for
    ///     layout properties that Avalonia does not expose as styled properties, such as
    ///     a Grid's row and column definition collections.
    /// </summary>
    protected virtual void OnSizeClassChanged(SizeClass sizeClass) { }
}
