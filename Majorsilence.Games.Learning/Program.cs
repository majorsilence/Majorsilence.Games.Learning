using System.Runtime.InteropServices;
using SDL2;
using Majorsilence.Games.Learning;
using Majorsilence.Games.Learning.Surfaces;
using Majorsilence.Games.Learning.Textures;

using var window = new Window("SDL2 Displaying Image", 640, 480);
using var renderer = new Renderer(window);

//var image = new Majorsilence.Games.Learning.Image("/Users/petergill/Downloads/stick_people.png");

//using var image = new ImageSurface("assets/artwork/z-like/character.png");
//image.ColorAsTransparent(255, 255, 255);

//using var texture = new Texture(renderer, image);
using var spriteTexture = Texture.CreateImageTexture(renderer,
    "assets/artwork/z-like/character.png",
    new SDL2.SDL.SDL_Color { a = 255, b = 255, g = 255, r = 255 });

//using var font = new Fonts("assets/fonts/Gidole-Regular.ttf", 25);
//var color = new SDL2.SDL.SDL_Color { a = 0, b = 155, g = 155, r = 150 };
//using var text = new TextSurface(font, color, "Hello World");
//using var textTexture = new Texture(renderer, text);
using var textTexture = Texture.CreateTextTexture(renderer,
    "assets/fonts/Gidole-Regular.ttf",
    size: 25,
    new SDL2.SDL.SDL_Color { a = 0, b = 155, g = 155, r = 155 },
    "Hello World"
);

var stationary1 = new PlaceholderStationaryObject(textTexture, 0, 0);
var moving1 = new PlaceholderMovingObject(spriteTexture);

renderer.DrawColor(255, 255, 255, 255);


var loop = new EventLoop(renderer);

var stationaryObjects = new List<PlaceholderStationaryObject>()
{
    stationary1
}.OrderBy(o => o.ZIndex).ToList();
var movingObjects = new List<PlaceholderMovingObject>()
{
    moving1
}.OrderBy(o => o.ZIndex).ToList();

loop.Start(movingObjects, stationaryObjects);

//SDL_ttf.TTF_Quit();