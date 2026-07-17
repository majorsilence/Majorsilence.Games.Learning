using Majorsilence.Games.Core.Levels;

namespace Majorsilence.Games.Core.Tests;

public class LevelLoaderTest
{
    private const string ValidJson = """
    {
      "tileWidth": 32,
      "tileHeight": 16,
      "legend": { "G": "grass", "W": "water" },
      "tiles": ["GW", "WG"],
      "entities": [ { "type": "playerStart", "column": 1, "row": 0 } ]
    }
    """;

    private const string RaggedRowJson = """
    {
      "tileWidth": 32,
      "tileHeight": 16,
      "legend": { "G": "grass" },
      "tiles": ["GG", "G"],
      "entities": []
    }
    """;

    private const string UndefinedLegendCharJson = """
    {
      "tileWidth": 32,
      "tileHeight": 16,
      "legend": { "G": "grass" },
      "tiles": ["GX"],
      "entities": []
    }
    """;

    private const string ElevatedJson = """
    {
      "tileWidth": 32,
      "tileHeight": 16,
      "legend": { "G": "grass" },
      "tiles": ["GG", "GG"],
      "elevationStep": 16,
      "heights": ["01", "10"],
      "entities": []
    }
    """;

    private const string BadHeightsCharJson = """
    {
      "tileWidth": 32,
      "tileHeight": 16,
      "legend": { "G": "grass" },
      "tiles": ["GG"],
      "heights": ["0X"],
      "entities": []
    }
    """;

    private const string SideScrollJson = """
    {
      "tileWidth": 32,
      "tileHeight": 32,
      "perspective": "sidescroll",
      "scrollMode": "forwardOnly",
      "legend": { "G": "ground" },
      "tiles": ["GG"],
      "entities": []
    }
    """;

    private const string BadPerspectiveJson = """
    {
      "tileWidth": 32,
      "tileHeight": 16,
      "perspective": "topdown",
      "legend": { "G": "grass" },
      "tiles": ["GG"],
      "entities": []
    }
    """;

    private const string CustomTilesetJson = """
    {
      "tileWidth": 32,
      "tileHeight": 16,
      "tilesetPath": "assets/artwork/titanic-demo/tileset.png",
      "tileFrames": { "deck": 0, "water": 1 },
      "legend": { "D": "deck", "W": "water" },
      "tiles": ["DW"],
      "entities": []
    }
    """;

    private const string RoomFeaturesJson = """
    {
      "tileWidth": 32,
      "tileHeight": 16,
      "legend": { "D": "deck", "W": "water", "X": "wall" },
      "tiles": ["DWX"],
      "solid": ["wall"],
      "hazards": { "water": "freeze" },
      "floodDelaySeconds": 45.5,
      "coop": true,
      "entities": [
        { "type": "door", "column": 0, "row": 0, "properties": { "target": "assets/levels/titanic-rooms/bridge.json", "spawn": "fromBoatDeck" } }
      ]
    }
    """;

    private const string OceanWorldJson = """
    {
      "tileWidth": 32,
      "tileHeight": 16,
      "legend": { "D": "deck", "W": "water" },
      "tiles": ["DW"],
      "worldMinColumn": -1000,
      "worldMaxColumn": 1000,
      "worldMinRow": -1000,
      "worldMaxRow": 1000,
      "fallbackTileType": "water",
      "driftSpeedX": 6,
      "driftSpeedY": -3,
      "tileVariants": { "water": [1, 3, 4] },
      "entities": []
    }
    """;

