using System.Text.Json;

namespace Majorsilence.Games.Core.Levels;

/// <summary>
/// Loads and validates LevelMap JSON files, and resolves a level's legend-keyed
/// tile grid into the int[,] frame-index grid IsometricTilemap consumes.
/// </summary>
public static class LevelLoader
{
    private static readonly string[] ValidPerspectives = { "isometric", "sidescroll" };
    private static readonly string[] ValidScrollModes = { "horizontal", "forwardOnly", "vertical" };

    // Mirrors the JSON shape. Legend keys are single-character strings here (JSON
    // object keys are always strings) and get validated/converted to char in Build.
    private class LevelMapJson
    {
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public Dictionary<string, string> Legend { get; set; } = new();
        public string[] Tiles { get; set; } = Array.Empty<string>();
        public List<LevelEntity> Entities { get; set; } = new();
        public string Perspective { get; set; } = "isometric";
        public string ScrollMode { get; set; } = "horizontal";
        public int ElevationStep { get; set; }
        public string[]? Heights { get; set; }
        public string TilesetPath { get; set; } = "";
        public Dictionary<string, int> TileFrames { get; set; } = new();
        public List<string> Solid { get; set; } = new();
        public Dictionary<string, string> Hazards { get; set; } = new();
        public float FloodDelaySeconds { get; set; } = -1f;
        public bool Coop { get; set; }
        public int WorldMinColumn { get; set; }
        public int WorldMaxColumn { get; set; }
        public int WorldMinRow { get; set; }
        public int WorldMaxRow { get; set; }
        public string FallbackTileType { get; set; } = "";
        public float DriftSpeedX { get; set; }
        public float DriftSpeedY { get; set; }
        public Dictionary<string, int[]> TileVariants { get; set; } = new();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static LevelMap Load(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new MajorsilenceException($"Could not read level file: {path}", ex);
        }

        return Parse(json, path);
    }

    /// <summary>
    /// Parses a level from a JSON string directly (e.g. for tests) rather than a file path.
    /// </summary>
    public static LevelMap Parse(string json, string sourceName = "<level>")
    {
        LevelMapJson? raw;
        try
        {
            raw = JsonSerializer.Deserialize<LevelMapJson>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new MajorsilenceException($"Level '{sourceName}' is not valid JSON.", ex);
        }

        if (raw is null)
            throw new MajorsilenceException($"Level '{sourceName}' is empty or invalid.");

        return Build(raw, sourceName);
    }

