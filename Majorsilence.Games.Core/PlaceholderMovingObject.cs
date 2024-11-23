using System;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Core;

public class PlaceholderMovingObject
{
    private readonly Texture _texture;
    private int _x, _y;

    public PlaceholderMovingObject(Texture texture)
    {
        _texture = texture;
    }

    /// <summary>
    /// Higher value is on top
    /// </summary>
    public int ZIndex { get; set; } = 1;
    
    public void SetPosition(int x, int y)
    {
        _x = x;
        _y = y;
    }
    
    public void Move(int x, int y)
    {
        _x += x;
        _y += y;
    }

    public void Render()
    {
        _texture.Render(_x, _y);
    }
}