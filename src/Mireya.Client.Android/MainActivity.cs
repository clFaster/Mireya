using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;

namespace Mireya.Client.Avalonia.AndroidTv;

/// <summary>
///     Android TV launcher activity. The shared <see cref="App" /> and the composition root
///     are configured by <see cref="MainApplication" />; this activity only hosts the
///     Avalonia surface, registers in the Leanback (TV) launcher and runs full-screen.
/// </summary>
[Activity(
    Label = "Mireya",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    Banner = "@drawable/banner",
    Exported = true,
    MainLauncher = false,
    LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode
        | ConfigChanges.Keyboard
        | ConfigChanges.KeyboardHidden
        | ConfigChanges.Navigation)]
[IntentFilter(
    new[] { Intent.ActionMain },
    Categories = new[] { Intent.CategoryLeanbackLauncher, Intent.CategoryLauncher })]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Run as an immersive full-screen kiosk: keep the screen on and hide the system
        // bars so signage content fills the whole TV screen. The modern WindowInsets API
        // is only available from API 30 (Android 11).
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        var controller = Window?.InsetsController;
        if (System.OperatingSystem.IsAndroidVersionAtLeast(30) && controller is not null)
        {
            controller.Hide(WindowInsets.Type.SystemBars());
            controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
        }
    }
}

