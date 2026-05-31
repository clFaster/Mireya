using System;
using Avalonia;
using Avalonia.Controls;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using Mireya.Client.Avalonia.Platform;

namespace Mireya.Client.Avalonia.Views.Components;

public partial class VideoAssetDisplay : UserControl, IVideoRenderer
{
    /// <summary>Raised once the first frame of the current video has rendered (see <see cref="IVideoRenderer" />).</summary>
    public event Action? FirstFrameReady;

    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;

    // Track desired mute state across lifecycle events
    private bool _isMuted;

    // Guards FirstFrameReady so it is raised only once per Play() call
    private bool _firstFrameSignaled;

    public VideoAssetDisplay()
    {
        InitializeComponent();
        InitializeVlc();
    }

    private void InitializeVlc()
    {
        try
        {
            Core.Initialize();
            _libVlc = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVlc);
            VideoView.MediaPlayer = _mediaPlayer;

            // Ensure mute is enforced when playback state changes (some sources reset volume)
            _mediaPlayer.Playing += (_, __) =>
            {
                try
                {
                    _mediaPlayer.Mute = _isMuted;
                    _mediaPlayer.Volume = _isMuted ? 0 : 100;
                }
                catch
                { /* ignore */
                }
            };
            _mediaPlayer.Opening += (_, __) =>
            {
                try
                {
                    _mediaPlayer.Mute = _isMuted;
                    _mediaPlayer.Volume = _isMuted ? 0 : 100;
                }
                catch
                { /* ignore */
                }
            };

            // TimeChanged fires once playback actually progresses, i.e. the first frame has
            // been decoded and presented. Use it as a "first frame ready" signal so the
            // transition layer can reveal the video without a black first-frame flash.
            // Note: this event is raised on a LibVLC background thread.
            _mediaPlayer.TimeChanged += (_, __) =>
            {
                if (_firstFrameSignaled)
                    return;
                _firstFrameSignaled = true;
                FirstFrameReady?.Invoke();
            };
        }
        catch (Exception ex)
        {
            // Log error - VLC initialization failed
            Console.WriteLine($"Failed to initialize VLC: {ex.Message}");
        }
    }

    public void Play(string path, bool muted) => PlayVideo(path, muted);

    public void PlayVideo(string videoPath, bool isMuted = false)
    {
        if (_mediaPlayer == null || _libVlc == null || string.IsNullOrEmpty(videoPath))
            return;

        try
        {
            // Dispose previous media if exists
            _currentMedia?.Dispose();

            // Create new media and keep reference
            _currentMedia = new Media(_libVlc, videoPath);

            // Store desired mute state
            _isMuted = isMuted;

            // New media: allow the next first-frame signal to fire
            _firstFrameSignaled = false;

            // Hint initial volume through media options to prevent loud blips on start
            // Note: LibVLC expects values 0-512; 0 is muted.
            _currentMedia.AddOption($":volume={(isMuted ? 0 : 256)}");
            // Also disable audio entirely when muted for some codecs/drivers
            if (isMuted)
            {
                _currentMedia.AddOption(":no-audio");
            }

            // Set mute state on MediaPlayer (also force volume to 0 when muted to be extra safe)
            _mediaPlayer.Mute = _isMuted;
            _mediaPlayer.Volume = _isMuted ? 0 : 100;

            Console.WriteLine($"Playing video: {videoPath} (Muted: {_isMuted})");

            _mediaPlayer.Play(_currentMedia);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to play video: {ex.Message}");
        }
    }

    public void Stop()
    {
        _mediaPlayer?.Stop();
        _currentMedia?.Dispose();
        _currentMedia = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        try
        {
            Stop();
            VideoView.MediaPlayer = null;
            _mediaPlayer?.Dispose();
            _mediaPlayer = null;
            _libVlc?.Dispose();
            _libVlc = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to dispose VLC resources: {ex.Message}");
        }
    }

    public MediaPlayer? MediaPlayer => _mediaPlayer;
}
