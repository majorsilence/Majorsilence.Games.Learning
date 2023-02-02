using System;
using Majorsilence.Games.Learning.Surfaces;
using Majorsilence.Games.Learning.Textures;

namespace Majorsilence.Games.Learning;

public class PlaceholderMovingObject
{
    private readonly Texture _texture;

    public PlaceholderMovingObject(Texture texture)
    {
        _texture = texture;
    }

    /// <summary>
    /// Higher value is on top
    /// </summary>
    public int ZIndex { get; set; } = 1;

    public void Render(int x, int y)
    {
        _texture.Render(x, y);
    }
}