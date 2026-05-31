using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mireya.Client.Avalonia.Views;

/// <summary>
///     The shared root content for the application. Hosted directly by single-view
///     platform heads (Android TV, …) and wrapped by <see cref="MainWindow" /> on
///     classic desktop heads so both present the same content from a single source.
/// </summary>
public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
