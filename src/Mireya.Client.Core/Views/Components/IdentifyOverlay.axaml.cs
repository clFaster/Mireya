using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mireya.Client.Avalonia.Views.Components;

/// <summary>
///     Shows the remote identify flash inline and in a floating window over native
///     video and website surfaces.
/// </summary>
public partial class IdentifyOverlay : UserControl
{
    public IdentifyOverlay()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
