using System;
using System.Runtime.InteropServices;
using SDL3;

namespace Majorsilence.Games.Core;

public class Renderer : IDisposable
{
    private IntPtr _renderer;
    private readonly Window _window;

    public Renderer(Window window)
    {
        _renderer = SDL.CreateRenderer(window, null);
        SDL.SetRenderVSync(_renderer, 1);
        _window = window;
    }

    public static implicit operator IntPtr(Renderer ap)
    {
        if (ap._disposed) return IntPtr.Zero;
        return ap._renderer;
    }

    public void Clear()
    {
        SDL.RenderClear(this);
    }

    public void Present()
    {
        SDL.RenderPresent(this);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    public (int Width, int Height) Size
    {
        get
        {
            SDL.GetRenderOutputSize(_renderer, out int width, out int height);
            return (width, height);
        }
    }

    public bool IsFullscreen => (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Fullscreen) != 0;
    public void SetFullscreen(bool fullscreen)
    {
        SDL.SetWindowFullscreen(_window, fullscreen);
    }

    private bool _disposed;

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
        // TODO: set large fields to null.
        SDL.DestroyRenderer(_renderer);
        _disposed = true;
    }

    /// <summary>
    /// Save a screenshot of the current screen.
    /// </summary>
    /// <param name="savePath">supports file extensions .bmp, .jpg, .png</param>
    public void SaveScreenshot(string savePath = "screenshot.bmp")
    {
        var surface = SDL.RenderReadPixels(_renderer, null);

        if (savePath.EndsWith(".bmp", StringComparison.InvariantCultureIgnoreCase))
            SDL.SaveBMP(surface, savePath);
        else if (savePath.EndsWith(".jpg", StringComparison.InvariantCultureIgnoreCase))
            Image.SaveJPG(surface, savePath, 85);
        else if (savePath.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase))
            Image.SavePNG(surface, savePath);
        else
            throw new MajorsilenceException("Only bmp, jpg, png file formats are supported.");

        SDL.DestroySurface(surface);
    }

    public void DrawColor(byte r, byte g, byte b, byte a)
    {
        SDL.SetRenderDrawColor(this, r, g, b, a);
    }
}
