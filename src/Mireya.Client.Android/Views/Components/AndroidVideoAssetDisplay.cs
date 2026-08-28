using System;
using Android.App;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.UI;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Java.IO;
using Mireya.Client.Avalonia.Platform;
using Console = System.Console;
using Uri = Android.Net.Uri;

namespace Mireya.Client.Avalonia.Views.Components;

/// <summary>
///     Android video renderer backed by Jetpack Media3/ExoPlayer. Media3 delegates
///     decoding to Android's platform codecs, so the Android client does not need to
///     bundle LibVLC and its native C++ runtime.
/// </summary>
public sealed class AndroidVideoAssetDisplay : NativeControlHost, IVideoRenderer
{
    private string? _currentVideoPath;
    private (string Path, bool Muted)? _pendingPlay;
    private DispatcherTimer? _playbackCompletionTimer;
    private bool _playbackEndedSignaled;
    private IExoPlayer? _player;

    private PlayerView? _playerView;
    public event Action<string>? PlaybackEnded;

    public bool KeepAttachedWhenInactive => false;

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
        _currentVideoPath = null;
        _playbackEndedSignaled = false;
        StopPlaybackCompletionPolling();

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

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = Application.Context;

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
        // Avalonia's NativeControlHost may have already disposed the JNI wrappers for the
        // views it embedded (PlayerView is an AndroidX view attached through
        // AndroidViewControlHandle). When that happens, touching _playerView throws
        // ObjectDisposedException. Historically that exception was caught by a single
        // outer try/catch, which silently skipped _player.Stop/Release/Dispose — leaking a
        // full ExoPlayer (audio + video decoders, decoder threads, buffers) on every
        // Video→non-Video transition and driving the app back into the low-memory killer.
        // Release each resource independently, and release the player we own *first* so
        // its lifecycle no longer depends on the PlayerView JNI wrapper still being alive.
        _pendingPlay = null;
        _currentVideoPath = null;
        _playbackEndedSignaled = false;
        StopPlaybackCompletionPolling();

        // Best-effort: detach the player from the view so the view stops rendering. May
        // throw if the JNI-side PlayerView is already gone; that is fine, the player
        // release below is what actually reclaims the native resources.
        try
        {
            if (_playerView != null)
                _playerView.Player = null;
        }
        catch (ObjectDisposedException)
        {
            // PlayerView JNI wrapper was disposed by Avalonia's detach hook before us.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to detach Media3 player from view: {ex.Message}");
        }

        if (_player != null)
        {
            try
            {
                _player.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to stop Media3 player: {ex.Message}");
            }

            try
            {
                _player.Release();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to release Media3 player: {ex.Message}");
            }

            try
            {
                _player.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to dispose Media3 player: {ex.Message}");
            }

            _player = null;
        }

        if (_playerView != null)
        {
            try
            {
                _playerView.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed by Avalonia's detach path; nothing to do.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to dispose Media3 PlayerView: {ex.Message}");
            }

            _playerView = null;
        }

        base.DestroyNativeControlCore(control);
    }

    private void PlayInternal(string videoPath, bool muted)
    {
        if (_player == null || string.IsNullOrWhiteSpace(videoPath))
            return;

        try
        {
            using var file = new File(videoPath);
            using var fileUri = Uri.FromFile(file);
            using var mediaItem = MediaItem.FromUri(fileUri);

            _player.Volume = muted ? 0f : 1f;
            _currentVideoPath = videoPath;
            _playbackEndedSignaled = false;
            _player.SetMediaItem(mediaItem);
            _player.Prepare();
            _player.Play();
            StartPlaybackCompletionPolling();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to play video with Media3: {ex.Message}");
        }
    }

    private void StartPlaybackCompletionPolling()
    {
        if (_playbackCompletionTimer == null)
        {
            _playbackCompletionTimer = new DispatcherTimer(
                DispatcherPriority.Default,
                Dispatcher.UIThread
            )
            {
                Interval = TimeSpan.FromMilliseconds(100),
            };
            _playbackCompletionTimer.Tick += OnPlaybackCompletionTimerTick;
        }

        _playbackCompletionTimer.Stop();
        _playbackCompletionTimer.Start();
    }

    private void StopPlaybackCompletionPolling()
    {
        _playbackCompletionTimer?.Stop();
    }

    private void OnPlaybackCompletionTimerTick(object? sender, EventArgs e)
    {
        try
        {
            if (_player == null || _playbackEndedSignaled)
            {
                StopPlaybackCompletionPolling();
                return;
            }

            if (
                _player.PlaybackState != BasePlayer.InterfaceConsts.StateEnded
                || _currentVideoPath == null
            )
                return;

            _playbackEndedSignaled = true;
            StopPlaybackCompletionPolling();
            PlaybackEnded?.Invoke(_currentVideoPath);
        }
        catch (Exception ex)
        {
            StopPlaybackCompletionPolling();
            Console.WriteLine($"Failed while checking Media3 playback completion: {ex.Message}");
        }
    }
}
