using System;
using SDL3;

namespace Majorsilence.Games.Core.Surfaces;

public class TextSurface : Surface
{
    public TextSurface(Fonts font, SDL.Color color, string input)
    {
        _surface = TTF.RenderTextSolid(font, input, (UIntPtr)0, color);
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
        SDL.DestroySurface(_surface);
        _disposed = true;
    }
}
