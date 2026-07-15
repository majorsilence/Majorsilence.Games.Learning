using Majorsilence.Games.Core.Rendering;

namespace Majorsilence.Games.Core.GameObjects;

public abstract class GameObject
{
    public int X { get; set; }
    public int Y { get; set; }
    /// <summary>
    /// Higher value is on top. Used as a tie-breaker when SortY values are equal.
    /// </summary>
    public int ZIndex { get; set; } = 1;

    /// <summary>
    /// Pixel offset added to Y to approximate this object's visual footprint/anchor
    /// for isometric depth-sorting (e.g. sprite height, so sorting compares "feet"
    /// position rather than the top-left corner). Defaults to 0.
    /// </summary>
    public float SortOffsetY { get; set; } = 0f;

    /// <summary>
    /// Screen-Y used for per-frame isometric depth-sorting (painter's algorithm).
    /// Deliberately ground-based (ignores Z) so an object that's jumping/elevated
    /// still sorts against tiles/objects at its footprint, not its airborne height.
    /// </summary>
    public virtual float SortY => Y + SortOffsetY;

    /// <summary>
    /// Height above this object's ground position, in world pixels (isometric
    /// verticality / jump height). Lifts the rendered sprite upward without
    /// affecting X/Y or SortY. Defaults to 0.
    /// </summary>
    public float Z { get; set; } = 0f;

    public abstract void Update(float deltaTime);

    /// <summary>
    /// X/Y are world-space position; Camera resolves the final screen draw
    /// position via Camera.WorldToScreen. Screen-space/UI objects (see
    /// StationaryObject) may choose to ignore the camera and draw at X/Y directly.
    /// </summary>
    public abstract void Render(Camera camera);
}