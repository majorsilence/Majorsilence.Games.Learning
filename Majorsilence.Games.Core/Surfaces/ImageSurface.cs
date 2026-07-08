using System;
using System.Runtime.InteropServices;
using SDL3;

namespace Majorsilence.Games.Core.Surfaces;

public class ImageSurface : Surface, IDisposable
{
    public ImageSurface(string path)
    {
        if (!File.Exists(path)) throw new MajorsilenceException($"Image not found: {path}");

        _surface = Image.Load(path);
        SetRect();
    }

    private void SetRect()
    {
        var sur = Marshal.PtrToStructure<SDL.Surface>(_surface);
        var rect = Rect;
        rect.H = sur.Height;
        rect.W = sur.Width;
        rect.X = 0;
        rect.Y = 0;
        Rect = rect;
    }

    public override void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
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
