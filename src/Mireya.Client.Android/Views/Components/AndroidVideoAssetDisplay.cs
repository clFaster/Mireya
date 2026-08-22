using System;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.UI;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Mireya.Client.Avalonia.Platform;

namespace Mireya.Client.Avalonia.AndroidTv.Views.Components;

/// <summary>
///     Android video renderer backed by Jetpack Media3/ExoPlayer. Media3 delegates
///     decoding to Android's platform codecs, so the Android client does not need to
///     bundle LibVLC and its native C++ runtime.
/// </summary>
public sealed class AndroidVideoAssetDisplay : NativeControlHost, IVideoRenderer
{
    public event Action? FirstFrameReady;

    private PlayerView? _playerView;
    private IExoPlayer? _player;
    private DispatcherTimer? _firstFrameTimer;
    private (string Path, bool Muted)? _pendingPlay;
    private bool _firstFrameSignaled;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = global::Android.App.Application.Context;

        var player =
            new ExoPlayerBuilder(context).Build()
            ?? throw new InvalidOperationException(
                "Media3 failed to create an ExoPlayer instance."
            );
        _player = player;

        _playerView = new PlayerView(context)
        {
            Player = player,
            UseController = false,
            KeepScreenOn = true,
        };

        if (_pendingPlay is { } pending)
        {
            _pendingPlay = null;
            PlayInternal(pending.Path, pending.Muted);
        }

        return new AndroidViewControlHandle(_playerView);
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        try
        {
            _pendingPlay = null;
            StopFirstFramePolling();

            if (_playerView != null)
                _playerView.Player = null;

            if (_player != null)
            {
                _player.Stop();
                _player.Release();
                _player.Dispose();
                _player = null;
            }

            if (_playerView != null)
            {
                _playerView.Dispose();
                _playerView = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to dispose Media3 resources: {ex.Message}");
        }

        base.DestroyNativeControlCore(control);
    }

    public void Play(string path, bool muted)
    {
        if (_player == null)
        {
            _pendingPlay = (path, muted);
            return;
        }

        PlayInternal(path, muted);
    }

    public void Stop()
    {
        _pendingPlay = null;
        StopFirstFramePolling();

        try
        {
            _player?.Stop();
            _player?.ClearMediaItems();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to stop Media3 playback: {ex.Message}");
        }
    }

    private void PlayInternal(string videoPath, bool muted)
    {
        if (_player == null || string.IsNullOrWhiteSpace(videoPath))
            return;

        try
        {
            using var file = new Java.IO.File(videoPath);
            using var fileUri = global::Android.Net.Uri.FromFile(file);
            using var mediaItem = MediaItem.FromUri(fileUri!);

            _firstFrameSignaled = false;
            _player.Volume = muted ? 0f : 1f;
            _player.SetMediaItem(mediaItem);
            _player.Prepare();
            _player.Play();
            StartFirstFramePolling();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to play video with Media3: {ex.Message}");
        }
    }

    private void SignalFirstFrame()
    {
        if (_firstFrameSignaled)
            return;

        _firstFrameSignaled = true;
        StopFirstFramePolling();
        FirstFrameReady?.Invoke();
    }

    private void StartFirstFramePolling()
    {
        if (_firstFrameTimer == null)
        {
            _firstFrameTimer = new DispatcherTimer(DispatcherPriority.Default, Dispatcher.UIThread)
            {
                Interval = TimeSpan.FromMilliseconds(50),
            };
            _firstFrameTimer.Tick += OnFirstFrameTimerTick;
        }

        _firstFrameTimer.Stop();
        _firstFrameTimer.Start();
    }

    private void StopFirstFramePolling() => _firstFrameTimer?.Stop();

    private void OnFirstFrameTimerTick(object? sender, EventArgs e)
    {
        try
        {
            // The .NET binding generates native stubs for every Java default method on
            // Player.Listener. A partial C# implementation therefore crashes with an
            // AbstractMethodError as soon as Media3 invokes another callback. Polling the
            // playback clock avoids that binding issue; once it advances, ExoPlayer has
            // started presenting the prepared video.
            if (_player == null || _firstFrameSignaled)
                StopFirstFramePolling();
            else if (_player.CurrentPosition > 0)
                SignalFirstFrame();
        }
        catch (Exception ex)
        {
            StopFirstFramePolling();
            Console.WriteLine($"Failed while waiting for the first Media3 frame: {ex.Message}");
        }
    }
}
