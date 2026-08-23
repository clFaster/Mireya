using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Mireya.Client.Avalonia.ViewModels;

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
        Focusable = true;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.CancelAutoStart();

        var handled = e.Key switch
        {
            Key.Enter or Key.Space => viewModel.TryToggleScreenInfo(),
            Key.Escape => viewModel.TryCloseScreenInfo(),
            _ => false,
        };

        if (handled)
            e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.CancelAutoStart();
        if (viewModel.TryOpenScreenInfo())
            e.Handled = true;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
