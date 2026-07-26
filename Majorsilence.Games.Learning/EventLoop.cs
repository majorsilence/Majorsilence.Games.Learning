using System;
using System.Runtime.InteropServices;
using Majorsilence.Games.Core;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;
using SDL3;

namespace Majorsilence.Games.Learning;

public class EventLoop
{
    private readonly Renderer _renderer;
    private bool _stopRequested;

    public EventLoop(Renderer renderer)
    {
        _renderer = renderer;
    }

    /// <summary>Ends the next Start() call's loop on the following frame, without quitting the app (SDL stays initialized) - e.g. a title screen handing off to the real game loop. Distinct from the player pressing Quit: see QuitRequested.</summary>
    public void Stop() => _stopRequested = true;

    /// <summary>True if the most recent Start() call ended because the player pressed Quit, rather than via Stop() - callers that chain loops (title screen -> game) use this to tell "move on" from "the whole app should exit".</summary>
    public bool QuitRequested { get; private set; }

    /// <summary>
    /// beforeUpdate runs once per frame, before any GameObject.Update - e.g. to sync
    /// a player's GroundZ from whichever tile they're currently over. afterUpdate runs
    /// once per frame, after every GameObject.Update but before the camera/render pass -
    /// e.g. to check the player's post-move tile for collision/hazards/doors, where the
    /// check needs this frame's final position. Both receive this frame's deltaTime so
    /// callers can drive their own timers (e.g. a scripted voyage clock) off the same
    /// clamped time source as movement, rather than reading it independently. Both are
    /// kept as plain hooks rather than baking tilemap-awareness into EventLoop itself,
    /// since EventLoop has no reason to know about any particular Tilemap/projection.
    /// </summary>
    public void Start(List<GameObject> gameObjects, Camera camera, Action<float>? beforeUpdate = null, Action<float>? afterUpdate = null)
    {
        var quit = false;
        _stopRequested = false;
        var frequency = SDL.GetPerformanceFrequency();
        var previousCounter = SDL.GetPerformanceCounter();

        while (!quit && !_stopRequested)
        {
            // main game loop

            var currentCounter = SDL.GetPerformanceCounter();
            // clamp to avoid a runaway delta after a debugger pause or window-drag stall
            var deltaTime = Math.Min((float)(currentCounter - previousCounter) / frequency, 0.25f);
            previousCounter = currentCounter;

            InputManager.Update();

            // see https://github.com/libsdl-org/SDL/issues/4376#issuecomment-841654449
            // event loop is done this way because SDL_PollEvent was extremely slow
            // when developing on mac book air

            if (InputActions.IsPressed(InputAction.Quit))
            {
                quit = true;
            }

            if (InputActions.IsJustReleased(InputAction.ToggleFullscreen))
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


            beforeUpdate?.Invoke(deltaTime);
            foreach (var obj in gameObjects)
            {
                obj.Update(deltaTime);
            }
            afterUpdate?.Invoke(deltaTime);
            camera.Update();

            _renderer.Clear();
            RenderQueue.RenderSorted(gameObjects, camera);
            _renderer.Present();
        }

        QuitRequested = quit;
    }
}
