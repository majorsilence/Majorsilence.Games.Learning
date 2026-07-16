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

    /// <summary>
    /// Path to this level's tileset image. Empty means "use the caller's default"
    /// (e.g. Program.cs falls back to the original isometric-demo tileset) - lets
    /// existing levels that predate this field keep working unchanged, while new
    /// levels with their own art (e.g. a different theme) can be self-contained.
    /// </summary>
    public string TilesetPath { get; set; } = "";

    /// <summary>
    /// Maps this level's semantic tile-type names (from its legend) to TilesetPath's
    /// frame order. Empty means "use the caller's default" mapping, same fallback
    /// rule as TilesetPath.
    /// </summary>
    public Dictionary<string, int> TileFrames { get; set; } = new();

    /// <summary>Tile-type names (from the legend) that block movement. Empty means nothing is solid.</summary>
    public List<string> Solid { get; set; } = new();

    /// <summary>
    /// Tile-type names that are lethal to stand on, mapped to a hazard kind
    /// (e.g. "freeze", "drown") a game interprets. Empty means no hazards.
    /// </summary>
    public Dictionary<string, string> Hazards { get; set; } = new();

    /// <summary>
    /// Seconds after a scripted "collision" event before this room's floor floods
    /// (converted to a hazard at runtime). Negative means this room never floods.
    /// </summary>
    public float FloodDelaySeconds { get; set; } = -1f;

    /// <summary>
    /// Whether this level's game session supports local 2-player co-op. Only
    /// meaningful on the first level loaded in a session (the session-wide choice
    /// is made once, from that level).
    /// </summary>
    public bool Coop { get; set; }
}
