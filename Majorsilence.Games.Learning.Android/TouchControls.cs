using Majorsilence.Games.Core;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;
using SDL3;

namespace Majorsilence.Games.Learning.Android;

/// <summary>
/// On-screen touch controls: a virtual 8-way d-pad on the left and JUMP/ACT/TIX
/// buttons on the right. Doubles as an IInputSource registered with InputActions,
/// so the shared game code keeps reading InputActions with no idea a touchscreen
/// is behind it. Lives in game.GameObjects (persists across room changes, like
/// the Hud) and renders in screen space above everything via a huge SortOffsetY.
/// </summary>
public class TouchControls : GameObject, IInputSource
{
    private readonly Renderer _renderer;
    private readonly Texture _dpad;
    private readonly Dictionary<InputAction, (Texture Idle, Texture Pressed)> _buttonArt = new();

    private HashSet<InputAction> _current = new();
    private HashSet<InputAction> _previous = new();

    // Layout, recomputed each Update from the renderer's logical size (touch
    // coordinates are normalized to the window, and logical presentation
    // stretches over the whole window, so normalized * logical maps exactly).
    private (float X, float Y, float Radius) _dpadZone;
    private readonly Dictionary<InputAction, (float X, float Y, float Radius)> _buttonZones = new();

    // A direction counts once the touch is this far (fraction of the pad radius)
    // from the pad center; between the two thresholds both axes can be active,
    // giving 8-way movement.
    private const float DeadZoneFraction = 0.22f;
    // Touches slightly beyond the pad's visible edge still steer, so a thumb
    // sliding off mid-drag doesn't drop movement.
    private const float DpadGraceFraction = 1.6f;

    public TouchControls(Renderer renderer)
    {
        _renderer = renderer;
        SortOffsetY = 1_000_000f; // screen-space overlay: always on top
        _dpad = Texture.CreateImageTexture(renderer, "assets/artwork/touch/dpad.png");
        foreach (var (action, name) in new[]
                 {
                     (InputAction.Jump, "jump"),
                     (InputAction.Confirm, "act"),
                     (InputAction.Fire, "tix"),
                 })
        {
            _buttonArt[action] = (
                Texture.CreateImageTexture(renderer, $"assets/artwork/touch/btn-{name}.png"),
                Texture.CreateImageTexture(renderer, $"assets/artwork/touch/btn-{name}-pressed.png"));
        }
    }

    public bool IsActionPressed(InputAction action) => _current.Contains(action);
    public bool IsActionJustPressed(InputAction action) => _current.Contains(action) && !_previous.Contains(action);
    public bool IsActionJustReleased(InputAction action) => !_current.Contains(action) && _previous.Contains(action);

    public override void Update(float deltaTime)
    {
        LayoutForWindow();

        (_previous, _current) = (_current, _previous);
        _current.Clear();

        var (w, h) = _renderer.LogicalSize;
        foreach (var touch in InputManager.Touches.Values)
        {
            var x = touch.X * w;
            var y = touch.Y * h;

            var dpadDx = x - _dpadZone.X;
            var dpadDy = y - _dpadZone.Y;
            var dpadDist = MathF.Sqrt(dpadDx * dpadDx + dpadDy * dpadDy);
            if (dpadDist >= _dpadZone.Radius * DeadZoneFraction && dpadDist <= _dpadZone.Radius * DpadGraceFraction)
            {
                // 8-way: an axis engages once it carries a meaningful share of
                // the offset, so pure horizontal/vertical drags don't jitter
                // diagonally but true diagonals engage both.
                if (dpadDx <= -0.38f * dpadDist) _current.Add(InputAction.MoveLeft);
                if (dpadDx >= 0.38f * dpadDist) _current.Add(InputAction.MoveRight);
                if (dpadDy <= -0.38f * dpadDist) _current.Add(InputAction.MoveUp);
                if (dpadDy >= 0.38f * dpadDist) _current.Add(InputAction.MoveDown);
            }

            foreach (var (action, zone) in _buttonZones)
            {
                var dx = x - zone.X;
                var dy = y - zone.Y;
                if (dx * dx + dy * dy <= zone.Radius * zone.Radius * 1.3f)
                {
                    _current.Add(action);
                }
            }
        }
    }

    private void LayoutForWindow()
    {
        var (w, h) = _renderer.LogicalSize;
        var s = Math.Min(w, h);
        var margin = 0.05f * s;

        var dpadRadius = 0.17f * s;
        _dpadZone = (margin + dpadRadius, h - margin - dpadRadius, dpadRadius);

        var buttonRadius = 0.075f * s;
        var jump = (w - margin - buttonRadius, h - margin - buttonRadius, buttonRadius);
        _buttonZones[InputAction.Jump] = jump;
        _buttonZones[InputAction.Confirm] = (jump.Item1, jump.Item2 - buttonRadius * 2.6f, buttonRadius);
        _buttonZones[InputAction.Fire] = (jump.Item1 - buttonRadius * 2.6f, jump.Item2, buttonRadius);
    }

    public override void Render(Camera camera)
    {
        RenderCircle(_dpad, _dpadZone);
        foreach (var (action, zone) in _buttonZones)
        {
            var art = _buttonArt[action];
            RenderCircle(IsActionPressed(action) ? art.Pressed : art.Idle, zone);
        }
    }

    private static void RenderCircle(Texture texture, (float X, float Y, float Radius) zone)
    {
        var size = (int)(zone.Radius * 2);
        texture.Render((int)(zone.X - zone.Radius), (int)(zone.Y - zone.Radius),
            texture.Rect, size, size);
    }
}
