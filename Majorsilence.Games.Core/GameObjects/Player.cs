using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Textures;
using SDL2;

namespace Majorsilence.Games.Core.GameObjects;

public class Player : DynamicObject
{
    public int Health { get; set; }
    
    public Player(Texture texture) : base(texture)
    {
    }
    
    public override void Update()
    {
        // Handle user input to update direction
        if (InputManager.IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_LEFT))
        {
            DirectionX = HorizontalDirection.Left;
        }
        else if (InputManager.IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_RIGHT))
        {
            DirectionX = HorizontalDirection.Right;
        }
        else
        {
            DirectionX =  HorizontalDirection.None;
        }

        if (InputManager.IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_UP))
        {
            DirectionY = VerticalDirection.Up;
        }
        else if (InputManager.IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_DOWN))
        {
            DirectionY = VerticalDirection.Down;
        }
        else
        {
            DirectionY = VerticalDirection.None;
        }

        // Update position based on speed and direction
        base.Update();
    }
    
}