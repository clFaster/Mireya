using System;

namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     Abstraction for the control that plays video assets on a given platform
///     (LibVLC on desktop, the native media player on Android). Implemented by a control
///     supplied through <see cref="IAssetViewFactory" />.
/// </summary>
public interface IVideoRenderer
{
    /// <summary>
    ///     Begin playback of the local video file at <paramref name="path" />.
    /// </summary>
    /// <param name="path">Absolute path to the local video file.</param>
    /// <param name="muted">Whether the video should play without audio.</param>
    void Play(string path, bool muted);

    /// <summary>
    ///     Stop playback and release the current media.
    /// </summary>
    void Stop();

    /// <summary>
    ///     Raised when the active video reaches its natural end. The path identifies the
    ///     playback request that completed so stale notifications can be ignored.
    /// </summary>
    event Action<string>? PlaybackEnded;
}
