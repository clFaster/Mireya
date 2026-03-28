using System;
using Avalonia.Controls;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;

namespace Mireya.Client.Avalonia.Views.Components;

public partial class VideoAssetDisplay : UserControl
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;

    // Track desired mute state across lifecycle events
    private bool _isMuted;

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
        }
        catch (Exception ex)
        {
            // Log error - VLC initialization failed
            Console.WriteLine($"Failed to initialize VLC: {ex.Message}");
        }
    }

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

    public MediaPlayer? MediaPlayer => _mediaPlayer;
}
