using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Core.GameObjects;

public class Player : DynamicObject
{
    public int Health { get; set; }

    public Player(SpriteSheet spriteSheet) : base(spriteSheet)
    {
    }

    public override void Update(float deltaTime)
    {
        // Handle user input to update direction
        if (InputActions.IsPressed(InputAction.MoveLeft))
        {
            DirectionX = HorizontalDirection.Left;
        }
        else if (InputActions.IsPressed(InputAction.MoveRight))
        {
            DirectionX = HorizontalDirection.Right;
        }
        else
        {
            DirectionX =  HorizontalDirection.None;
        }

        if (InputActions.IsPressed(InputAction.MoveUp))
        {
            DirectionY = VerticalDirection.Up;
        }
        else if (InputActions.IsPressed(InputAction.MoveDown))
        {
            DirectionY = VerticalDirection.Down;
        }
        else
        {
            DirectionY = VerticalDirection.None;
        }

        // Update position based on speed and direction
        base.Update(deltaTime);
    }

}