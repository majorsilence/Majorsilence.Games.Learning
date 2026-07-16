using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;

namespace Majorsilence.Games.Learning;

/// <summary>
/// An invisible GameObject whose position tracks the midpoint of two other
/// GameObjects, so Camera.Target (which follows a single object) can center a
/// 2-player co-op view between both players without Camera needing to know
/// about multiple targets.
/// </summary>
public class MidpointAnchor : GameObject
{
    private readonly GameObject _a;
    private readonly GameObject _b;

    public MidpointAnchor(GameObject a, GameObject b)
    {
        _a = a;
        _b = b;
    }

    public override void Update(float deltaTime)
    {
        X = (_a.X + _b.X) / 2;
        Y = (_a.Y + _b.Y) / 2;
    }

    public override void Render(Camera camera)
    {
        // Invisible - exists only to be a Camera.Target.
    }
}
