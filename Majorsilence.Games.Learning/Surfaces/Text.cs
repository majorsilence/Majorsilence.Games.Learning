using System;
using SDL2;

namespace Majorsilence.Games.Learning.Surfaces;

public class Text : Surface
{
    public Text(Fonts font, SDL2.SDL.SDL_Color color, string input)
    {
        _surface = SDL_ttf.TTF_RenderText_Solid(font,
            input, color);
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