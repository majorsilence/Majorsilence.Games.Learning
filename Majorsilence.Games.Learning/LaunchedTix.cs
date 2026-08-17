using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Learning;

/// <summary>
/// A tix coin fired from the tix launcher: a short parabolic flight on its own
/// tiny gravity integrator before settling into a normal, collectible TixPickup
/// where it comes down.
///
/// It follows the engine's ground convention rather than DynamicObject's
/// machinery: <see cref="GroundZ"/> is supplied from outside each frame, by
/// whoever knows which tile the coin is currently over. A coin thrown onto a
/// raised terrace has to come to rest on the terrace, not at the deck level it
/// was launched from.
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

    /// <summary>
    /// Height of the ground beneath the coin right now, in the same units as Z.
    /// Set each frame by game code from the tile the coin is over - the same
    /// contract DynamicObject.GroundZ has with the player.
    /// </summary>
    public float GroundZ { get; set; }

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
        if (Z <= GroundZ)
        {
            Z = GroundZ;
            _landed = true;
        }
    }
}
