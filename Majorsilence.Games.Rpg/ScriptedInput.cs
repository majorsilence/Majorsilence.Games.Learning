using Majorsilence.Games.Core.Input;

namespace Majorsilence.Games.Rpg;

/// <summary>
/// Replays a fixed sequence of inputs, so a run can walk itself through the
/// game with no hands on the keyboard - the only way to check movement,
/// collision, doorways and conversations without a person driving. Registered
/// as an ordinary IInputSource, so the game cannot tell it from a keyboard.
///
/// The script is a comma-separated list of `action[:seconds]` steps, each
/// action one or more of left/right/up/down/confirm/cancel joined by '+':
///
///   "right:2,up:0.5,confirm,confirm,down:1"
///
/// A step with no duration is a tap (long enough to register as a press, short
/// enough not to repeat). Steps run in order; when the last one finishes the
/// source goes quiet and Finished flips, which the caller can use to end a run.
/// </summary>
public class ScriptedInput : IInputSource
{
    private const float TapSeconds = 0.06f;

    private readonly List<(InputAction[] Actions, float Seconds)> _steps = new();
    private readonly HashSet<InputAction> _current = new();
    private readonly HashSet<InputAction> _previous = new();
    private int _index;
    private float _elapsed;

    public ScriptedInput(string script)
    {
        foreach (var step in script.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = step.Split(':');
            var actions = parts[0].Split('+', StringSplitOptions.RemoveEmptyEntries)
                .Select(Parse)
                .Where(a => a is not null)
                .Select(a => a!.Value)
                .ToArray();
            _steps.Add((actions, parts.Length > 1 ? float.Parse(parts[1]) : TapSeconds));
        }
    }

    public bool Finished => _index >= _steps.Count;

    private static InputAction? Parse(string name) => name.Trim().ToLowerInvariant() switch
    {
        "left" => InputAction.MoveLeft,
        "right" => InputAction.MoveRight,
        "up" => InputAction.MoveUp,
        "down" => InputAction.MoveDown,
        "confirm" => InputAction.Confirm,
        "cancel" => InputAction.Cancel,
        "jump" => InputAction.Jump,
        "fire" => InputAction.Fire,
        "idle" => null,
        _ => null
    };

    /// <summary>Advances the script by one frame. Call once per frame, before anything reads input.</summary>
    public void Advance(float deltaTime)
    {
        _previous.Clear();
        foreach (var action in _current) _previous.Add(action);
        _current.Clear();
        if (Finished) return;

        _elapsed += deltaTime;
        var (actions, seconds) = _steps[_index];
        if (_elapsed >= seconds)
        {
            _elapsed = 0f;
            _index++;
        }

        foreach (var action in actions) _current.Add(action);
    }

    public bool IsActionPressed(InputAction action) => _current.Contains(action);
    public bool IsActionJustPressed(InputAction action) => _current.Contains(action) && !_previous.Contains(action);
    public bool IsActionJustReleased(InputAction action) => !_current.Contains(action) && _previous.Contains(action);
}
