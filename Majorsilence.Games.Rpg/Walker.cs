using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Physics;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Rpg;

public enum Facing
{
    Down = 0,
    Up = 1,
    Left = 2,
    Right = 3
}

/// <summary>
/// A character on a top-down map - the hero and every townsperson alike. Owns
/// its sub-pixel position, which way it faces, and a two-pose walk cycle;
/// collision comes from an optional TopDownBody (the hero has one, standing
/// NPCs don't need it).
///
/// Sprite sheets are laid out as four facings of two poses, in Facing order:
/// down, up, left, right. A sheet can hold several characters back to back -
/// FrameBase picks which one, so all the townsfolk share a single texture.
/// </summary>
public class Walker : GameObject
{
    private const float WalkFrameSeconds = 0.18f;

    private readonly SpriteSheet _sheet;
    private float _preciseX;
    private float _preciseY;
    private float _animationClock;
    private bool _initialized;

    public Walker(SpriteSheet sheet, int frameBase = 0)
    {
        _sheet = sheet;
        FrameBase = frameBase;
    }

    /// <summary>First frame of this character's 8-frame block within the sheet.</summary>
    public int FrameBase { get; }

    public TopDownBody? Body { get; set; }

    public Facing Facing { get; set; } = Facing.Down;

    /// <summary>World pixels per second when walking.</summary>
    public float Speed { get; set; } = 60f;

    public int DirectionX { get; set; }
    public int DirectionY { get; set; }

    public float PreciseX => _preciseX;
    public float PreciseY => _preciseY;

    /// <summary>Moves to an exact position, keeping the sub-pixel accumulator in step - plain X/Y assignment would be undone by the next Update.</summary>
    public void SnapTo(int x, int y)
    {
        X = x;
        Y = y;
        _preciseX = x;
        _preciseY = y;
        _initialized = true;
    }

    /// <summary>Faces the given intent without moving - so an NPC turns to answer, and the hero keeps facing the way they last walked.</summary>
    public void FaceTowards(int directionX, int directionY)
    {
        if (directionY > 0) Facing = Facing.Down;
        else if (directionY < 0) Facing = Facing.Up;
        else if (directionX < 0) Facing = Facing.Left;
        else if (directionX > 0) Facing = Facing.Right;
    }

    public override void Update(float deltaTime)
    {
        if (!_initialized)
        {
            _preciseX = X;
            _preciseY = Y;
            _initialized = true;
        }

        var moving = DirectionX != 0 || DirectionY != 0;
        if (moving)
        {
            FaceTowards(DirectionX, DirectionY);
            if (Body is not null)
                Body.MoveAndCollide(ref _preciseX, ref _preciseY, DirectionX, DirectionY, Speed, deltaTime);
            else
            {
                _preciseX += DirectionX * Speed * deltaTime;
                _preciseY += DirectionY * Speed * deltaTime;
            }

            X = (int)MathF.Round(_preciseX);
            Y = (int)MathF.Round(_preciseY);
            _animationClock += deltaTime;
        }
        else
        {
            // Standing still resets to the neutral pose rather than freezing
            // mid-stride, which reads as a character caught in a stumble.
            _animationClock = 0f;
        }
    }

    public override void Render(Camera camera)
    {
        var pose = (int)(_animationClock / WalkFrameSeconds) % 2;
        var frame = FrameBase + (int)Facing * 2 + pose;
        var (screenX, screenY) = camera.WorldToScreen(X, Y);
        _sheet.Render(screenX, screenY, frame);
    }
}
