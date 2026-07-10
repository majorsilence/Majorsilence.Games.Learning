namespace Majorsilence.Games.Core.Input;

/// <summary>
/// A device-specific source of InputAction states (e.g. keyboard, touch, gamepad).
/// Multiple sources can be registered with InputActions so game code checks actions,
/// not devices.
/// </summary>
public interface IInputSource
{
    bool IsActionPressed(InputAction action);
    bool IsActionJustPressed(InputAction action);
    bool IsActionJustReleased(InputAction action);
}
