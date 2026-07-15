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
