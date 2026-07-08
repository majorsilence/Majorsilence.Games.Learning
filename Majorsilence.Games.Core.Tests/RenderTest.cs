using Majorsilence.Games.Core.Textures;
using SDL3;

namespace Majorsilence.Games.Core.Tests;

public class RenderTest
{
    public void Test1()
    {
        using var window = new Window("SDL3 Displaying Image", 640, 480);
        using var renderer = new Renderer(window);

        using var spriteTexture = Texture.CreateImageTexture(renderer,
            "assets/artwork/z-like/character.png",
            new SDL.Color { A = 255, B = 255, G = 255, R = 255 });

        using var textTexture = Texture.CreateTextTexture(renderer,
            "assets/fonts/Gidole-Regular.ttf",
            size: 25,
            new SDL.Color { A = 0, B = 155, G = 155, R = 155 },
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

        System.Diagnostics.Debug.Assert(renderer.IsFullscreen == false);
        renderer.SetFullscreen(true);
        System.Diagnostics.Debug.Assert(renderer.IsFullscreen == true);
        renderer.SetFullscreen(false);
        System.Diagnostics.Debug.Assert(renderer.IsFullscreen == false);
    }
}
