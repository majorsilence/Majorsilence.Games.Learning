using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Learning;

/// <summary>
/// A short-lived visual effect (wake foam, funnel smoke): drifts at a constant
/// world-pixel velocity (zero for wake, which should stay put as the ship sails
/// away from it) and expires after its lifespan. Game owns spawning/removal;
/// this class only tracks its own remaining time and simple linear motion.
/// </summary>
public class Particle : GameObject
{
    private readonly SpriteSheet _sheet;
    private float _preciseX;
    private float _preciseY;
    private bool _initialized;

    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float RemainingSeconds { get; set; }
    public bool IsExpired => RemainingSeconds <= 0f;

    public Particle(SpriteSheet sheet, float lifespanSeconds)
    {
        _sheet = sheet;
        RemainingSeconds = lifespanSeconds;
    }

    public override void Update(float deltaTime)
    {
        if (!_initialized)
        {
            _preciseX = X;
            _preciseY = Y;
            _initialized = true;
        }

        _preciseX += VelocityX * deltaTime;
        _preciseY += VelocityY * deltaTime;
        X = (int)MathF.Round(_preciseX);
        Y = (int)MathF.Round(_preciseY);

        RemainingSeconds -= deltaTime;
    }

    public override void Render(Camera camera)
    {
        if (IsExpired) return;
        var (screenX, screenY) = camera.WorldToScreen(X, Y - Z);
        _sheet.Render(screenX, screenY, 0);
    }
}
