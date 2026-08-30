using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;
using Mireya.Client.Avalonia.Platform;

namespace Mireya.Client.Avalonia;

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
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode
        | ConfigChanges.Keyboard
        | ConfigChanges.KeyboardHidden
        | ConfigChanges.Navigation
)]
[IntentFilter(
    new[] { Intent.ActionMain },
    Categories = new[] { Intent.CategoryLeanbackLauncher, Intent.CategoryLauncher }
)]
public class MainActivity : AvaloniaMainActivity
{
    private bool _screenInfoTouchCaptured;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        AndroidServices.PresentationController.Attach(this);
    }

    protected override void OnDestroy()
    {
        AndroidServices.PresentationController.Detach(this);
        base.OnDestroy();
    }

    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e is null)
            return base.DispatchKeyEvent(e);

        var root = App.RootViewModel;
        if (e.Action == KeyEventActions.Down && e.RepeatCount == 0)
            root?.CancelAutoStart();

        var isPrimaryAction =
            e.KeyCode
            is Keycode.DpadCenter
                or Keycode.Enter
                or Keycode.NumpadEnter
                or Keycode.Space
                or Keycode.ButtonA;

        if (isPrimaryAction && root?.CanHandleScreenInfoInput == true)
        {
            if (e.Action == KeyEventActions.Down && e.RepeatCount == 0)
                root.TryToggleScreenInfo();

            // Consume both halves of the native event so Avalonia does not toggle twice.
            return true;
        }

        return base.DispatchKeyEvent(e);
    }

    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        if (e is null)
            return base.DispatchTouchEvent(e);

        if (e.Action == MotionEventActions.Down)
            App.RootViewModel?.CancelAutoStart();

        if (e.Action == MotionEventActions.Down && App.RootViewModel?.TryOpenScreenInfo() == true)
        {
            _screenInfoTouchCaptured = true;
            return true;
        }

        if (_screenInfoTouchCaptured)
        {
            if (e.Action is MotionEventActions.Up or MotionEventActions.Cancel)
                _screenInfoTouchCaptured = false;
            return true;
        }

        return base.DispatchTouchEvent(e);
    }

#pragma warning disable CS0672 // AndroidX still routes predictive Back through this override as its fallback.
#pragma warning disable CS0618 // Base OnBackPressed remains the AndroidX fallback below API 33.
    public override void OnBackPressed()
    {
        if (App.RootViewModel?.TryCloseScreenInfo() == true)
            return;

        if (OperatingSystem.IsAndroidVersionAtLeast(24))
        {
#pragma warning disable CA1422 // AndroidX invokes this override as its predictive-Back fallback.
            base.OnBackPressed();
#pragma warning restore CA1422
        }
        else
        {
            Finish();
        }
    }
#pragma warning restore CS0618
#pragma warning restore CS0672
}
