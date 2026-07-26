using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Core.GameObjects;

public class Player : DynamicObject
{
    private readonly IInputSource? _inputSource;

    public int Health { get; set; }

    /// <summary>
    /// When false, this player's movement/jump input is ignored (direction reset
    /// to none) while physics/animation still run - e.g. a brief freeze during a
    /// death sequence.
    /// </summary>
    public bool InputEnabled { get; set; } = true;

    /// <summary>
    /// Optional per-player input device. Null (the default) reads the shared
    /// static InputActions facade, matching prior single-player behavior. Pass a
    /// dedicated IInputSource (e.g. a second KeyboardInputSource with different
    /// bindings) so multiple local players don't share one input state.
    /// </summary>
    public Player(SpriteSheet spriteSheet, IInputSource? inputSource = null) : base(spriteSheet)
    {
        _inputSource = inputSource;
    }

    private bool IsPressed(InputAction action) => _inputSource?.IsActionPressed(action) ?? InputActions.IsPressed(action);
    private bool IsJustPressed(InputAction action) => _inputSource?.IsActionJustPressed(action) ?? InputActions.IsJustPressed(action);

    public override void Update(float deltaTime)
    {
        if (!InputEnabled)
        {
            DirectionX = HorizontalDirection.None;
            DirectionY = VerticalDirection.None;
            base.Update(deltaTime);
            return;
        }

        // Handle user input to update direction
        if (IsPressed(InputAction.MoveLeft))
        {
            DirectionX = HorizontalDirection.Left;
        }
        else if (IsPressed(InputAction.MoveRight))
        {
            DirectionX = HorizontalDirection.Right;
        }
        else
        {
            DirectionX =  HorizontalDirection.None;
        }

        // In side view the sprite faces its travel direction (and keeps facing
        // it while standing still). Isometric art has no facing, so don't flip.
        if (Platformer is not null)
        {
            if (DirectionX == HorizontalDirection.Left) FlipHorizontal = true;
            else if (DirectionX == HorizontalDirection.Right) FlipHorizontal = false;
        }

        if (IsPressed(InputAction.MoveUp))
        {
            DirectionY = VerticalDirection.Up;
        }
        else if (IsPressed(InputAction.MoveDown))
        {
            DirectionY = VerticalDirection.Down;
        }
        else
        {
            DirectionY = VerticalDirection.None;
        }

        if (IsJustPressed(InputAction.Jump))
        {
            // Down+Jump on a one-way platform drops through it instead of jumping.
            if (Platformer is not null && Platformer.OnGround && IsPressed(InputAction.MoveDown))
                Platformer.RequestDropThrough();
            else
                Jump();
        }

        // Update position based on speed and direction
        base.Update(deltaTime);
    }

}