    private static LevelMap Build(LevelMapJson raw, string sourceName)
    {
        if (raw.TileWidth <= 0 || raw.TileHeight <= 0)
            throw new MajorsilenceException($"Level '{sourceName}': tileWidth/tileHeight must be greater than zero.");

        if (!ValidPerspectives.Contains(raw.Perspective, StringComparer.OrdinalIgnoreCase))
            throw new MajorsilenceException(
                $"Level '{sourceName}': perspective '{raw.Perspective}' must be one of: {string.Join(", ", ValidPerspectives)}.");

        if (!ValidScrollModes.Contains(raw.ScrollMode, StringComparer.OrdinalIgnoreCase))
            throw new MajorsilenceException(
                $"Level '{sourceName}': scrollMode '{raw.ScrollMode}' must be one of: {string.Join(", ", ValidScrollModes)}.");

        var legend = new Dictionary<char, string>();
        foreach (var (key, value) in raw.Legend)
        {
            if (key.Length != 1)
                throw new MajorsilenceException($"Level '{sourceName}': legend key '{key}' must be exactly one character.");
            legend[key[0]] = value;
        }

        if (raw.Tiles.Length == 0)
            throw new MajorsilenceException($"Level '{sourceName}': tiles grid is empty.");

        var width = raw.Tiles[0].Length;
        for (var row = 0; row < raw.Tiles.Length; row++)
        {
            if (raw.Tiles[row].Length != width)
                throw new MajorsilenceException(
                    $"Level '{sourceName}': row {row} has length {raw.Tiles[row].Length}, expected {width} (all rows must be the same length).");

            foreach (var c in raw.Tiles[row])
            {
                if (!legend.ContainsKey(c))
                    throw new MajorsilenceException($"Level '{sourceName}': tile character '{c}' at row {row} is not defined in the legend.");
            }
        }

        if (raw.Heights is not null)
        {
            if (raw.Heights.Length != raw.Tiles.Length)
                throw new MajorsilenceException(
                    $"Level '{sourceName}': heights has {raw.Heights.Length} rows, expected {raw.Tiles.Length} (must match tiles).");

            for (var row = 0; row < raw.Heights.Length; row++)
            {
                if (raw.Heights[row].Length != width)
                    throw new MajorsilenceException(
                        $"Level '{sourceName}': heights row {row} has length {raw.Heights[row].Length}, expected {width} (must match tiles).");

                foreach (var c in raw.Heights[row])
                {
                    if (c < '0' || c > '9')
                        throw new MajorsilenceException(
                            $"Level '{sourceName}': heights row {row} contains '{c}', expected a digit '0'-'9'.");
                }
            }
        }

        return new LevelMap
        {
            TileWidth = raw.TileWidth,
            TileHeight = raw.TileHeight,
            Legend = legend,
            Tiles = raw.Tiles,
            Entities = raw.Entities,
            Perspective = raw.Perspective,
            ScrollMode = raw.ScrollMode,
            ElevationStep = raw.ElevationStep,
            Heights = raw.Heights,
            TilesetPath = raw.TilesetPath,
            TileFrames = raw.TileFrames,
            Solid = raw.Solid,
            Hazards = raw.Hazards,
            FloodDelaySeconds = raw.FloodDelaySeconds,
            Coop = raw.Coop,
            WorldMinColumn = raw.WorldMinColumn,
            WorldMaxColumn = raw.WorldMaxColumn,
            WorldMinRow = raw.WorldMinRow,
            WorldMaxRow = raw.WorldMaxRow,
            FallbackTileType = raw.FallbackTileType,
            DriftSpeedX = raw.DriftSpeedX,
            DriftSpeedY = raw.DriftSpeedY,
            TileVariants = raw.TileVariants
        };
    }

    /// <summary>
    /// Converts a level's legend-keyed tile grid into the int[,] frame-index grid
    /// IsometricTilemap's constructor accepts, using the caller-supplied mapping from
    /// semantic tile-type name (as used in the legend) to a specific tileset's frame index.
    /// </summary>
    public static int[,] ResolveTileIndices(LevelMap level, IReadOnlyDictionary<string, int> tileTypeToFrameIndex)
    {
        var rows = level.Tiles.Length;
        var columns = rows == 0 ? 0 : level.Tiles[0].Length;
        var result = new int[rows, columns];

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var c = level.Tiles[row][column];
                var typeName = level.Legend[c];
                if (!tileTypeToFrameIndex.TryGetValue(typeName, out var frameIndex))
                    throw new MajorsilenceException($"No frame index mapping provided for tile type '{typeName}' (character '{c}').");
                result[row, column] = frameIndex;
            }
        }

        return result;
    }

    /// <summary>
    /// Converts a level's optional Heights ASCII grid into world-pixel elevations
    /// (digit * ElevationStep) for IsometricTilemap. Returns an all-zero grid if
    /// Heights was omitted, so flat levels need no special-casing by callers.
    /// </summary>
    public static int[,] ResolveElevations(LevelMap level)
    {
        var rows = level.Tiles.Length;
        var columns = rows == 0 ? 0 : level.Tiles[0].Length;
        var result = new int[rows, columns];

        if (level.Heights is null) return result;

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                result[row, column] = (level.Heights[row][column] - '0') * level.ElevationStep;
            }
        }

        return result;
    }
}
