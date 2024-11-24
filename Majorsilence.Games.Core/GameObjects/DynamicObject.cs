using System;
using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Core.GameObjects;

public class DynamicObject : GameObject
{
    private readonly Texture _texture;
    public int Speed { get; set; }
    public HorizontalDirection DirectionX { get; set; } // -1 for left, 1 for right, 0 for no horizontal movement
    public VerticalDirection DirectionY { get; set; } // -1 for up, 1 for down, 0 for no vertical movement
    
    public DynamicObject(Texture texture)
    {
        _texture = texture;
    }

    public override void Update()
    {
        // Update position based on speed and direction
        X += Speed * (int)DirectionX;
        Y += Speed * (int)DirectionY;
    }


    public override void Render()
    {
        _texture.Render(X, Y);
    }
}