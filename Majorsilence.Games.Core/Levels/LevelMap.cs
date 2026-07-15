namespace Majorsilence.Games.Core.Levels;

/// <summary>
/// A parsed, validated level: a tile grid described as ASCII-art rows keyed
/// through a legend of semantic tile-type names, plus a list of entity
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

    /// <summary>"isometric" or "sidescroll" - which Camera/Tilemap setup a loader should build.</summary>
    public string Perspective { get; set; } = "isometric";

    /// <summary>
    /// For "sidescroll" levels: "horizontal" (bidirectional left/right follow),
    /// "forwardOnly" (one-way horizontal, classic Mario-style camera lock), or
    /// "vertical" (bidirectional up/down follow). Ignored for isometric levels.
    /// </summary>
    public string ScrollMode { get; set; } = "horizontal";

    /// <summary>World pixels per elevation step; 0 means the Heights feature is unused.</summary>
    public int ElevationStep { get; set; }

    /// <summary>
    /// Optional parallel ASCII grid (digits '0'-'9'), same dimensions as Tiles, giving
    /// each tile's elevation in steps (multiplied by ElevationStep for world pixels).
    /// Null means flat (every tile at elevation 0) - existing levels without this
    /// field keep working unchanged.
    /// </summary>
    public string[]? Heights { get; set; }
}
