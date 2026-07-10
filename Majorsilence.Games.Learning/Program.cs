using System.Runtime.InteropServices;
using SDL3;
using Majorsilence.Games.Learning;
using Majorsilence.Games.Core;
using Majorsilence.Games.Core.Audio;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Isometric;
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

// Tile types, in tileset.png frame order: grass, dirt path, water, stone, sand.
const int Grass = 0, Dirt = 1, Water = 2, Stone = 3, Sand = 4;
var tiles = new[,]
{
    { Grass, Grass, Dirt,  Grass, Grass, Sand },
    { Grass, Dirt,  Dirt,  Grass, Sand,  Sand },
    { Water, Water, Dirt,  Grass, Grass, Grass },
    { Water, Water, Grass, Grass, Stone, Stone },
    { Grass, Grass, Grass, Stone, Stone, Grass },
    { Grass, Grass, Grass, Grass, Grass, Grass },
};

using var tilesetTexture = Texture.CreateImageTexture(renderer, "assets/artwork/isometric-demo/tileset.png");
var tileset = new SpriteSheet(tilesetTexture, frameWidth: 32, frameHeight: 16);
var isoGrid = new IsometricGrid(tileWidth: 32, tileHeight: 16);

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

using var playerTexture = Texture.CreateImageTexture(renderer, "assets/artwork/isometric-demo/character.png");
var playerSheet = new SpriteSheet(playerTexture, frameWidth: 16, frameHeight: 32);
var (playerStartX, playerStartY) = StandOnTile(column: 3, row: 2, width: 16, height: 32);
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

var tree1 = MakeTree(column: 3, row: 1);
var tree2 = MakeTree(column: 1, row: 4);

worldObjects.Add(player);
worldObjects.Add(tree1);
worldObjects.Add(tree2);
InputManager.WindowResized += SyncViewport;

renderer.DrawColor(30, 30, 35, 255);

var loop = new EventLoop(renderer);

var gameObjects = new List<GameObject>()
{
    title,
    isoMap,
    player,
    tree1,
    tree2
};

loop.Start(gameObjects);
