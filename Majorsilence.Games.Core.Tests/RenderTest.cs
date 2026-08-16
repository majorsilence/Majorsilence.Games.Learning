using Majorsilence.Games.Core.Textures;
using SDL3;
using Xunit;

namespace Majorsilence.Games.Core.Tests;

/// <summary>
/// The one test here that needs SDL: it opens a real window and draws to it.
/// Everything else in this project is pure math and runs anywhere.
///
/// These skip rather than fail when there's nowhere to draw, because a suite
/// that is permanently red on a headless box is a suite everyone learns to
/// ignore. To run them without a display, use SDL_VIDEODRIVER=offscreen - not
/// "dummy", which has no renderer at all and fails at the first draw.
/// </summary>
public class RenderTest
{
    private static string VideoDriver =>
        Environment.GetEnvironmentVariable("SDL_VIDEODRIVER") ?? "";

    private static bool HasDisplay =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    /// <summary>
    /// Whether SDL can give us a renderer here at all. "dummy" is a video driver
    /// with no rendering behind it, so it is a no even on a machine that has a
    /// display; "offscreen" renders for real into memory, so it is a yes even on
    /// a machine that has none.
    /// </summary>
    private static bool CanRender =>
        VideoDriver != "dummy" && (VideoDriver == "offscreen" || HasDisplay);

    private const string SkipReason =
        "no renderer available; run headless with SDL_VIDEODRIVER=offscreen";

    [Fact]
    public void RendersASpriteAndTextToAWindow()
    {
        if (!CanRender) Assert.Skip(SkipReason);

        using var window = new Window("SDL3 Displaying Image", 640, 480);
        using var renderer = new Renderer(window);
        renderer.SyncLogicalPresentationToWindow();

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
        Assert.Equal(640, size.Width);
        Assert.Equal(480, size.Height);
    }

    [Fact]
    public void TogglesFullscreen()
    {
        if (!CanRender) Assert.Skip(SkipReason);

        using var window = new Window("SDL3 Fullscreen Toggle", 640, 480);
        using var renderer = new Renderer(window);

        Assert.False(renderer.IsFullscreen);
        renderer.SetFullscreen(true);
        Assert.True(renderer.IsFullscreen);
        renderer.SetFullscreen(false);
        Assert.False(renderer.IsFullscreen);
    }
}
