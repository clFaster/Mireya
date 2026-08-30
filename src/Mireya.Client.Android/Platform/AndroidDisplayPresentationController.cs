using System;
using Android.Content.PM;
using Android.Views;

namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     Applies the native Android presentation appropriate to setup/diagnostics or
///     unattended signage playback. The controller survives activity recreation and
///     reapplies the last requested state when the new activity attaches.
/// </summary>
public sealed class AndroidDisplayPresentationController(FormFactor formFactor)
    : IDisplayPresentationController
{
    private MainActivity? _activity;

    public DisplayPresentation Current { get; private set; } = DisplayPresentation.Interactive;

    public void Attach(MainActivity activity)
    {
        _activity = activity;
        ApplyToActivity(activity, Current);
    }

    public void Detach(MainActivity activity)
    {
        if (ReferenceEquals(_activity, activity))
            _activity = null;
    }

    public void Apply(DisplayPresentation presentation)
    {
        Current = presentation;
        if (_activity is { } activity)
            activity.RunOnUiThread(() => ApplyToActivity(activity, presentation));
    }

    private void ApplyToActivity(MainActivity activity, DisplayPresentation presentation)
    {
        var isPlayback = presentation == DisplayPresentation.Playback;
        activity.RequestedOrientation =
            formFactor == FormFactor.Tv
                ? ScreenOrientation.Landscape
                : isPlayback
                    ? ScreenOrientation.SensorLandscape
                    : ScreenOrientation.Unspecified;

        var window = activity.Window;
        if (window is null)
            return;

        if (isPlayback)
            window.AddFlags(WindowManagerFlags.KeepScreenOn | WindowManagerFlags.Fullscreen);
        else
        {
            window.ClearFlags(WindowManagerFlags.KeepScreenOn | WindowManagerFlags.Fullscreen);
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var controller = window.InsetsController;
            if (controller is null)
                return;

            if (isPlayback)
            {
                controller.Hide(WindowInsets.Type.SystemBars());
                controller.SystemBarsBehavior = (int)
                    WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
            }
            else
                controller.Show(WindowInsets.Type.SystemBars());

            return;
        }

#pragma warning disable CS0618 // SystemUiVisibility is required below Android 11.
        window.DecorView.SystemUiVisibility = isPlayback
            ? (StatusBarVisibility)(
                SystemUiFlags.ImmersiveSticky
                | SystemUiFlags.Fullscreen
                | SystemUiFlags.HideNavigation
                | SystemUiFlags.LayoutStable
                | SystemUiFlags.LayoutFullscreen
                | SystemUiFlags.LayoutHideNavigation
            )
            : StatusBarVisibility.Visible;
#pragma warning restore CS0618
    }
}
