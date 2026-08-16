using Majorsilence.Games.Core.Levels;
using Xunit;

namespace Majorsilence.Games.Core.Tests;

/// <summary>
/// Parsing checks for the level JSON format. Several of these pin backward
/// compatibility: a level file that omits a field has to keep meaning what it
/// meant before that field existed, because the shipped level files predate
/// most of them.
/// </summary>
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

    private const string TopDownJson = """
    {
      "tileWidth": 16,
      "tileHeight": 16,
      "perspective": "topdown",
      "legend": { "G": "grass" },
      "tiles": ["GG"],
      "entities": []
    }
    """;

    private const string BadPerspectiveJson = """
    {
      "tileWidth": 32,
      "tileHeight": 16,
      "perspective": "birdseye",
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
      "musicPath": "assets/audio/rpg/ashholt.wav",
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
      "wallProps": { "railing": "hullSide" },
      "entities": []
    }
    """;

    [Fact]
    public void ParsesDimensionsLegendAndEntities()
    {
        var level = LevelLoader.Parse(ValidJson, "valid-fixture");

        Assert.Equal(32, level.TileWidth);
        Assert.Equal(16, level.TileHeight);
        Assert.Equal(2, level.Tiles.Length);
        Assert.Equal("grass", level.Legend['G']);
        Assert.Equal("water", level.Legend['W']);

        var entity = Assert.Single(level.Entities);
        Assert.Equal("playerStart", entity.Type);
        Assert.Equal(1, entity.Column);
        Assert.Equal(0, entity.Row);
    }

    [Fact]
    public void ResolvesTileIndicesThroughTheLegend()
    {
        var level = LevelLoader.Parse(ValidJson, "valid-fixture");
        var frameIndex = new Dictionary<string, int> { ["grass"] = 0, ["water"] = 1 };

        var tiles = LevelLoader.ResolveTileIndices(level, frameIndex);

        Assert.Equal(0, tiles[0, 0]); // G
        Assert.Equal(1, tiles[0, 1]); // W
        Assert.Equal(1, tiles[1, 0]); // W
        Assert.Equal(0, tiles[1, 1]); // G
    }

    [Theory]
    [InlineData(RaggedRowJson, "rows of differing length")]
    [InlineData(UndefinedLegendCharJson, "a tile char with no legend entry")]
    [InlineData("not json", "malformed JSON")]
    [InlineData(BadHeightsCharJson, "a non-digit in the heights grid")]
    [InlineData(BadPerspectiveJson, "an unknown perspective")]
    public void RejectsMalformedLevels(string json, string because)
    {
        var error = Assert.Throws<MajorsilenceException>(() => LevelLoader.Parse(json, "bad-fixture"));
        Assert.False(string.IsNullOrWhiteSpace(error.Message), $"rejecting {because} should explain why");
    }

    /// <summary>
    /// A level file that sets none of the optional fields has to behave like the
    /// format did before they existed: isometric, horizontally scrolling, flat,
    /// no collision, no hazards, never floods, single player, caller-supplied
    /// tileset, and a world no bigger than its own tile grid.
    /// </summary>
    [Fact]
    public void OmittedFieldsKeepTheirPreFeatureMeaning()
    {
        var level = LevelLoader.Parse(ValidJson, "valid-fixture");

        Assert.Equal("isometric", level.Perspective);
        Assert.Equal("horizontal", level.ScrollMode);
        Assert.Null(level.Heights);

        var flat = LevelLoader.ResolveElevations(level);
        Assert.Equal(0, flat[0, 0]);
        Assert.Equal(0, flat[1, 1]);

        Assert.Equal("", level.TilesetPath);
        Assert.Equal("", level.MusicPath);
        Assert.Empty(level.TileFrames);
        Assert.Empty(level.Solid);
        Assert.Empty(level.Hazards);
        Assert.True(level.FloodDelaySeconds < 0, "no floodDelaySeconds should mean 'never floods'");
        Assert.False(level.Coop);
        Assert.Empty(level.Entities[0].Properties);

        var room = LevelLoader.Parse(RoomFeaturesJson, "room-features-fixture");
        Assert.Equal(0, room.WorldMinColumn);
        Assert.Equal(0, room.WorldMaxColumn);
        Assert.Equal("", room.FallbackTileType);
        Assert.Equal(0f, room.DriftSpeedX);
        Assert.Equal(0f, room.DriftSpeedY);
        Assert.Empty(room.TileVariants);
        Assert.Empty(room.WallProps);
    }

    [Fact]
    public void ResolvesElevationsAgainstTheStep()
    {
        var level = LevelLoader.Parse(ElevatedJson, "elevated-fixture");
        var elevations = LevelLoader.ResolveElevations(level);

        Assert.Equal(0, elevations[0, 0]);  // '0' * step 16
        Assert.Equal(16, elevations[0, 1]); // '1' * step 16
        Assert.Equal(16, elevations[1, 0]);
        Assert.Equal(0, elevations[1, 1]);
    }

    [Theory]
    [InlineData(SideScrollJson, "sidescroll")]
    [InlineData(TopDownJson, "topdown")]
    public void AcceptsEverySupportedPerspective(string json, string expected)
    {
        var level = LevelLoader.Parse(json, "perspective-fixture");
        Assert.Equal(expected, level.Perspective);
    }

    [Fact]
    public void ParsesScrollMode()
    {
        var level = LevelLoader.Parse(SideScrollJson, "sidescroll-fixture");
        Assert.Equal("forwardOnly", level.ScrollMode);
    }

    [Fact]
    public void ParsesTilesetPathAndFrameNames()
    {
        var level = LevelLoader.Parse(CustomTilesetJson, "custom-tileset-fixture");

        Assert.Equal("assets/artwork/titanic-demo/tileset.png", level.TilesetPath);
        Assert.Equal("assets/audio/rpg/ashholt.wav", level.MusicPath);
        Assert.Equal(0, level.TileFrames["deck"]);
        Assert.Equal(1, level.TileFrames["water"]);

        var tiles = LevelLoader.ResolveTileIndices(level, level.TileFrames);
        Assert.Equal(0, tiles[0, 0]); // D
        Assert.Equal(1, tiles[0, 1]); // W
    }

    [Fact]
    public void ParsesRoomFeaturesAndDoorProperties()
    {
        var level = LevelLoader.Parse(RoomFeaturesJson, "room-features-fixture");

        Assert.Contains("wall", level.Solid);
        Assert.Equal("freeze", level.Hazards["water"]);
        Assert.Equal(45.5f, level.FloodDelaySeconds, 3);
        Assert.True(level.Coop);

        var door = Assert.Single(level.Entities);
        Assert.Equal("door", door.Type);
        Assert.Equal("assets/levels/titanic-rooms/bridge.json", door.Properties["target"]);
        Assert.Equal("fromBoatDeck", door.Properties["spawn"]);
    }

    [Fact]
    public void ParsesUnboundedDriftingWorlds()
    {
        var level = LevelLoader.Parse(OceanWorldJson, "ocean-world-fixture");

        Assert.Equal(-1000, level.WorldMinColumn);
        Assert.Equal(1000, level.WorldMaxColumn);
        Assert.Equal(-1000, level.WorldMinRow);
        Assert.Equal(1000, level.WorldMaxRow);
        Assert.Equal("water", level.FallbackTileType);
        Assert.Equal(6f, level.DriftSpeedX, 3);
        Assert.Equal(-3f, level.DriftSpeedY, 3);
        Assert.Equal(new[] { 1, 3, 4 }, level.TileVariants["water"]);
        Assert.Equal("hullSide", level.WallProps["railing"]);
    }
}
