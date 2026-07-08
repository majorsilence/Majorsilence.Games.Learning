using SDL3;

namespace Majorsilence.Games.Core.Audio;

/// <summary>
/// A loaded audio file bound to its own playback track. Shared base for
/// <see cref="Sound"/> (short, predecoded effects) and <see cref="Music"/> (streamed,
/// typically looping background tracks) — SDL_mixer 3 treats both the same way under
/// the hood, so the split here is purely about the defaults each one is played with.
/// </summary>
public abstract class AudioClip : IDisposable
{
    private readonly IntPtr _audio;
    private readonly IntPtr _track;

    protected AudioClip(AudioDevice device, string path, bool predecode)
    {
        if (!File.Exists(path)) throw new MajorsilenceException($"Audio file not found: {path}");

        _audio = Mixer.LoadAudio(device, path, predecode);
        if (_audio == IntPtr.Zero)
            throw new MajorsilenceException($"Could not load audio '{path}': {SDL.GetError()}");

        _track = Mixer.CreateTrack(device);
        Mixer.SetTrackAudio(_track, _audio);
    }

    public float Volume
    {
        get => Mixer.GetTrackGain(_track);
        set => Mixer.SetTrackGain(_track, value);
    }

    public bool IsPlaying => Mixer.TrackPlaying(_track);

    public void Play(bool loop)
    {
        Mixer.SetTrackLoops(_track, loop ? -1 : 0);
        Mixer.PlayTrack(_track, 0);
    }

    public void Stop(int fadeOutMs = 0)
    {
        var fadeOutFrames = fadeOutMs > 0 ? Mixer.TrackMSToFrames(_track, fadeOutMs) : 0;
        Mixer.StopTrack(_track, fadeOutFrames);
    }

    public void Pause() => Mixer.PauseTrack(_track);
    public void Resume() => Mixer.ResumeTrack(_track);

    public void Dispose()
    {
        Dispose(true);
    }

    private bool _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        Mixer.DestroyTrack(_track);
        Mixer.DestroyAudio(_audio);
        _disposed = true;
    }
}
