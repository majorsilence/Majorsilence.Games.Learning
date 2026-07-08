using SDL3;

namespace Majorsilence.Games.Core.Audio;

/// <summary>
/// Opens the default playback device and owns the SDL_mixer mixer used to play
/// <see cref="Sound"/> and <see cref="Music"/> clips. One instance per application, created
/// after the SDL video subsystem (see <see cref="Window"/>) is up.
/// </summary>
public class AudioDevice : IDisposable
{
    private IntPtr _mixer;

    public AudioDevice()
    {
        if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
        {
            throw new MajorsilenceException("SDL audio subsystem could not initialize! SDL_Error: " + SDL.GetError());
        }

        if (!Mixer.Init())
        {
            throw new MajorsilenceException("SDL_mixer could not initialize! SDL_Error: " + SDL.GetError());
        }

        _mixer = Mixer.CreateMixerDevice(SDL.AudioDeviceDefaultPlayback, IntPtr.Zero);
        if (_mixer == IntPtr.Zero)
        {
            throw new MajorsilenceException("Could not open audio device! SDL_Error: " + SDL.GetError());
        }
    }

    public static implicit operator IntPtr(AudioDevice ap)
    {
        if (ap._disposed) return IntPtr.Zero;
        return ap._mixer;
    }

    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        Mixer.DestroyMixer(_mixer);
        Mixer.Quit();
        _disposed = true;
    }
}
