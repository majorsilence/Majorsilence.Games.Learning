namespace Majorsilence.Games.Core.Audio;

/// <summary>
/// A streamed background music track. Unlike <see cref="Sound"/>, it isn't predecoded
/// up front (music files are typically much larger), and <see cref="Play"/> loops by default.
/// </summary>
public class Music : AudioClip
{
    public Music(AudioDevice device, string path) : base(device, path, predecode: false)
    {
    }

    public void Play() => Play(loop: true);
}
