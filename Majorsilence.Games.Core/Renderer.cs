using System;
using System.Runtime.InteropServices;
using SDL2;

namespace Majorsilence.Games.Core;

public class Renderer : IDisposable
{
    private IntPtr _renderer;

    public Renderer(Window window)
    {
        _renderer = SDL.SDL_CreateRenderer(window, -1,
            SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED |
            SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
    }

    public static implicit operator IntPtr(Renderer ap)
    {
        if (ap._disposed) return IntPtr.Zero;
        return ap._renderer;
    }

    public void Clear()
    {
        SDL.SDL_RenderClear(this);
    }

    public void Present()
    {
        SDL.SDL_RenderPresent(this);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    public (int Width, int Height) Size
    {
        get
        {
            SDL.SDL_GetRendererOutputSize(_renderer, out int width, out int height);
            return (width, height);
        }
    }

    private bool _disposed;

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
        // TODO: set large fields to null.
        SDL.SDL_DestroyRenderer(_renderer);
        _disposed = true;
    }

    /// <summary>
    /// Save a screenshot of the current screen.
    /// </summary>
    /// <param name="savePath">supports file extensions .bmp, .jpg, .png</param>
    public void SaveScreenshot(string savePath = "screenshot.bmp")
    {
        uint format = SDL.SDL_PIXELFORMAT_RGBX8888;
        var width = 0;
        var height = 0;

        SDL.SDL_GetRendererOutputSize(_renderer, out width, out height);
        var surface = SDL.SDL_CreateRGBSurfaceWithFormat(0, width, height, 32, format);

        SDL.SDL_Rect rect = new SDL.SDL_Rect()
        {
            x = 0,
            y = 0,
            w = width,
            h = height
        };

        var sur = Marshal.PtrToStructure<SDL.SDL_Surface>(surface);

        SDL.SDL_RenderReadPixels(_renderer, ref rect, format, sur.pixels, sur.pitch);
        if (savePath.EndsWith(".bmp", StringComparison.InvariantCultureIgnoreCase))
            SDL.SDL_SaveBMP(surface, savePath);
        else if (savePath.EndsWith(".jpg", StringComparison.InvariantCultureIgnoreCase))
            SDL_image.IMG_SaveJPG(surface, savePath, 85);
        else if (savePath.EndsWith(".jpg", StringComparison.InvariantCultureIgnoreCase))
            SDL_image.IMG_SavePNG(surface, savePath);
        else
            throw new MajorsilenceException("Only bmp, jpg, png file formats are supported.");

        SDL.SDL_FreeSurface(surface);
    }

    public void DrawColor(byte r, byte g, byte b, byte a)
    {
        SDL.SDL_SetRenderDrawColor(this, r, g, b, a);
    }
}