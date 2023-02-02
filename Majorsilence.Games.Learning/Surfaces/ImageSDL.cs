using System;
using System.Runtime.InteropServices;
using SDL2;

namespace Majorsilence.Games.Learning.Surfaces;

public class ImageSDL : Surface, IDisposable
{
    public ImageSDL(string path)
    {
        if (!File.Exists(path)) throw new MajorsilenceException($"Image not found: {path}");

        _surface = SDL_image.IMG_Load(path);
        SetRect();
    }

    private void SetRect()
    {
        var sur = Marshal.PtrToStructure<SDL.SDL_Surface>(_surface);
        var rect = Rect;
        rect.h = sur.h;
        rect.w = sur.w;
        rect.x = 0;
        rect.y = 0;
        Rect = rect;
    }

    public override void Dispose()
    {
        Dispose(true);
    }
    
    public void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // TODO: dispose managed state (managed objects).
        }

        // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
        // TODO: set large fields to null.
        SDL.SDL_FreeSurface(_surface);
        _disposed = true;
    }
}