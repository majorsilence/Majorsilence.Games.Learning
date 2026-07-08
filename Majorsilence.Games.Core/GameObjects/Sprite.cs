using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Core.GameObjects;

/// <summary>
/// A game object backed by a sprite sheet, rendering either a fixed frame
/// or the current frame of an assigned Animation.
/// </summary>
public class Sprite : GameObject
{
    private readonly SpriteSheet _spriteSheet;
    private Animation? _animation;
    private int _frameIndex;

    public Sprite(SpriteSheet spriteSheet, int frameIndex = 0)
    {
        _spriteSheet = spriteSheet;
        _frameIndex = frameIndex;
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

    public override void Update()
    {
        _animation?.Update();
    }

    public override void Render()
    {
        var frame = _animation?.CurrentFrame ?? _frameIndex;
        _spriteSheet.Render(X, Y, frame);
    }
}
