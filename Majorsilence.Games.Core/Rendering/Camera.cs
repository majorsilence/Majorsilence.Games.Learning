using Majorsilence.Games.Core.GameObjects;

namespace Majorsilence.Games.Core.Rendering;

/// <summary>
/// Which axis the camera follows its Target on. Horizontal/Vertical lock the
/// other axis in place (e.g. a classic side-scroller keeps Y fixed); Free follows
/// both (the isometric default).
/// </summary>
public enum ScrollAxis
{
    Free,
    Horizontal,
    Vertical
}

/// <summary>
/// Owns the world-space-to-screen-space transform. Projection-agnostic: it doesn't
/// know or care whether "world space" was produced by an isometric projection
/// (IsometricGrid.TileToWorld) or is flat 2D side-scroller space - GameObject.X/Y
/// are always world position, and Camera.WorldToScreen resolves the final draw
/// position at render time.
/// </summary>
public class Camera
{
    public float X { get; set; }
    public float Y { get; set; }
    public int ViewportWidth { get; set; }
    public int ViewportHeight { get; set; }

    /// <summary>Optional GameObject the camera follows every Update().</summary>
    public GameObject? Target { get; set; }

    public ScrollAxis Axis { get; set; } = ScrollAxis.Free;

    /// <summary>
    /// When true, the camera's position on its followed axis/axes never recedes
    /// (only advances toward the target) - e.g. a classic one-way side-scroller
    /// where the camera won't scroll back left once it has advanced right.
    /// </summary>
    public bool OneWay { get; set; }

    /// <summary>
    /// When OneWay is set, this object is fenced to never move behind the camera's
    /// trailing edge on the followed axis/axes (e.g. the player can't walk back off
    /// the left/top of the screen in a classic one-way side-scroller). Typically the
    /// same object as Target, kept as a separate property since a future game might
    /// drive the camera off a different object than the one it fences.
    /// </summary>
    public GameObject? LeadingEdgeGate { get; set; }

    /// <summary>Optional world-space level bounds the camera won't scroll past. Null = unbounded.</summary>
    public float? MinX { get; set; }
    public float? MaxX { get; set; }
    public float? MinY { get; set; }
    public float? MaxY { get; set; }

    /// <summary>
    /// Transient screen-space offset applied in WorldToScreen (e.g. impact shake).
    /// Deliberately not part of X/Y so it never interacts with follow/clamp/ratchet
    /// logic - game code sets it each frame and zero means no effect.
    /// </summary>
    public float ShakeX { get; set; }
    public float ShakeY { get; set; }

    private float _maxReachedX = float.NegativeInfinity;
    private float _maxReachedY = float.NegativeInfinity;

    public void Update()
    {
        if (Target is not null)
        {
            if (Axis != ScrollAxis.Vertical) X = Target.X;
            if (Axis != ScrollAxis.Horizontal) Y = Target.Y;
        }

        if (OneWay)
        {
            if (Axis != ScrollAxis.Vertical)
            {
                _maxReachedX = MathF.Max(_maxReachedX, X);
                X = _maxReachedX;
            }
            if (Axis != ScrollAxis.Horizontal)
            {
                _maxReachedY = MathF.Max(_maxReachedY, Y);
                Y = _maxReachedY;
            }
        }

        X = ClampAxis(X, MinX, MaxX, ViewportWidth / 2f);
        Y = ClampAxis(Y, MinY, MaxY, ViewportHeight / 2f);

        if (OneWay && LeadingEdgeGate is not null)
        {
            var gateX = X - ViewportWidth / 2f;
            var gateY = Y - ViewportHeight / 2f;

            if (LeadingEdgeGate is DynamicObject dynamic)
            {
                var x = Axis != ScrollAxis.Vertical ? MathF.Max(dynamic.X, gateX) : dynamic.X;
                var y = Axis != ScrollAxis.Horizontal ? MathF.Max(dynamic.Y, gateY) : dynamic.Y;
                dynamic.SnapTo((int)MathF.Round(x), (int)MathF.Round(y));
            }
            else
            {
                if (Axis != ScrollAxis.Vertical) LeadingEdgeGate.X = (int)MathF.Round(MathF.Max(LeadingEdgeGate.X, gateX));
                if (Axis != ScrollAxis.Horizontal) LeadingEdgeGate.Y = (int)MathF.Round(MathF.Max(LeadingEdgeGate.Y, gateY));
            }
        }
    }

    private static float ClampAxis(float value, float? min, float? max, float halfViewport)
    {
        if (min is null && max is null) return value;
        var lo = (min ?? float.NegativeInfinity) + halfViewport;
        var hi = (max ?? float.PositiveInfinity) - halfViewport;
        // if the level is narrower than the viewport on this axis, center it
        // instead of clamping to an inverted (lo > hi) range
        if (lo > hi) return ((min ?? value) + (max ?? value)) / 2f;
        return Math.Clamp(value, lo, hi);
    }

    public (int ScreenX, int ScreenY) WorldToScreen(float worldX, float worldY)
    {
        return (
            (int)MathF.Round(worldX - X + ShakeX + ViewportWidth / 2f),
            (int)MathF.Round(worldY - Y + ShakeY + ViewportHeight / 2f)
        );
    }
}
