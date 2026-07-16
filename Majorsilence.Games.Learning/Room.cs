using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Isometric;
using Majorsilence.Games.Core.Levels;

namespace Majorsilence.Games.Learning;

/// <summary>
/// One loaded, live room of the ship: its tilemap, props, NPCs, tix pickups,
/// doors and spawn points, plus a mutable copy of the tile-type grid (so
/// flooding can rewrite it at runtime without touching the immutable LevelMap
/// the room was built from). Built once per room load by Game; torn down and
/// replaced (not mutated in place) when the player walks through a door.
/// </summary>
public class Room
{
    public string Path { get; }
    public LevelMap Level { get; }
    public IsometricGrid Grid { get; }
    public IsometricTilemap Tilemap { get; }

    /// <summary>Every GameObject this room owns (tilemap, props, NPCs, tix) - not the player(s)/HUD, which persist across rooms.</summary>
    public List<GameObject> RoomObjects { get; } = new();

    public List<Door> Doors { get; } = new();
    public Dictionary<string, (int Column, int Row)> SpawnPoints { get; } = new();
    public List<Npc> Npcs { get; } = new();
    public List<TixPickup> TixPickups { get; } = new();
    public (int Column, int Row)? ShopTile { get; private set; }

    public bool HasFlooded { get; private set; }

    private readonly string[,] _tileTypes;
    private readonly Dictionary<string, int> _tileFrameIndex;
    private readonly bool _hasVirtualWorld;

    /// <summary>
    /// initialDriftX/Y seed the tilemap's starting world offset - Game persists the
    /// ship's total sailed distance across room reloads (a fresh Room instance is
    /// built every time a door is used, including re-entering the boat deck), and
    /// passes the current total back in here so props/doors/spawn points (placed via
    /// StandOnTile, which reads Tilemap.X/Y) land in the right spot immediately
    /// rather than snapping in at the origin and jumping on the next drift tick.
    /// </summary>
    public Room(string path, Game game, int initialDriftX = 0, int initialDriftY = 0)
    {
        Path = path;
        Level = LevelLoader.Load(path);

        var tilesetPath = string.IsNullOrEmpty(Level.TilesetPath) ? game.DefaultTilesetPath : Level.TilesetPath;
        var tileset = game.GetSheet(tilesetPath, 32, 16);
        _tileFrameIndex = Level.TileFrames.Count > 0 ? Level.TileFrames : game.DefaultTileFrameIndex;

        _hasVirtualWorld = Level.WorldMaxColumn > Level.WorldMinColumn && Level.WorldMaxRow > Level.WorldMinRow;
        var fallbackFrameIndex = !string.IsNullOrEmpty(Level.FallbackTileType) && _tileFrameIndex.TryGetValue(Level.FallbackTileType, out var fbFrame)
            ? fbFrame
            : -1;

        var tiles = LevelLoader.ResolveTileIndices(Level, _tileFrameIndex);
        var elevations = LevelLoader.ResolveElevations(Level);
        Grid = new IsometricGrid(Level.TileWidth, Level.TileHeight);
        Tilemap = new IsometricTilemap(tiles, tileset, Grid, elevations,
            Level.WorldMinColumn, Level.WorldMaxColumn, Level.WorldMinRow, Level.WorldMaxRow, fallbackFrameIndex)
        { X = initialDriftX, Y = initialDriftY, ZIndex = 0 };
        RoomObjects.Add(Tilemap);

        var rows = Level.Tiles.Length;
        var columns = rows == 0 ? 0 : Level.Tiles[0].Length;
        _tileTypes = new string[rows, columns];
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
            _tileTypes[row, column] = Level.Legend[Level.Tiles[row][column]];

        BuildEntities(game);

        var effectiveDelay = game.EffectiveFloodDelaySeconds(path, Level.FloodDelaySeconds);
        if (effectiveDelay >= 0 && game.SecondsSinceCollision() >= effectiveDelay)
            ApplyFlood();
    }

