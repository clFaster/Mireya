using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Mireya.Client.Avalonia.Views.Components;

/// <summary>
/// Reusable "About Mireya" branding block. Colors and callout copy are styled properties
/// so each host screen can match its own palette instead of inheriting a fixed theme.
/// Defaults match the ScreenInfoPage indigo palette.
/// </summary>
public partial class AboutMireyaPanel : UserControl
{
    private static readonly Uri GitHubUri = new("https://github.com/clFaster/Mireya");

    public static readonly StyledProperty<IBrush> AccentBrushProperty =
        AvaloniaProperty.Register<AboutMireyaPanel, IBrush>(nameof(AccentBrush), Brush.Parse("#8C9EFF"));

    public static readonly StyledProperty<IBrush> PanelBackgroundProperty =
        AvaloniaProperty.Register<AboutMireyaPanel, IBrush>(nameof(PanelBackground), Brush.Parse("#171A2A"));

    public static readonly StyledProperty<IBrush> PanelBorderBrushProperty =
        AvaloniaProperty.Register<AboutMireyaPanel, IBrush>(nameof(PanelBorderBrush), Brush.Parse("#2C3150"));

    public static readonly StyledProperty<IBrush> BodyBrushProperty =
        AvaloniaProperty.Register<AboutMireyaPanel, IBrush>(nameof(BodyBrush), Brush.Parse("#AAB2D5"));

    public static readonly StyledProperty<IBrush> InsetBackgroundProperty =
        AvaloniaProperty.Register<AboutMireyaPanel, IBrush>(nameof(InsetBackground), Brush.Parse("#101321"));

    public static readonly StyledProperty<IBrush> InsetBorderBrushProperty =
        AvaloniaProperty.Register<AboutMireyaPanel, IBrush>(nameof(InsetBorderBrush), Brush.Parse("#252B48"));

    public static readonly StyledProperty<IBrush> BadgeBackgroundProperty =
        AvaloniaProperty.Register<AboutMireyaPanel, IBrush>(nameof(BadgeBackground), Brush.Parse("#28326A"));

    public static readonly StyledProperty<IBrush> ButtonBackgroundProperty =
        AvaloniaProperty.Register<AboutMireyaPanel, IBrush>(nameof(ButtonBackground), Brush.Parse("#1B1F33"));

    public static readonly StyledProperty<IBrush> ButtonBorderBrushProperty =
        AvaloniaProperty.Register<AboutMireyaPanel, IBrush>(nameof(ButtonBorderBrush), Brush.Parse("#495173"));

    public static readonly StyledProperty<IBrush> ButtonForegroundProperty =
        AvaloniaProperty.Register<AboutMireyaPanel, IBrush>(nameof(ButtonForeground), Brush.Parse("#D7DBF4"));

    public static readonly StyledProperty<string> CalloutTitleProperty =
        AvaloniaProperty.Register<AboutMireyaPanel, string>(nameof(CalloutTitle), "A Mireya server is required");

    public static readonly StyledProperty<string> CalloutTextProperty =
        AvaloniaProperty.Register<AboutMireyaPanel, string>(
            nameof(CalloutText),
            "Run your own server, enter its reachable base URL in server selection, then approve this screen in the server's admin console.");

    public AboutMireyaPanel()
    {
        InitializeComponent();
    }

    public IBrush AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public IBrush PanelBackground
    {
        get => GetValue(PanelBackgroundProperty);
        set => SetValue(PanelBackgroundProperty, value);
    }

    public IBrush PanelBorderBrush
    {
        get => GetValue(PanelBorderBrushProperty);
        set => SetValue(PanelBorderBrushProperty, value);
    }

    public IBrush BodyBrush
    {
        get => GetValue(BodyBrushProperty);
        set => SetValue(BodyBrushProperty, value);
    }

    public IBrush InsetBackground
    {
        get => GetValue(InsetBackgroundProperty);
        set => SetValue(InsetBackgroundProperty, value);
    }

    public IBrush InsetBorderBrush
    {
        get => GetValue(InsetBorderBrushProperty);
        set => SetValue(InsetBorderBrushProperty, value);
    }

    public IBrush BadgeBackground
    {
        get => GetValue(BadgeBackgroundProperty);
        set => SetValue(BadgeBackgroundProperty, value);
    }

    public IBrush ButtonBackground
    {
        get => GetValue(ButtonBackgroundProperty);
        set => SetValue(ButtonBackgroundProperty, value);
    }

    public IBrush ButtonBorderBrush
    {
        get => GetValue(ButtonBorderBrushProperty);
        set => SetValue(ButtonBorderBrushProperty, value);
    }

    public IBrush ButtonForeground
    {
        get => GetValue(ButtonForegroundProperty);
        set => SetValue(ButtonForegroundProperty, value);
    }

    public string CalloutTitle
    {
        get => GetValue(CalloutTitleProperty);
        set => SetValue(CalloutTitleProperty, value);
    }

    public string CalloutText
    {
        get => GetValue(CalloutTextProperty);
        set => SetValue(CalloutTextProperty, value);
    }

    private async void OpenGitHub(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null)
            await topLevel.Launcher.LaunchUriAsync(GitHubUri);
    }
}
