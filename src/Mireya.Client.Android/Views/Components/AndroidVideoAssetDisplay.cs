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
    public event Action<string>? PlaybackEnded;

    public bool KeepAttachedWhenInactive => false;

    private PlayerView? _playerView;
    private IExoPlayer? _player;
    private DispatcherTimer? _playbackCompletionTimer;
    private (string Path, bool Muted)? _pendingPlay;
    private string? _currentVideoPath;
    private bool _playbackEndedSignaled;

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
            _currentVideoPath = null;
            _playbackEndedSignaled = false;
            StopPlaybackCompletionPolling();

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

    private void PlayInternal(string videoPath, bool muted)
    {
        if (_player == null || string.IsNullOrWhiteSpace(videoPath))
            return;

        try
        {
            using var file = new Java.IO.File(videoPath);
            using var fileUri = global::Android.Net.Uri.FromFile(file);
            using var mediaItem = MediaItem.FromUri(fileUri!);

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

    private void StopPlaybackCompletionPolling() => _playbackCompletionTimer?.Stop();

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
