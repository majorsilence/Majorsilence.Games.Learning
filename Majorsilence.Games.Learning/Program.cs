using System.Runtime.InteropServices;
using SDL3;
using Majorsilence.Games.Learning;
using Majorsilence.Games.Core;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Isometric;
using Majorsilence.Games.Core.Surfaces;
using Majorsilence.Games.Core.Textures;

using var window = new Window("SDL3 Displaying Image", 640, 480);
using var renderer = new Renderer(window);

//var image = new Majorsilence.Games.Learning.Image("/Users/petergill/Downloads/stick_people.png");

//using var image = new ImageSurface("assets/artwork/z-like/character.png");
//image.ColorAsTransparent(255, 255, 255);

//using var texture = new Texture(renderer, image);
using var spriteTexture = Texture.CreateImageTexture(renderer,
    "assets/artwork/z-like/character.png",
    new SDL.Color { A = 255, B = 255, G = 255, R = 255 });

//using var font = new Fonts("assets/fonts/Gidole-Regular.ttf", 25);
//var color = new SDL.Color { A = 0, B = 155, G = 155, R = 150 };
//using var text = new TextSurface(font, color, "Hello World");
//using var textTexture = new Texture(renderer, text);
using var textTexture = Texture.CreateTextTexture(renderer,
    "assets/fonts/Gidole-Regular.ttf",
    size: 25,
    new SDL.Color { A = 0, B = 155, G = 155, R = 155 },
    "Hello World"
);

var stationary1 = new StationaryObject(textTexture);
var moving1 = new Player(spriteTexture)
{
    Speed = 2,
    X = 100,
    Y = 100,
    ZIndex = 1
};

var characterSheet = new SpriteSheet(spriteTexture, frameWidth: 16, frameHeight: 16);
var walkCycle = new Sprite(characterSheet)
{
    X = 300,
    Y = 100,
    ZIndex = 1
};
walkCycle.SetAnimation(new Animation(frames: new[] { 0, 1, 2, 3 }, frameDurationMs: 150));

using var tilesetTexture = Texture.CreateImageTexture(renderer, "assets/artwork/z-like/objects.png");
var tileset = new SpriteSheet(tilesetTexture, frameWidth: 16, frameHeight: 16);
var isoGrid = new IsometricGrid(tileWidth: 32, tileHeight: 16);
var tiles = new int[5, 5];
for (var row = 0; row < 5; row++)
for (var col = 0; col < 5; col++)
    tiles[row, col] = (row + col) % 2;

var isoMap = new IsometricTilemap(tiles, tileset, isoGrid)
{
    X = 350,
    Y = 250,
    ZIndex = 0
};

renderer.DrawColor(255, 255, 255, 255);


var loop = new EventLoop(renderer);

var gameObjects = new List<GameObject>()
{
    stationary1,
    moving1,
    walkCycle,
    isoMap
}.OrderBy(o => o.ZIndex).ToList();


loop.Start(gameObjects);

//TTF.Quit();