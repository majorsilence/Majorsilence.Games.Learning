using System;
using SDL2;

namespace Majorsilence.Games.Core;

public class Fonts : IDisposable
{
    private IntPtr font;

    public Fonts(string fontPath, int size)
    {
        font = SDL_ttf.TTF_OpenFont(fontPath, size);
    }

    public static implicit operator IntPtr(Fonts ap)
    {
        if (ap._disposed) return IntPtr.Zero;
        return ap.font;
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
        SDL_ttf.TTF_CloseFont(font);
        _disposed = true;
    }
}