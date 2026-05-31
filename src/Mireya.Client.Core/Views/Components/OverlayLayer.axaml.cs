using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mireya.Client.Avalonia.Views.Components;

/// <summary>
///     Composes the on-screen overlays (status panel + remote identify flash) into a
///     single reusable layer. It is hosted both inline in <c>ContentDisplayView</c> (for
///     image / idle content) and inside a floating top-most window that paints over the
///     native video (LibVLC) and website (WebView2) surfaces, which otherwise cover
///     ordinary Avalonia visuals (the "airspace" problem). See UA9 / UA10.
/// </summary>
public partial class OverlayLayer : UserControl
{
    public OverlayLayer()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
