using System;
using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Core.GameObjects;

public class DynamicObject : GameObject
{
    private readonly SpriteSheet _spriteSheet;
    private Animation? _animation;
    private int _frameIndex;
    private float _preciseX;
    private float _preciseY;
    private bool _initialized;

    public float Speed { get; set; } // pixels per second
    public HorizontalDirection DirectionX { get; set; } // -1 for left, 1 for right, 0 for no horizontal movement
    public VerticalDirection DirectionY { get; set; } // -1 for up, 1 for down, 0 for no vertical movement

    // Minimal jump/gravity physics - deliberately simple, no general collision system.
    // GroundZ is externally supplied each frame by game code (e.g. looked up from
    // IsometricTilemap.GetElevationPixels for whichever tile this object currently
    // stands on) rather than derived internally, keeping DynamicObject decoupled
    // from any particular tilemap/projection.
    public float GroundZ { get; set; }
    public float JumpSpeed { get; set; } = 260f; // world pixels/sec initial upward velocity
    public float Gravity { get; set; } = 900f; // world pixels/sec^2
    private float _verticalVelocity;

    public bool IsGrounded => Z <= GroundZ && _verticalVelocity <= 0f;

    /// <summary>
    /// When true, the assigned Animation only advances while this object is
    /// actually moving (either direction non-zero), so a standing character
    /// doesn't march in place. Off by default to preserve existing behavior.
    /// </summary>
    public bool AnimateOnlyWhenMoving { get; set; }

    public DynamicObject(SpriteSheet spriteSheet)
    {
        _spriteSheet = spriteSheet;
    }

    public void Jump()
    {
        if (IsGrounded) _verticalVelocity = JumpSpeed;
    }

    public void SetAnimation(Animation animation)
    {
        _animation = animation;
        _animation.Reset();
    }

    public void SetFrame(int frameIndex)
    {
        _animation = null;
        _frameIndex = frameIndex;
    }

    /// <summary>
    /// Forces this object's position to (x, y), keeping the internal sub-pixel
    /// accumulator in sync. Plain X/Y assignment alone would be silently undone
    /// on the next Update() call, since movement is integrated from the internal
    /// accumulator, not from X/Y directly - use this for teleports/camera gates.
    /// </summary>
    public void SnapTo(int x, int y)
    {
        X = x;
        Y = y;
        _preciseX = x;
        _preciseY = y;
        _initialized = true;
    }

    public override void Update(float deltaTime)
    {
        if (!_initialized)
        {
            _preciseX = X;
            _preciseY = Y;
            _initialized = true;
        }

        // Update position based on speed and direction
        _preciseX += Speed * (int)DirectionX * deltaTime;
        _preciseY += Speed * (int)DirectionY * deltaTime;
        X = (int)MathF.Round(_preciseX);
        Y = (int)MathF.Round(_preciseY);

        _verticalVelocity -= Gravity * deltaTime;
        Z += _verticalVelocity * deltaTime;
        if (Z <= GroundZ)
        {
            Z = GroundZ;
            _verticalVelocity = 0f;
        }

        if (!AnimateOnlyWhenMoving || DirectionX != HorizontalDirection.None || DirectionY != VerticalDirection.None)
        {
            _animation?.Update(deltaTime);
        }
    }


    public override void Render(Camera camera)
    {
        var frame = _animation?.CurrentFrame ?? _frameIndex;
        var (screenX, screenY) = camera.WorldToScreen(X, Y - Z);
        _spriteSheet.Render(screenX, screenY, frame);
    }
}