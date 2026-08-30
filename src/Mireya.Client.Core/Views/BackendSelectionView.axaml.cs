using Avalonia.Controls;
using Mireya.Client.Avalonia.Platform;

namespace Mireya.Client.Avalonia.Views;

public partial class BackendSelectionView : AdaptiveUserControl
{
    public BackendSelectionView()
    {
        InitializeComponent();
    }

    protected override void OnSizeClassChanged(SizeClass sizeClass)
    {
        if (
            SetupLayout is null
            || SetupPrimary is null
            || SetupSecondary is null
            || HeroLayout is null
            || HeroMark is null
            || HeroCopy is null
        )
            return;

        var expanded = sizeClass == SizeClass.Expanded;
        var compact = sizeClass == SizeClass.Compact;
        SetupLayout.ColumnDefinitions.Clear();
        SetupLayout.RowDefinitions.Clear();

        HeroLayout.ColumnDefinitions.Clear();
        HeroLayout.RowDefinitions.Clear();
        if (compact)
        {
            HeroLayout.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            HeroLayout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            HeroLayout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(HeroMark, 0);
            Grid.SetColumn(HeroMark, 0);
            Grid.SetRow(HeroCopy, 1);
            Grid.SetColumn(HeroCopy, 0);
            HeroCopy.Margin = new global::Avalonia.Thickness(0, 16, 0, 0);
        }
        else
        {
            HeroLayout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            HeroLayout.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            HeroLayout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(HeroMark, 0);
            Grid.SetColumn(HeroMark, 0);
            Grid.SetRow(HeroCopy, 0);
            Grid.SetColumn(HeroCopy, 1);
            HeroCopy.Margin = new global::Avalonia.Thickness(0);
        }

        if (expanded)
        {
            SetupLayout.ColumnDefinitions.Add(new ColumnDefinition(2, GridUnitType.Star));
            SetupLayout.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            SetupLayout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(SetupSecondary, 0);
            Grid.SetColumn(SetupSecondary, 1);
            SetupSecondary.Margin = new global::Avalonia.Thickness(32, 0, 0, 0);
        }
        else
        {
            SetupLayout.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            SetupLayout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SetupLayout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(SetupSecondary, 1);
            Grid.SetColumn(SetupSecondary, 0);
            SetupSecondary.Margin = new global::Avalonia.Thickness(0, 24, 0, 0);
        }
    }
}
