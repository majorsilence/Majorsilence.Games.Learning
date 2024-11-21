using SDL2;

namespace Majorsilence.Games.Learning;

public class Window : IDisposable
{
    private IntPtr _window;

    public Window(string title, int width, int height)
    {
        //var screen = SDL.SDL_CreateWindow("My SDL Empty Window",
        //    SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, 640, 480, 0);
        SDL.SDL_Init(SDL.SDL_INIT_VIDEO);
        SDL_ttf.TTF_Init();
        _window = SDL.SDL_CreateWindow(title,
            SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, width, height,
            SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL | SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
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
        SDL.SDL_DestroyWindow(_window);
        SDL.SDL_Quit();

        _disposed = true;
    }
}