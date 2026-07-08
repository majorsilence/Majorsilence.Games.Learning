namespace Majorsilence.Games.Core.Audio;

/// <summary>
/// A short sound effect (e.g. a jump or hit sound), fully decoded up front so
/// repeated <see cref="Play"/> calls have no decode latency. Retriggering while
/// already playing restarts this clip's single track rather than layering instances.
/// </summary>
public class Sound : AudioClip
{
    public Sound(AudioDevice device, string path) : base(device, path, predecode: true)
    {
    }

    public void Play() => Play(loop: false);
}
