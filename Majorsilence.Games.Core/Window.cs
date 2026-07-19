using SDL3;

namespace Majorsilence.Games.Core;

public class Window : IDisposable
{
    private IntPtr _window;

    public Window(string title, int width, int height, bool highPixelDensity = false, bool fullscreen = false)
    {
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            throw new MajorsilenceException("SDL could not initialize! SDL_Error: " + SDL.GetError());
        }
        TTF.Init();
        var flags = SDL.WindowFlags.OpenGL | SDL.WindowFlags.Resizable;
        if (highPixelDensity) flags |= SDL.WindowFlags.HighPixelDensity;
        // On Android a fullscreen SDL window makes SDLActivity enter immersive
        // mode (system status/navigation bars hidden) instead of drawing under
        // an opaque status bar.
        if (fullscreen) flags |= SDL.WindowFlags.Fullscreen;
        _window = SDL.CreateWindow(title, width, height, flags);
    }

    /// <summary>
    /// Window size in points/screen-coordinates (not physical pixels - see
    /// SDL_GetWindowSize vs SDL_GetRenderOutputSize for the HiDPI distinction).
    /// </summary>
    public (int Width, int Height) Size
    {
        get
        {
            SDL.GetWindowSize(_window, out var w, out var h);
            return (w, h);
        }
    }

    public static implicit operator IntPtr(Window ap)
    {
        if (ap._disposed) return IntPtr.Zero;
        return ap._window;
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private bool _disposed;

    public void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // TODO: dispose managed state (managed objects).
        }

        // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
        // TODO: set large fields to null.
        SDL.DestroyWindow(_window);
        SDL.Quit();

        _disposed = true;
    }
}