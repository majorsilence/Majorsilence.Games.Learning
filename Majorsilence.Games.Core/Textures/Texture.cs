using System;
using Majorsilence.Games.Core.Surfaces;
using SDL3;

namespace Majorsilence.Games.Core.Textures;

public class Texture : IDisposable
{
    private IntPtr _texture;
    readonly Renderer _renderer;

    public SDL.Rect Rect { get; set; }

    /// <summary>
    /// Texture pixels per logical pixel: 1 for ordinary art, which is authored
    /// at the size it draws, and higher for anything rasterized at the display's
    /// real resolution (see CreateTextTexture). Render(x, y) divides by it, so
    /// callers position and measure in logical units either way.
    /// </summary>
    public float PixelScale { get; private set; } = 1f;

    /// <summary>This texture's size in logical pixels - what it actually covers on screen. Same as Rect for unscaled art.</summary>
    public int LogicalWidth => (int)MathF.Round(Rect.W / PixelScale);
    public int LogicalHeight => (int)MathF.Round(Rect.H / PixelScale);

    public Texture(Renderer renderer, Surfaces.Surface surface)
    {
        _texture = SDL.CreateTextureFromSurface(renderer, surface);
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
        SDL.GetTextureSize(_texture, out float texW, out float texH);
        SDL.FRect dstrect2 = new SDL.FRect { X = x, Y = y, W = texW / PixelScale, H = texH / PixelScale };

        SDL.RenderTexture(_renderer, _texture, IntPtr.Zero, dstrect2);
    }

    /// <summary>
    /// Render a sub-region of the texture (e.g. a single frame of a sprite sheet or tileset)
    /// at its native size.
    /// </summary>
    public virtual void Render(int x, int y, SDL.Rect srcRect)
    {
        Render(x, y, srcRect, srcRect.W, srcRect.H);
    }

    /// <summary>
    /// Render a sub-region of the texture at its native size, optionally
    /// mirrored horizontally (side-view sprites facing left/right).
    /// </summary>
    public virtual void Render(int x, int y, SDL.Rect srcRect, bool flipHorizontal)
    {
        if (!flipHorizontal)
        {
            Render(x, y, srcRect);
            return;
        }

        SDL.FRect srcFRect = new SDL.FRect { X = srcRect.X, Y = srcRect.Y, W = srcRect.W, H = srcRect.H };
        SDL.FRect dstRect = new SDL.FRect { X = x, Y = y, W = srcRect.W, H = srcRect.H };
        SDL.RenderTextureRotated(_renderer, _texture, in srcFRect, in dstRect, 0d, IntPtr.Zero, SDL.FlipMode.Horizontal);
    }

    /// <summary>
    /// Render a sub-region of the texture (e.g. a single frame of a sprite sheet or tileset)
    /// scaled to the given destination size.
    /// </summary>
    public virtual void Render(int x, int y, SDL.Rect srcRect, int destWidth, int destHeight)
    {
        SDL.FRect srcFRect = new SDL.FRect { X = srcRect.X, Y = srcRect.Y, W = srcRect.W, H = srcRect.H };
        SDL.FRect dstRect = new SDL.FRect { X = x, Y = y, W = destWidth, H = destHeight };
        SDL.RenderTexture(_renderer, _texture, srcFRect, dstRect);
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
        SDL.DestroyTexture(_texture);
        _disposed = true;
    }

    public static Texture CreateImageTexture(Renderer renderer, string filepath)
    {
        using var image = new ImageSurface(filepath);

        return new Texture(renderer, image);
    }
    public static Texture CreateImageTexture(Renderer renderer, string filepath,
        SDL.Color transparentColor)
    {
        using var image = new ImageSurface(filepath);
        image.ColorAsTransparent(transparentColor.R, transparentColor.G, transparentColor.B);

        return new Texture(renderer, image);
    }

    /// <summary>
    /// Renders text to a texture sized in logical pixels, as if the font were
    /// `size` points in the game's coordinate space - but rasterized at the
    /// display's real resolution. The game zooms its logical presentation to
    /// keep sprites a readable fraction of any screen (Renderer.
    /// SyncLogicalPresentationToWindow), and glyphs rasterized at logical size
    /// would be blown up by that same zoom from a ~12px bitmap, which is what
    /// made the HUD and menus hard to read. Text rebuilt after the window
    /// changes size picks up the new scale; until then it stays correctly
    /// sized, just rasterized for the old one.
    /// </summary>
    public static Texture CreateTextTexture(Renderer renderer, string fontPath, int size,
        SDL.Color color, string textValue)
    {
        var pixelSize = Math.Max(size, (int)MathF.Round(size * renderer.PixelsPerLogicalUnit));
        using var font = new Fonts(fontPath, pixelSize);
        using var text = new TextSurface(font, color, textValue);
        // The achieved scale, not the requested one - integer point sizes round.
        return new Texture(renderer, text) { PixelScale = pixelSize / (float)size };
    }
}
