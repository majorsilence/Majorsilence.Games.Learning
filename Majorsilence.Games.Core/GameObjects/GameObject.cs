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
    /// </summary>
    public virtual float SortY => Y + SortOffsetY;

    public abstract void Update(float deltaTime);
    public abstract void Render();
}