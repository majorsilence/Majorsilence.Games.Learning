namespace Majorsilence.Games.Learning;

/// <summary>A tile position that, when a player stands on it, loads another room.</summary>
public class Door
{
    public int Column { get; init; }
    public int Row { get; init; }
    public string Target { get; init; } = "";
    public string Spawn { get; init; } = "";
}