    private void BuildEntities(Game game)
    {
        foreach (var entity in Level.Entities)
        {
            switch (entity.Type)
            {
                case "door":
                    Doors.Add(new Door
                    {
                        Column = entity.Column,
                        Row = entity.Row,
                        Target = entity.Properties.GetValueOrDefault("target", ""),
                        Spawn = entity.Properties.GetValueOrDefault("spawn", "")
                    });
                    break;

                case "spawnPoint":
                    var spawnName = entity.Properties.GetValueOrDefault("name", "");
                    if (spawnName != "") SpawnPoints[spawnName] = (entity.Column, entity.Row);
                    break;

                case "npc":
                    var role = entity.Properties.GetValueOrDefault("role", "crew");
                    if (game.NpcKinds.TryGetValue(role, out var npcKind))
                    {
                        var npcSheet = game.GetSheet(npcKind.ImagePath, npcKind.Width, npcKind.Height);
                        var (npcX, npcY) = StandOnTile(entity.Column, entity.Row, npcKind.Width, npcKind.Height);
                        var npc = new Npc(npcSheet, role) { X = npcX, Y = npcY, ZIndex = 1, SortOffsetY = npcKind.Height };
                        Npcs.Add(npc);
                        RoomObjects.Add(npc);
                    }
                    break;

                case "tix":
                    var value = int.TryParse(entity.Properties.GetValueOrDefault("value", ""), out var parsedValue) ? parsedValue : 10;
                    var tixSheet = game.GetSheet(game.TixIconPath, 16, 16);
                    var (tixX, tixY) = StandOnTile(entity.Column, entity.Row, 16, 16);
                    var tix = new TixPickup(tixSheet) { X = tixX, Y = tixY, ZIndex = 1, SortOffsetY = 16, Value = value };
                    TixPickups.Add(tix);
                    RoomObjects.Add(tix);
                    break;

                case "shop":
                    ShopTile = (entity.Column, entity.Row);
                    break;

                case "playerStart":
                    // Only meaningful for the very first room of a session - Game reads it directly.
                    break;

                default:
                    if (game.PropKinds.TryGetValue(entity.Type, out var propKind))
                    {
                        var sheet = game.GetSheet(propKind.ImagePath, propKind.Width, propKind.Height);
                        var (propX, propY) = StandOnTile(entity.Column, entity.Row, propKind.Width, propKind.Height);
                        var sprite = new Sprite(sheet) { X = propX, Y = propY, ZIndex = 1, SortOffsetY = propKind.Height };
                        RoomObjects.Add(sprite);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Places a GameObject's top-left so it stands upright with its base planted on
    /// the given tile's front (bottom) vertex, matching the anchor convention used
    /// throughout the isometric demos. Includes the tilemap's current drift offset,
    /// so a freshly-placed object (room entry, respawn) lands in the right spot
    /// even if the ship has already sailed some distance this session.
    /// </summary>
    public (int X, int Y) StandOnTile(int column, int row, int width, int height)
    {
        var (tileX, tileY) = Grid.TileToWorld(column, row);
        return (Tilemap.X + tileX + (Grid.TileWidth - width) / 2, Tilemap.Y + tileY + Grid.TileHeight - height);
    }

    public int GetElevationPixels(int column, int row) => Tilemap.GetElevationPixels(column, row);

    /// <summary>Out-of-range tiles are treated as solid, since no room's ship layout should be walked off the edge of its own grid (unless a virtual world extends it - see ResolveTileType).</summary>
    public bool IsSolid(int column, int row)
    {
        var type = ResolveTileType(column, row);
        return type is null || Level.Solid.Contains(type);
    }

    public bool TryGetHazard(int column, int row, out string hazard)
    {
        hazard = "";
        var type = ResolveTileType(column, row);
        return type is not null && Level.Hazards.TryGetValue(type, out hazard!);
    }

    /// <summary>
    /// Semantic tile-type name at (column, row): from the explicit grid if in
    /// range, else the level's FallbackTileType if a virtual world is configured
    /// and the coordinate falls within its bounds, else null (genuinely outside
    /// the room's world - IsSolid treats this as a hard boundary).
    /// </summary>
    private string? ResolveTileType(int column, int row)
    {
        if (InBounds(column, row)) return _tileTypes[row, column];

        if (_hasVirtualWorld && column >= Level.WorldMinColumn && column < Level.WorldMaxColumn &&
            row >= Level.WorldMinRow && row < Level.WorldMaxRow)
        {
            return string.IsNullOrEmpty(Level.FallbackTileType) ? "" : Level.FallbackTileType;
        }

        return null;
    }

    private bool InBounds(int column, int row)
    {
        var rows = _tileTypes.GetLength(0);
        var columns = rows == 0 ? 0 : _tileTypes.GetLength(1);
        return row >= 0 && row < rows && column >= 0 && column < columns;
    }

    /// <summary>
    /// Shifts the tilemap and every other room object (props/NPCs/tix) by the same
    /// amount - used by Game to advance ship drift each frame, so everything stays
    /// visually locked together while moving through the world.
    /// </summary>
    public void ShiftBy(int deltaX, int deltaY)
    {
        if (deltaX == 0 && deltaY == 0) return;
        foreach (var obj in RoomObjects)
        {
            obj.X += deltaX;
            obj.Y += deltaY;
        }
    }

    /// <summary>
    /// Converts every non-solid, not-already-hazardous floor tile to "floodwater"
    /// (visually and for hazard checks). A room with no "floodwater" entry in its
    /// Hazards map simply has nothing to flood into (e.g. an already-open-air room).
    /// </summary>
    public void ApplyFlood()
    {
        if (HasFlooded) return;
        HasFlooded = true;

        if (!Level.Hazards.ContainsKey("floodwater")) return;
        if (!_tileFrameIndex.TryGetValue("floodwater", out var floodFrame)) return;

        var rows = _tileTypes.GetLength(0);
        var columns = rows == 0 ? 0 : _tileTypes.GetLength(1);
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var type = _tileTypes[row, column];
                if (Level.Solid.Contains(type)) continue;
                if (Level.Hazards.ContainsKey(type)) continue;

                _tileTypes[row, column] = "floodwater";
                Tilemap.SetTile(column, row, floodFrame);
            }
        }
    }
}
