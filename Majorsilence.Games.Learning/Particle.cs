using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Learning;

/// <summary>
/// A short-lived visual effect (wake foam, funnel smoke): drifts at a constant
/// world-pixel velocity (zero for wake, which should stay put as the ship sails
/// away from it), optionally rises in Z (height, not world Y - see RiseSpeed),
/// and expires after its lifespan. Game owns spawning/removal; this class only
/// tracks its own remaining time and simple linear motion.
/// </summary>
public class Particle : GameObject
{
    private readonly SpriteSheet _sheet;
    private float _preciseX;
    private float _preciseY;
    private float _spawnSortY;
    private bool _initialized;

    public float VelocityX { get; set; }
    public float VelocityY { get; set; }

    /// <summary>World pixels/second climbed in Z (height) - visually rises without moving the world X/Y position isometric depth-sorting is based on.</summary>
    public float RiseSpeed { get; set; }

    public float RemainingSeconds { get; set; }
    public bool IsExpired => RemainingSeconds <= 0f;

    public Particle(SpriteSheet sheet, float lifespanSeconds)
    {
        _sheet = sheet;
        RemainingSeconds = lifespanSeconds;
    }

    /// <summary>
    /// Depth-sorts at its position when spawned, not its live (possibly drifting)
    /// position - a moving particle's world Y otherwise corrupts painter's-algorithm
    /// order against the tile it's currently over (e.g. "rising" smoke would sort
    /// as further away and vanish behind nearby ground tiles).
    /// </summary>
    public override float SortY => _spawnSortY;

    public override void Update(float deltaTime)
    {
        if (!_initialized)
        {
            _preciseX = X;
            _preciseY = Y;
            _spawnSortY = Y + SortOffsetY;
            _initialized = true;
        }

        _preciseX += VelocityX * deltaTime;
        _preciseY += VelocityY * deltaTime;
        X = (int)MathF.Round(_preciseX);
        Y = (int)MathF.Round(_preciseY);
        Z += RiseSpeed * deltaTime;

        RemainingSeconds -= deltaTime;
    }

    public override void Render(Camera camera)
    {
        if (IsExpired) return;
        var (screenX, screenY) = camera.WorldToScreen(X, Y - Z);
        _sheet.Render(screenX, screenY, 0);
    }
}
