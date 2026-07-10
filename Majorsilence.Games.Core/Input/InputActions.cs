using System.Collections.Generic;
using System.Linq;

namespace Majorsilence.Games.Core.Input;

/// <summary>
/// Entry point game code uses to query InputAction state, backed by one or more
/// IInputSource devices. Keyboard is registered by default; touch/gamepad sources
/// can be added later via RegisterSource without changing any call sites.
/// </summary>
public static class InputActions
{
    private static readonly List<IInputSource> _sources = new() { new KeyboardInputSource() };

    public static void RegisterSource(IInputSource source) => _sources.Add(source);

    public static bool IsPressed(InputAction action) => _sources.Any(s => s.IsActionPressed(action));
    public static bool IsJustPressed(InputAction action) => _sources.Any(s => s.IsActionJustPressed(action));
    public static bool IsJustReleased(InputAction action) => _sources.Any(s => s.IsActionJustReleased(action));
}
