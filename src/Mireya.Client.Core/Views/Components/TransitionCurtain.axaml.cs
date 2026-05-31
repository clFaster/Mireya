using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mireya.Client.Avalonia.Views.Components;

/// <summary>
///     A full-screen black layer used to mask the flash between assets (a dip-to-black
///     crossfade). Its opacity is driven by <c>ContentDisplayViewModel.TransitionCurtainOpacity</c>.
///     It is hosted inside a floating top-most transparent window (see
///     <c>ContentDisplayView</c>) so it covers the native video (LibVLC) and website
///     (WebView2) surfaces, which otherwise paint over ordinary Avalonia visuals (the
///     "airspace" problem).
/// </summary>
public partial class TransitionCurtain : UserControl
{
    public TransitionCurtain()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
