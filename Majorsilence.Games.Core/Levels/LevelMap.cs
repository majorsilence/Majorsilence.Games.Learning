namespace Majorsilence.Games.Core.Levels;

/// <summary>
/// A parsed, validated level: an isometric tile grid described as ASCII-art rows
/// keyed through a legend of semantic tile-type names, plus a list of entity
/// placements (props, spawn points, etc). Tile-type names are decoupled from any
/// specific tileset's frame ordering - see LevelLoader.ResolveTileIndices.
/// </summary>
public class LevelMap
{
    public int TileWidth { get; set; }
    public int TileHeight { get; set; }
    public Dictionary<char, string> Legend { get; set; } = new();
    public string[] Tiles { get; set; } = Array.Empty<string>();
    public List<LevelEntity> Entities { get; set; } = new();
}
