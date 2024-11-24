using System;
using System.Runtime.InteropServices;
using Majorsilence.Games.Core;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Textures;
using SDL2;

namespace Majorsilence.Games.Learning;

public class EventLoop
{
    private readonly Renderer _renderer;

    public EventLoop(Renderer renderer)
    {
        _renderer = renderer;
    }

    public void Start(List<GameObject> gameObjects)
    {
        var quit = false;
        SDL.SDL_Event sdlEvent;
        var x = 288;
        var y = 208;

        const int SIZE_EV = 64;


        //SDL.SDL_Event[] es = new SDL.SDL_Event[SIZE_EV];
        while (!quit)
        {
            // main game loop


            InputManager.Update();

            // see https://github.com/libsdl-org/SDL/issues/4376#issuecomment-841654449
            // event loop is done this way because SDL_PollEvent was extremely slow
            // when developing on mac book air

            //SDL.SDL_WaitEvent(out sdlEvent);

            if (InputManager.IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_Q))
            {
                quit = true;
            }

            if (InputManager.IsKeyJustReleased(SDL.SDL_Scancode.SDL_SCANCODE_F))
            {
                _renderer.SetFullscreen(!_renderer.IsFullscreen);
            }

            if (InputManager.IsCtrlPressed())
            {
                // Handle Ctrl key pressed
            }

            if (InputManager.IsAltPressed())
            {
                // Handle Alt key pressed
            }

            if (InputManager.IsShiftPressed())
            {
                // Handle Shift key pressed
            }

            
            _renderer.Clear();

            foreach (var obj in gameObjects)
            {
                obj.Update();
                obj.Render();
            }
            _renderer.Present();
        }
    }
}