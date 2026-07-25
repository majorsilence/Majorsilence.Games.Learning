using System.Collections.Generic;
using System.Linq;
using SDL3;

namespace Majorsilence.Games.Core.Input;

public class KeyboardInputSource : IInputSource
{
    private readonly Dictionary<InputAction, SDL.Scancode[]> _bindings;

    public KeyboardInputSource(Dictionary<InputAction, SDL.Scancode[]>? bindings = null)
    {
        _bindings = bindings ?? new Dictionary<InputAction, SDL.Scancode[]>
        {
            [InputAction.MoveUp] = new[] { SDL.Scancode.Up, SDL.Scancode.W },
            [InputAction.MoveDown] = new[] { SDL.Scancode.Down, SDL.Scancode.S },
            [InputAction.MoveLeft] = new[] { SDL.Scancode.Left, SDL.Scancode.A },
            [InputAction.MoveRight] = new[] { SDL.Scancode.Right, SDL.Scancode.D },
            [InputAction.Jump] = new[] { SDL.Scancode.Space },
            [InputAction.Fire] = new[] { SDL.Scancode.E },
            [InputAction.Confirm] = new[] { SDL.Scancode.Return },
            [InputAction.Cancel] = new[] { SDL.Scancode.Escape },
            [InputAction.ToggleFullscreen] = new[] { SDL.Scancode.F },
            // Escape deliberately stays off Quit (it doubles as Cancel above) -
            // otherwise closing a UI panel with Esc would exit the whole game.
            [InputAction.Quit] = new[] { SDL.Scancode.Q },
        };
    }

    public bool IsActionPressed(InputAction action) =>
        _bindings.TryGetValue(action, out var keys) && keys.Any(InputManager.IsKeyPressed);

    public bool IsActionJustPressed(InputAction action) =>
        _bindings.TryGetValue(action, out var keys) && keys.Any(InputManager.IsKeyJustPressed);

    public bool IsActionJustReleased(InputAction action) =>
        _bindings.TryGetValue(action, out var keys) && keys.Any(InputManager.IsKeyJustReleased);
}
