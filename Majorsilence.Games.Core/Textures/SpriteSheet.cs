using SDL3;

namespace Majorsilence.Games.Core.Textures;

/// <summary>
/// Slices a texture into a grid of equally-sized frames, letting individual
/// frames be addressed by index or by (column, row) and rendered directly.
/// </summary>
public class SpriteSheet
{
    public Texture Texture { get; }
    public int FrameWidth { get; }
    public int FrameHeight { get; }
    public int Columns { get; }
    public int Rows { get; }
    public int FrameCount => Columns * Rows;

    public SpriteSheet(Texture texture, int frameWidth, int frameHeight)
    {
        if (frameWidth <= 0 || frameHeight <= 0)
            throw new MajorsilenceException("Sprite sheet frame dimensions must be greater than zero.");

        Texture = texture;
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        Columns = Math.Max(1, texture.Rect.W / frameWidth);
        Rows = Math.Max(1, texture.Rect.H / frameHeight);
    }

    public SDL.Rect GetFrameRect(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= FrameCount)
            throw new MajorsilenceException($"Frame index {frameIndex} is out of range (0-{FrameCount - 1}).");

        return GetFrameRect(frameIndex % Columns, frameIndex / Columns);
    }

    public SDL.Rect GetFrameRect(int column, int row)
    {
        return new SDL.Rect
        {
            X = column * FrameWidth,
            Y = row * FrameHeight,
            W = FrameWidth,
            H = FrameHeight
        };
    }

    public void Render(int x, int y, int frameIndex)
    {
        Texture.Render(x, y, GetFrameRect(frameIndex));
    }

    public void Render(int x, int y, int frameIndex, bool flipHorizontal)
    {
        Texture.Render(x, y, GetFrameRect(frameIndex), flipHorizontal);
    }

    public void Render(int x, int y, int column, int row)
    {
        Texture.Render(x, y, GetFrameRect(column, row));
    }

    public void Render(int x, int y, int frameIndex, int destWidth, int destHeight)
    {
        Texture.Render(x, y, GetFrameRect(frameIndex), destWidth, destHeight);
    }
}
