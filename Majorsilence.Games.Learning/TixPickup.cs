using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Learning;

/// <summary>
/// A ship-tix pickup sitting at a world position. Game removes it from the world
/// and credits Value to the balance when a player gets close enough (proximity
/// in world pixels, not tile-exact - simplest and most forgiving check, and the
/// only option once LaunchedTix scatters pickups off the tile grid).
/// </summary>
public class TixPickup : GameObject
{
    protected readonly SpriteSheet Sheet;

    public int Value { get; set; } = 10;

    public TixPickup(SpriteSheet sheet)
    {
        Sheet = sheet;
    }

    public override void Update(float deltaTime)
    {
    }

    public override void Render(Camera camera)
    {
        var (screenX, screenY) = camera.WorldToScreen(X, Y - Z);
        Sheet.Render(screenX, screenY, 0);
    }
}
