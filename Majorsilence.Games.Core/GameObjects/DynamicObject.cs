using System;
using Majorsilence.Games.Core.Input;
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

    public DynamicObject(SpriteSheet spriteSheet)
    {
        _spriteSheet = spriteSheet;
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

        _animation?.Update(deltaTime);
    }


    public override void Render()
    {
        var frame = _animation?.CurrentFrame ?? _frameIndex;
        _spriteSheet.Render(X, Y, frame);
    }
}