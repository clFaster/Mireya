using System;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Platform;
using LibVLCSharp.Shared;
using Mireya.Client.Avalonia.Platform;
using VlcVideoView = LibVLCSharp.Platforms.Android.VideoView;

namespace Mireya.Client.Avalonia.AndroidTv.Views.Components;

/// <summary>
///     Android implementation of <see cref="IVideoRenderer" />. Hosts the native libVLC
///     <see cref="VlcVideoView" /> through Avalonia's <see cref="NativeControlHost" /> so
///     playback uses the same libVLC engine as the desktop head (parity), with hardware
///     decoding provided by the bundled <c>VideoLAN.LibVLC.Android</c> binaries.
/// </summary>
public sealed class AndroidVideoAssetDisplay : NativeControlHost, IVideoRenderer
{
    /// <summary>Raised once the first frame of the current video has rendered (see <see cref="IVideoRenderer" />).</summary>
    public event Action? FirstFrameReady;

    private VlcVideoView? _videoView;
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;

    // Track desired mute state and the pending play request issued before the native
    // control was created (the renderer can be driven before it is attached).
    private bool _isMuted;
    private (string Path, bool Muted)? _pendingPlay;

    // Guards FirstFrameReady so it is raised only once per Play() call
    private bool _firstFrameSignaled;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = global::Android.App.Application.Context;

        try
        {
            Core.Initialize();
        }
        catch (Exception ex)
        {
            // On Android the native libraries are bundled in the APK, so this is normally a
            // no-op; never let initialization failure crash the render thread.
            Console.WriteLine($"LibVLC Core.Initialize failed: {ex.Message}");
        }

        _libVlc = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVlc);

        _videoView = new VlcVideoView(context) { MediaPlayer = _mediaPlayer };

        // Enforce mute when playback state changes (some sources reset volume).
        _mediaPlayer.Playing += (_, _) => ApplyMute();
        _mediaPlayer.Opening += (_, _) => ApplyMute();

        // TimeChanged fires once playback actually progresses, i.e. the first frame has
        // been decoded and presented. Use it as a "first frame ready" signal so the
        // transition layer can reveal the video without a black first-frame flash.
        _mediaPlayer.TimeChanged += (_, _) =>
        {
            if (_firstFrameSignaled)
                return;
            _firstFrameSignaled = true;
            FirstFrameReady?.Invoke();
        };

        // Apply any play request that arrived before the native control existed.
        if (_pendingPlay is { } pending)
        {
            _pendingPlay = null;
            PlayInternal(pending.Path, pending.Muted);
        }

        return new AndroidViewControlHandle(_videoView);
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        try
        {
            Stop();

            if (_videoView != null)
            {
                _videoView.MediaPlayer = null;
                _videoView.Dispose();
                _videoView = null;
            }

            _mediaPlayer?.Dispose();
            _mediaPlayer = null;
            _libVlc?.Dispose();
            _libVlc = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to dispose libVLC resources: {ex.Message}");
        }

        base.DestroyNativeControlCore(control);
    }

    public void Play(string path, bool muted)
    {
        if (_mediaPlayer == null || _libVlc == null)
        {
            // Native control not created yet — remember the request and apply on creation.
            _pendingPlay = (path, muted);
            return;
        }

        PlayInternal(path, muted);
    }

    public void Stop()
    {
        _pendingPlay = null;
        _mediaPlayer?.Stop();
        _currentMedia?.Dispose();
        _currentMedia = null;
    }

    private void PlayInternal(string videoPath, bool muted)
    {
        if (_mediaPlayer == null || _libVlc == null || string.IsNullOrEmpty(videoPath))
            return;

        try
        {
            _currentMedia?.Dispose();
            _currentMedia = new Media(_libVlc, videoPath, FromType.FromPath);

            _isMuted = muted;

            // New media: allow the next first-frame signal to fire.
            _firstFrameSignaled = false;

            // Hint initial volume through media options to prevent loud blips on start
            // (libVLC expects 0-512; 0 is muted) and disable audio entirely when muted.
            _currentMedia.AddOption($":volume={(muted ? 0 : 256)}");
            if (muted)
                _currentMedia.AddOption(":no-audio");

            ApplyMute();

            _mediaPlayer.Play(_currentMedia);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to play video: {ex.Message}");
        }
    }

    private void ApplyMute()
    {
        if (_mediaPlayer == null)
            return;

        try
        {
            _mediaPlayer.Mute = _isMuted;
            _mediaPlayer.Volume = _isMuted ? 0 : 100;
        }
        catch
        {
            /* ignore — volume control can be unavailable between media transitions */
        }
    }
}
