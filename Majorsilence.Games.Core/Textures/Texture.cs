using System;
using Majorsilence.Games.Core.Surfaces;
using SDL2;

namespace Majorsilence.Games.Core.Textures;

public class Texture : IDisposable
{
    private IntPtr _texture;
    readonly Renderer _renderer;

    public SDL2.SDL.SDL_Rect Rect { get; set; }
    
    public Texture(Renderer renderer, Surfaces.Surface surface)
    {
        _texture = SDL.SDL_CreateTextureFromSurface(renderer, surface);
        _renderer = renderer;
        Rect = surface.Rect;
    }

    public static implicit operator IntPtr(Texture ap)
    {
        if (ap._disposed) return IntPtr.Zero;
        return ap._texture;
    }

    public virtual void Render(int x, int y)
    {
        var texW = 0;
        var texH = 0;
        SDL.SDL_QueryTexture(_texture, out uint format, out int access, out texW, out texH);
        SDL.SDL_Rect dstrect2 = new SDL.SDL_Rect { x = x, y = y, w = texW, h = texH };

        SDL.SDL_RenderCopy(_renderer, _texture, IntPtr.Zero, ref dstrect2);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    protected bool _disposed;

    protected void Dispose(bool disposing)
    {
        if (_disposed) return;

        // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
        // TODO: set large fields to null.
        SDL.SDL_DestroyTexture(_texture);
        _disposed = true;
    }

    public static Texture CreateImageTexture(Renderer renderer, string filepath)
    {
        using var image = new ImageSurface(filepath);

        return new Texture(renderer, image);
    }
    public static Texture CreateImageTexture(Renderer renderer, string filepath,
        SDL2.SDL.SDL_Color transparentColor)
    {
        using var image = new ImageSurface(filepath);
        image.ColorAsTransparent(transparentColor.r, transparentColor.g, transparentColor.b);

        return new Texture(renderer, image);
    }
    
    public static Texture CreateTextTexture(Renderer renderer, string fontPath, int size,
        SDL2.SDL.SDL_Color color, string textValue)
    {
        using var font = new Fonts(fontPath, size);
        using var text = new TextSurface(font, color, textValue);
        return new Texture(renderer, text);
    }
}