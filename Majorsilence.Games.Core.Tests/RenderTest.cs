using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Core.Tests;

public class RenderTest
{
    public void Test1()
    {
        using var window = new Window("SDL2 Displaying Image", 640, 480);
        using var renderer = new Renderer(window);

        using var spriteTexture = Texture.CreateImageTexture(renderer,
            "assets/artwork/z-like/character.png",
            new SDL2.SDL.SDL_Color { a = 255, b = 255, g = 255, r = 255 });

        using var textTexture = Texture.CreateTextTexture(renderer,
            "assets/fonts/Gidole-Regular.ttf",
            size: 25,
            new SDL2.SDL.SDL_Color { a = 0, b = 155, g = 155, r = 155 },
            "Hello World"
        );

        renderer.DrawColor(255, 255, 255, 255);
        
        renderer.Clear();
        spriteTexture.Render(50, 50);
        textTexture.Render(50, 50);
        renderer.Present();
        
        var size = renderer.Size;
        
        System.Diagnostics.Debug.Assert(size.Height == 480);
        System.Diagnostics.Debug.Assert(size.Width == 640);
    }
}