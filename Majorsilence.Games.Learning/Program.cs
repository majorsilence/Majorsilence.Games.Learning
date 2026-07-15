using System.Runtime.InteropServices;
using SDL3;
using Majorsilence.Games.Learning;
using Majorsilence.Games.Core;
using Majorsilence.Games.Core.Audio;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Isometric;
using Majorsilence.Games.Core.Levels;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Surfaces;
using Majorsilence.Games.Core.Textures;
using Majorsilence.Games.Core.Tilemaps;

// Which level to run - pass a level path as the first CLI argument to skip the
// prompt (e.g. for scripted/automated runs), otherwise pick interactively.
string ChooseLevelPath()
{
    var files = Directory.GetFiles("assets/levels", "*.json").OrderBy(f => f).ToArray();
    if (files.Length == 0)
        throw new MajorsilenceException("No level files found in assets/levels.");
    if (files.Length == 1)
        return files[0];

    Console.WriteLine("Select a level to run:");
    for (var i = 0; i < files.Length; i++)
    {
        var name = Path.GetFileNameWithoutExtension(files[i]);
        try
        {
            var preview = LevelLoader.Load(files[i]);
            var description = preview.Perspective.Equals("sidescroll", StringComparison.OrdinalIgnoreCase)
                ? $"sidescroll, {preview.ScrollMode}"
                : "isometric";
            Console.WriteLine($"  {i + 1}. {name} ({description})");
        }
        catch (MajorsilenceException)
        {
            Console.WriteLine($"  {i + 1}. {name}");
        }
    }

    while (true)
    {
        Console.Write($"Enter a number (1-{files.Length}, default 1): ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
            return files[0];
        if (int.TryParse(input, out var choice) && choice >= 1 && choice <= files.Length)
            return files[choice - 1];
        Console.WriteLine("Invalid choice, try again.");
    }
}

var levelPath = args.Length > 0 ? args[0] : ChooseLevelPath();
var level = LevelLoader.Load(levelPath);
var isSideScroll = level.Perspective.Equals("sidescroll", StringComparison.OrdinalIgnoreCase);

using var window = new Window("SDL3 Isometric Demo", 640, 480, highPixelDensity: true);
using var renderer = new Renderer(window);

using var audioDevice = new AudioDevice();
using var gameStartSound = new Sound(audioDevice, "assets/audio/game-start.mp3");
gameStartSound.Play();

using var titleTexture = Texture.CreateTextTexture(renderer,
    "assets/fonts/Gidole-Regular.ttf",
    size: 25,
    new SDL.Color { A = 0, B = 155, G = 155, R = 155 },
    "Arrow keys to move, Space to jump, F fullscreen, Esc/Q to quit"
);
var title = new StationaryObject(titleTexture);

var camera = new Camera();

// Keep the camera's viewport matched to the window's current point-size, so the
// visible world adapts to the window's aspect ratio (more world on a tall/mobile-
// shaped window, more on a wide/desktop one) instead of a fixed design resolution.
void SyncViewport()
{
    renderer.SyncLogicalPresentationToWindow();
    var (w, h) = renderer.Size;
    camera.ViewportWidth = w;
    camera.ViewportHeight = h;
}
SyncViewport();
InputManager.WindowResized += SyncViewport;

using var playerTexture = Texture.CreateImageTexture(renderer, "assets/artwork/isometric-demo/character.png");
var playerSheet = new SpriteSheet(playerTexture, frameWidth: 16, frameHeight: 32);
var player = new Player(playerSheet) { Speed = 120f, ZIndex = 1 };
player.SetAnimation(new Animation(frames: new[] { 0, 1, 2, 3 }, frameDurationMs: 150));
camera.Target = player;

var playerStart = level.Entities.FirstOrDefault(e => e.Type == "playerStart")
    ?? throw new MajorsilenceException("Level has no 'playerStart' entity.");

var gameObjects = new List<GameObject> { title, player };
Action? beforeUpdate = null;

if (isSideScroll)
{
    var sideTilesetTexture = Texture.CreateImageTexture(renderer, "assets/artwork/isometric-demo/sidescroll-tileset.png");
    var sideTileset = new SpriteSheet(sideTilesetTexture, frameWidth: 32, frameHeight: 32);
    var sideFrameIndex = new Dictionary<string, int> { ["empty"] = -1, ["ground"] = 0, ["brick"] = 1 };
    var sideTiles = LevelLoader.ResolveTileIndices(level, sideFrameIndex);
    var flatMap = new FlatTilemap(sideTiles, sideTileset, level.TileWidth, level.TileHeight) { ZIndex = 0 };

    player.X = playerStart.Column * level.TileWidth;
    player.Y = playerStart.Row * level.TileHeight + level.TileHeight - 32; // feet on the tile's bottom edge
    // no isometric depth-sort needed in a flat side-scroller (tiles never visually overlap)
    player.SortOffsetY = 0;

    camera.Axis = level.ScrollMode.Equals("vertical", StringComparison.OrdinalIgnoreCase)
        ? ScrollAxis.Vertical
        : ScrollAxis.Horizontal;
    camera.OneWay = level.ScrollMode.Equals("forwardOnly", StringComparison.OrdinalIgnoreCase);
    camera.LeadingEdgeGate = player;
    camera.MinX = 0;
    camera.MaxX = flatMap.PixelWidth;
    camera.MinY = 0;
    camera.MaxY = flatMap.PixelHeight;

    gameObjects.Insert(0, flatMap);

    void SyncPlayerGroundZ()
    {
        var column = player.X / level.TileWidth;
        var row = (player.Y + 32) / level.TileHeight; // ground check just below the player's feet
        player.GroundZ = flatMap.GetElevationPixels(column, row);
    }
    beforeUpdate = SyncPlayerGroundZ;
}
else
{
    var tilesetTexture = Texture.CreateImageTexture(renderer, "assets/artwork/isometric-demo/tileset.png");
    var tileset = new SpriteSheet(tilesetTexture, frameWidth: 32, frameHeight: 16);

    // Maps this level's semantic tile-type names (from its legend) to tileset.png's
    // frame order, decoupling level data from any one tileset image's layout.
    var tileFrameIndex = new Dictionary<string, int>
    {
        ["grass"] = 0,
        ["dirt"] = 1,
        ["water"] = 2,
        ["stone"] = 3,
        ["sand"] = 4
    };
    var tiles = LevelLoader.ResolveTileIndices(level, tileFrameIndex);
    var elevations = LevelLoader.ResolveElevations(level);
    var isoGrid = new IsometricGrid(level.TileWidth, level.TileHeight);

    var isoMap = new IsometricTilemap(tiles, tileset, isoGrid, elevations)
    {
        X = 0,
        Y = 0,
        ZIndex = 0
    };
    camera.Axis = ScrollAxis.Free;

    // Places a GameObject's top-left so it stands upright with its base planted on
    // the given tile's front (bottom) vertex, and gives it a matching SortOffsetY so
    // it depth-sorts correctly against the tilemap and other sprites of any height.
    (int X, int Y) StandOnTile(int column, int row, int width, int height)
    {
        var (tileX, tileY) = isoGrid.TileToWorld(column, row);
        return (tileX + (isoGrid.TileWidth - width) / 2, tileY + isoGrid.TileHeight - height);
    }

    var (playerStartX, playerStartY) = StandOnTile(playerStart.Column, playerStart.Row, width: 16, height: 32);
    player.X = playerStartX;
    player.Y = playerStartY;
    player.SortOffsetY = 32;

    // Keeps the player's ground height in sync with whichever tile they're currently
    // over, so jumping/landing works across multi-level platforms. DynamicObject
    // deliberately doesn't know about IsometricTilemap itself (it could equally be
    // used in a flat side-scroller with GroundZ left at 0), so game code wires this.
    void SyncPlayerGroundZ()
    {
        var (column, row) = isoGrid.WorldToTile(player.X, player.Y);
        player.GroundZ = isoMap.GetElevationPixels(column, row);
    }
    beforeUpdate = SyncPlayerGroundZ;

    var treeTexture = Texture.CreateImageTexture(renderer, "assets/artwork/isometric-demo/tree.png");
    var treeSheet = new SpriteSheet(treeTexture, frameWidth: 32, frameHeight: 48);

    Sprite MakeTree(int column, int row)
    {
        var (x, y) = StandOnTile(column, row, width: 32, height: 48);
        return new Sprite(treeSheet) { X = x, Y = y, ZIndex = 1, SortOffsetY = 48 };
    }

    gameObjects.Add(isoMap);
    foreach (var entity in level.Entities.Where(e => e.Type == "tree"))
    {
        gameObjects.Add(MakeTree(entity.Column, entity.Row));
    }
}

renderer.DrawColor(30, 30, 35, 255);

var loop = new EventLoop(renderer);

loop.Start(gameObjects, camera, beforeUpdate);
