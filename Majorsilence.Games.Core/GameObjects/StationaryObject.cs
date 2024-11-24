using System;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Core.GameObjects;

public class StationaryObject : GameObject
{
    private readonly Texture _texture;

    public StationaryObject(Texture texture)
    {
        _texture = texture;
    }

    public override void Update()
    {
        // Stationary objects do not need to update position or state
    }

    public override void Render()
    {
        _texture.Render(X, Y);
    }
}