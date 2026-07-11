namespace Majorsilence.Games.Core.Levels;

/// <summary>
/// A non-tile placement in a level (e.g. a prop or the player's starting position).
/// Type is a free-form string rather than an enum so new entity types don't require
/// a format version bump - the game consuming a level decides which types it handles.
/// </summary>
public class LevelEntity
{
    public string Type { get; set; } = "";
    public int Column { get; set; }
    public int Row { get; set; }
}
