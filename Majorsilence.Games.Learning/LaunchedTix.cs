using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Learning;

/// <summary>
/// A tix coin fired from the tix launcher: a short parabolic flight (its own
/// tiny gravity integrator, independent of DynamicObject/GroundZ since it isn't
/// tied to a tilemap's elevation) before settling into a normal, collectible
/// TixPickup at its landing spot.
/// </summary>
public class LaunchedTix : TixPickup
{
    private float _preciseX;
    private float _preciseY;
    private float _velocityX;
    private float _velocityY;
    private float _velocityZ;
    private bool _landed;
    private bool _initialized;

    public float Gravity { get; set; } = 900f;
    public bool Landed => _landed;

    public LaunchedTix(SpriteSheet sheet, float velocityX, float velocityY, float velocityZ) : base(sheet)
    {
        _velocityX = velocityX;
        _velocityY = velocityY;
        _velocityZ = velocityZ;
    }

    public override void Update(float deltaTime)
    {
        if (_landed) return;

        if (!_initialized)
        {
            _preciseX = X;
            _preciseY = Y;
            _initialized = true;
        }

        _preciseX += _velocityX * deltaTime;
        _preciseY += _velocityY * deltaTime;
        X = (int)MathF.Round(_preciseX);
        Y = (int)MathF.Round(_preciseY);

        _velocityZ -= Gravity * deltaTime;
        Z += _velocityZ * deltaTime;
        if (Z <= 0f)
        {
            Z = 0f;
            _landed = true;
        }
    }
}
