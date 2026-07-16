using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Learning;

/// <summary>
/// A stationary crew member standing at a post (bridge, engine room, crow's nest).
/// Role is a free-form string ("captain", "engineer", "watcher") matching the
/// level entity's "role" property - Game looks it up to decide takeover effects.
/// </summary>
public class Npc : GameObject
{
    private readonly SpriteSheet _sheet;
    private readonly int _frameIndex;

    public string Role { get; }

    public Npc(SpriteSheet sheet, string role, int frameIndex = 0)
    {
        _sheet = sheet;
        _frameIndex = frameIndex;
        Role = role;
    }

    public override void Update(float deltaTime)
    {
        // Idle stand - no per-frame behavior in this pass.
    }

    public override void Render(Camera camera)
    {
        var (screenX, screenY) = camera.WorldToScreen(X, Y - Z);
        _sheet.Render(screenX, screenY, _frameIndex);
    }
}
