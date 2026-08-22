using System;
using Avalonia;
using Avalonia.Controls;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using Mireya.Client.Avalonia.Platform;

namespace Mireya.Client.Avalonia.Views.Components;

public partial class VideoAssetDisplay : UserControl, IVideoRenderer
{
    public event Action<string>? PlaybackEnded;

    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private EventHandler<EventArgs>? _endReachedHandler;
    private int _playbackGeneration;
    private bool _playbackEndedSignaled;

    // Track desired mute state across lifecycle events
    private bool _isMuted;

    public VideoAssetDisplay()
    {
        InitializeComponent();
        InitializeVlc();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // ContentDisplayView detaches native renderers while another asset type is active.
        // OnDetachedFromVisualTree releases LibVLC, so a reused control must recreate it.
        if (_mediaPlayer == null || _libVlc == null)
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
            Stop();

            // Create new media and keep reference
            _currentMedia = new Media(_libVlc, videoPath);
            var generation = ++_playbackGeneration;
            _playbackEndedSignaled = false;
            _endReachedHandler = (_, _) =>
            {
                if (generation != _playbackGeneration || _playbackEndedSignaled)
                    return;

                _playbackEndedSignaled = true;
                PlaybackEnded?.Invoke(videoPath);
            };
            _mediaPlayer.EndReached += _endReachedHandler;

            // Store desired mute state
            _isMuted = isMuted;

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
        _playbackGeneration++;
        _playbackEndedSignaled = false;

        if (_mediaPlayer != null && _endReachedHandler != null)
            _mediaPlayer.EndReached -= _endReachedHandler;
        _endReachedHandler = null;

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
