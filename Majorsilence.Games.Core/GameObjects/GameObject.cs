namespace Majorsilence.Games.Core.GameObjects;

public abstract class GameObject
{
    public int X { get; set; }
    public int Y { get; set; }
    /// <summary>
    /// Higher value is on top
    /// </summary>
    public int ZIndex { get; set; } = 1;
    
    public abstract void Update();
    public abstract void Render();
}