    public void Test1()
    {
        var level = LevelLoader.Parse(ValidJson, "valid-fixture");

        System.Diagnostics.Debug.Assert(level.TileWidth == 32);
        System.Diagnostics.Debug.Assert(level.TileHeight == 16);
        System.Diagnostics.Debug.Assert(level.Tiles.Length == 2);
        System.Diagnostics.Debug.Assert(level.Legend['G'] == "grass");
        System.Diagnostics.Debug.Assert(level.Legend['W'] == "water");
        System.Diagnostics.Debug.Assert(level.Entities.Count == 1);
        System.Diagnostics.Debug.Assert(level.Entities[0].Type == "playerStart");
        System.Diagnostics.Debug.Assert(level.Entities[0].Column == 1);
        System.Diagnostics.Debug.Assert(level.Entities[0].Row == 0);

        var frameIndex = new Dictionary<string, int> { ["grass"] = 0, ["water"] = 1 };
        var tiles = LevelLoader.ResolveTileIndices(level, frameIndex);

        System.Diagnostics.Debug.Assert(tiles[0, 0] == 0); // G
        System.Diagnostics.Debug.Assert(tiles[0, 1] == 1); // W
        System.Diagnostics.Debug.Assert(tiles[1, 0] == 1); // W
        System.Diagnostics.Debug.Assert(tiles[1, 1] == 0); // G

        AssertThrows(() => LevelLoader.Parse(RaggedRowJson, "ragged-fixture"));
        AssertThrows(() => LevelLoader.Parse(UndefinedLegendCharJson, "undefined-char-fixture"));
        AssertThrows(() => LevelLoader.Parse("not json", "invalid-json-fixture"));

        // backward compatibility: a level with no perspective/scrollMode/heights fields
        // (like ValidJson above) defaults to isometric/horizontal/flat
        System.Diagnostics.Debug.Assert(level.Perspective == "isometric");
        System.Diagnostics.Debug.Assert(level.ScrollMode == "horizontal");
        System.Diagnostics.Debug.Assert(level.Heights is null);
        var flatElevations = LevelLoader.ResolveElevations(level);
        System.Diagnostics.Debug.Assert(flatElevations[0, 0] == 0 && flatElevations[1, 1] == 0);

        var elevated = LevelLoader.Parse(ElevatedJson, "elevated-fixture");
        var elevations = LevelLoader.ResolveElevations(elevated);
        System.Diagnostics.Debug.Assert(elevations[0, 0] == 0);  // '0' * step 16
        System.Diagnostics.Debug.Assert(elevations[0, 1] == 16); // '1' * step 16
        System.Diagnostics.Debug.Assert(elevations[1, 0] == 16);
        System.Diagnostics.Debug.Assert(elevations[1, 1] == 0);

        var sideScroll = LevelLoader.Parse(SideScrollJson, "sidescroll-fixture");
        System.Diagnostics.Debug.Assert(sideScroll.Perspective == "sidescroll");
        System.Diagnostics.Debug.Assert(sideScroll.ScrollMode == "forwardOnly");

        AssertThrows(() => LevelLoader.Parse(BadHeightsCharJson, "bad-heights-char-fixture"));
        AssertThrows(() => LevelLoader.Parse(BadPerspectiveJson, "bad-perspective-fixture"));

        // backward compatibility: no tilesetPath/tileFrames means "caller decides"
        System.Diagnostics.Debug.Assert(level.TilesetPath == "");
        System.Diagnostics.Debug.Assert(level.TileFrames.Count == 0);

        var customTileset = LevelLoader.Parse(CustomTilesetJson, "custom-tileset-fixture");
        System.Diagnostics.Debug.Assert(customTileset.TilesetPath == "assets/artwork/titanic-demo/tileset.png");
        System.Diagnostics.Debug.Assert(customTileset.TileFrames["deck"] == 0);
        System.Diagnostics.Debug.Assert(customTileset.TileFrames["water"] == 1);
        var customTiles = LevelLoader.ResolveTileIndices(customTileset, customTileset.TileFrames);
        System.Diagnostics.Debug.Assert(customTiles[0, 0] == 0); // D
        System.Diagnostics.Debug.Assert(customTiles[0, 1] == 1); // W

        // backward compatibility: no solid/hazards/floodDelaySeconds/coop means
        // "no collision, no hazards, never floods, single player"
        System.Diagnostics.Debug.Assert(level.Solid.Count == 0);
        System.Diagnostics.Debug.Assert(level.Hazards.Count == 0);
        System.Diagnostics.Debug.Assert(level.FloodDelaySeconds < 0);
        System.Diagnostics.Debug.Assert(level.Coop == false);
        System.Diagnostics.Debug.Assert(level.Entities[0].Properties.Count == 0);

        var roomFeatures = LevelLoader.Parse(RoomFeaturesJson, "room-features-fixture");
        System.Diagnostics.Debug.Assert(roomFeatures.Solid.Contains("wall"));
        System.Diagnostics.Debug.Assert(roomFeatures.Hazards["water"] == "freeze");
        System.Diagnostics.Debug.Assert(Math.Abs(roomFeatures.FloodDelaySeconds - 45.5f) < 0.001f);
        System.Diagnostics.Debug.Assert(roomFeatures.Coop);
        var door = roomFeatures.Entities[0];
        System.Diagnostics.Debug.Assert(door.Type == "door");
        System.Diagnostics.Debug.Assert(door.Properties["target"] == "assets/levels/titanic-rooms/bridge.json");
        System.Diagnostics.Debug.Assert(door.Properties["spawn"] == "fromBoatDeck");

        // backward compatibility: no world bounds/fallback/drift means "the level's
        // world is exactly its Tiles array, stationary" - RoomFeaturesJson never sets them
        System.Diagnostics.Debug.Assert(roomFeatures.WorldMinColumn == 0 && roomFeatures.WorldMaxColumn == 0);
        System.Diagnostics.Debug.Assert(roomFeatures.FallbackTileType == "");
        System.Diagnostics.Debug.Assert(roomFeatures.DriftSpeedX == 0f && roomFeatures.DriftSpeedY == 0f);

        var ocean = LevelLoader.Parse(OceanWorldJson, "ocean-world-fixture");
        System.Diagnostics.Debug.Assert(ocean.WorldMinColumn == -1000 && ocean.WorldMaxColumn == 1000);
        System.Diagnostics.Debug.Assert(ocean.WorldMinRow == -1000 && ocean.WorldMaxRow == 1000);
        System.Diagnostics.Debug.Assert(ocean.FallbackTileType == "water");
        System.Diagnostics.Debug.Assert(Math.Abs(ocean.DriftSpeedX - 6f) < 0.001f);
        System.Diagnostics.Debug.Assert(Math.Abs(ocean.DriftSpeedY - (-3f)) < 0.001f);
        System.Diagnostics.Debug.Assert(ocean.TileVariants["water"].SequenceEqual(new[] { 1, 3, 4 }));
        System.Diagnostics.Debug.Assert(roomFeatures.TileVariants.Count == 0); // backward compatible default
    }

    private static void AssertThrows(Action action)
    {
        try
        {
            action();
            System.Diagnostics.Debug.Assert(false, "expected a MajorsilenceException to be thrown");
        }
        catch (MajorsilenceException)
        {
            // expected
        }
    }
}
