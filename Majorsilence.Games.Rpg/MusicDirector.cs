using Majorsilence.Games.Core.Audio;

namespace Majorsilence.Games.Rpg;

/// <summary>
/// Decides what is playing, given where the hero is.
///
/// The one rule that matters: asking for the track that is already playing does
/// nothing. That makes Play idempotent, so callers don't have to track what is
/// already going - a map can name its track on every load, and two maps that
/// name the same track hand over without the music restarting from the top
/// under the player.
///
/// Tracks are cached rather than reloaded per visit; there are four of them and
/// they are seconds long, so the memory is not worth the decode.
/// </summary>
public class MusicDirector : IDisposable
{
    private readonly AudioDevice? _device;
    private readonly Dictionary<string, Music> _tracks = new();
    private Music? _playing;

    public MusicDirector(AudioDevice? device)
    {
        _device = device;
    }

    /// <summary>Which track is currently playing, "" for silence - what a scripted run prints to show the music followed the hero.</summary>
    public string NowPlaying { get; private set; } = "";

    public float Volume { get; set; } = 0.4f;

    /// <summary>Plays the given track, or silence for an empty path. A no-op if that track is already playing.</summary>
    public void Play(string path)
    {
        if (_device is null || path == NowPlaying) return;

        _playing?.Stop(fadeOutMs: 250);
        NowPlaying = path;

        if (path == "")
        {
            _playing = null;
            return;
        }

        if (!_tracks.TryGetValue(path, out var track))
        {
            track = new Music(_device, path);
            _tracks[path] = track;
        }

        track.Volume = Volume;
        track.Play();
        _playing = track;
    }

    public void Dispose()
    {
        foreach (var track in _tracks.Values) track.Dispose();
        _tracks.Clear();
        _playing = null;
    }
}
