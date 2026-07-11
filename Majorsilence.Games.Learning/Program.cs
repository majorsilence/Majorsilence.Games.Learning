using System.Runtime.InteropServices;
using SDL3;
using Majorsilence.Games.Learning;
using Majorsilence.Games.Core;
using Majorsilence.Games.Core.Audio;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Isometric;
using Majorsilence.Games.Core.Levels;
using Majorsilence.Games.Core.Surfaces;
using Majorsilence.Games.Core.Textures;

using var window = new Window("SDL3 Isometric Demo", 640, 480, highPixelDensity: true);
using var renderer = new Renderer(window);

using var audioDevice = new AudioDevice();
using var gameStartSound = new Sound(audioDevice, "assets/audio/game-start.mp3");
gameStartSound.Play();

using var titleTexture = Texture.CreateTextTexture(renderer,
    "assets/fonts/Gidole-Regular.ttf",
    size: 25,
    new SDL.Color { A = 0, B = 155, G = 155, R = 155 },
    "Isometric Demo - arrow keys to move, F fullscreen, Esc/Q to quit"
);
var title = new StationaryObject(titleTexture);

var level = LevelLoader.Load("assets/levels/demo.json");

using var tilesetTexture = Texture.CreateImageTexture(renderer, "assets/artwork/isometric-demo/tileset.png");
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
var isoGrid = new IsometricGrid(level.TileWidth, level.TileHeight);

var isoMap = new IsometricTilemap(tiles, tileset, isoGrid)
{
    X = 0,
    Y = 0,
    ZIndex = 0
};

// World-space objects (anything positioned relative to the isometric grid, as
// opposed to screen-space UI like `title`) that need to be shifted in lockstep
// whenever the grid's origin is recentered, so they stay anchored to their tile
// instead of sliding out from under it when the window is resized.
var worldObjects = new List<GameObject>();

// Recenter the isometric grid on the current logical viewport so the map
// adapts to the window's aspect ratio (more world visible on a tall/mobile-shaped
// window, more on a wide/desktop one) instead of sitting at a fixed offset.
void SyncViewport()
{
    var previousOriginX = isoGrid.OriginX;
    var previousOriginY = isoGrid.OriginY;

    renderer.SyncLogicalPresentationToWindow();
    var (w, h) = renderer.Size;
    isoGrid.OriginX = w / 2;
    isoGrid.OriginY = h / 4;

    var dx = isoGrid.OriginX - previousOriginX;
    var dy = isoGrid.OriginY - previousOriginY;
    foreach (var obj in worldObjects)
    {
        obj.X += dx;
        obj.Y += dy;
    }
}
SyncViewport(); // establishes the initial origin before placing tile-anchored objects below

// Places a GameObject's top-left so it stands upright with its base planted on
// the given tile's front (bottom) vertex, and gives it a matching SortOffsetY so
// it depth-sorts correctly against the tilemap and other sprites of any height.
(int X, int Y) StandOnTile(int column, int row, int width, int height)
{
    var (tileX, tileY) = isoGrid.TileToScreen(column, row);
    return (tileX + (isoGrid.TileWidth - width) / 2, tileY + isoGrid.TileHeight - height);
}

var playerStart = level.Entities.FirstOrDefault(e => e.Type == "playerStart")
    ?? throw new MajorsilenceException("Level has no 'playerStart' entity.");

using var playerTexture = Texture.CreateImageTexture(renderer, "assets/artwork/isometric-demo/character.png");
var playerSheet = new SpriteSheet(playerTexture, frameWidth: 16, frameHeight: 32);
var (playerStartX, playerStartY) = StandOnTile(playerStart.Column, playerStart.Row, width: 16, height: 32);
var player = new Player(playerSheet)
{
    Speed = 120f,
    X = playerStartX,
    Y = playerStartY,
    ZIndex = 1,
    SortOffsetY = 32
};
player.SetAnimation(new Animation(frames: new[] { 0, 1, 2, 3 }, frameDurationMs: 150));

using var treeTexture = Texture.CreateImageTexture(renderer, "assets/artwork/isometric-demo/tree.png");
var treeSheet = new SpriteSheet(treeTexture, frameWidth: 32, frameHeight: 48);

Sprite MakeTree(int column, int row)
{
    var (x, y) = StandOnTile(column, row, width: 32, height: 48);
    return new Sprite(treeSheet) { X = x, Y = y, ZIndex = 1, SortOffsetY = 48 };
}

var gameObjects = new List<GameObject> { title, isoMap, player };
worldObjects.Add(player);

foreach (var entity in level.Entities.Where(e => e.Type == "tree"))
{
    var tree = MakeTree(entity.Column, entity.Row);
    gameObjects.Add(tree);
    worldObjects.Add(tree);
}

InputManager.WindowResized += SyncViewport;

renderer.DrawColor(30, 30, 35, 255);

var loop = new EventLoop(renderer);

loop.Start(gameObjects);
