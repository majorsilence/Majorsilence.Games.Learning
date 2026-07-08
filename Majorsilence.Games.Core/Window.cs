using SDL3;

namespace Majorsilence.Games.Core;

public class Window : IDisposable
{
    private IntPtr _window;

    public Window(string title, int width, int height)
    {
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            throw new MajorsilenceException("SDL could not initialize! SDL_Error: " + SDL.GetError());
        }
        TTF.Init();
        _window = SDL.CreateWindow(title, width, height,
            SDL.WindowFlags.OpenGL | SDL.WindowFlags.Resizable);
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