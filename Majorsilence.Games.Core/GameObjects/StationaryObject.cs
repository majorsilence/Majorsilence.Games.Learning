using System;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Core.GameObjects;

/// <summary>
/// A fixed screen-space/UI object (e.g. a HUD label) - X/Y are screen pixels,
/// not world position, so unlike other GameObjects it deliberately ignores the
/// camera and never pans/scrolls with the world.
/// </summary>
public class StationaryObject : GameObject
{
    private readonly Texture _texture;

    public StationaryObject(Texture texture)
    {
        _texture = texture;
    }

    public override void Update(float deltaTime)
    {
        // Stationary objects do not need to update position or state
    }

    public override void Render(Camera camera)
    {
        _texture.Render(X, Y);
    }
}