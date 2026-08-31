using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Mireya.Client.Avalonia.Platform;

namespace Mireya.Client.Avalonia.Views;

public partial class ScreenInfoPage : AdaptiveUserControl
{
    public ScreenInfoPage()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // The info cards can overflow the viewport (most notably on TV, where the
        // available height is small) and contain only non-focusable text, so a
        // D-pad remote has nothing to focus that would trigger Avalonia's
        // automatic bring-into-view scrolling. Handle Up/Down directly so the
        // rest of the screen info always stays reachable.
        if (InfoScrollViewer is null)
            return;

        switch (e.Key)
        {
            case Key.Down:
            case Key.PageDown:
                InfoScrollViewer.PageDown();
                e.Handled = true;
                break;
            case Key.Up:
            case Key.PageUp:
                InfoScrollViewer.PageUp();
                e.Handled = true;
                break;
        }
    }

    protected override void OnSizeClassChanged(SizeClass sizeClass)
    {
        if (InfoLayout is null || DeviceColumn is null || FooterActions is null || ChooseServerAction is null)
            return;

        var expanded = sizeClass == SizeClass.Expanded;
        InfoLayout.ColumnDefinitions.Clear();
        InfoLayout.RowDefinitions.Clear();
        FooterActions.ColumnDefinitions.Clear();
        FooterActions.RowDefinitions.Clear();

        if (expanded)
        {
            InfoLayout.ColumnDefinitions.Add(new ColumnDefinition(3, GridUnitType.Star));
            InfoLayout.ColumnDefinitions.Add(new ColumnDefinition(2, GridUnitType.Star));
            InfoLayout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(DeviceColumn, 0);
            Grid.SetColumn(DeviceColumn, 1);
            DeviceColumn.Margin = new global::Avalonia.Thickness(32, 0, 0, 0);

            FooterActions.ColumnDefinitions.Add(new ColumnDefinition(2, GridUnitType.Star));
            FooterActions.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            FooterActions.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(ChooseServerAction, 0);
            Grid.SetColumn(ChooseServerAction, 1);
            ChooseServerAction.Margin = new global::Avalonia.Thickness(12, 0, 0, 0);
        }
        else
        {
            InfoLayout.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            InfoLayout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            InfoLayout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(DeviceColumn, 1);
            Grid.SetColumn(DeviceColumn, 0);
            DeviceColumn.Margin = new global::Avalonia.Thickness(0, 24, 0, 0);

            FooterActions.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            FooterActions.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            FooterActions.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(ChooseServerAction, 1);
            Grid.SetColumn(ChooseServerAction, 0);
            ChooseServerAction.Margin = new global::Avalonia.Thickness(0, 12, 0, 0);
        }
    }
}
