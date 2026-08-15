using System;
using Majorsilence.Games.Core;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;
using SDL3;

namespace Majorsilence.Games.Learning;

/// <summary>
/// A short line of text anchored to a world position rather than the screen (unlike
/// Hud, whose X/Y are screen coordinates) - drifts with the room like any other prop
/// and gently bobs in place so it reads as a hanging sign rather than floor
/// decoration. Built once around a fixed string (unlike Hud, whose text changes at
/// runtime) since it's meant for landmarks (e.g. "SHOP"), not status text.
/// </summary>
public class WorldLabel : GameObject
{
    private readonly Texture _texture;
    private float _bobTime;

    public WorldLabel(Renderer renderer, string fontPath, int size, SDL.Color color, string text, int x, int y)
    {
        _texture = Texture.CreateTextTexture(renderer, fontPath, size, color, text);
        X = x;
        Y = y;

        // A landmark sign should never be hidden behind the counter/wall it labels -
        // pushing its sort footprint far past anything else on screen guarantees it
        // always paints last (on top), regardless of where it sits on the grid.
        SortOffsetY = 10_000f;
    }

    public override void Update(float deltaTime)
    {
        _bobTime += deltaTime;
    }

    public override void Render(Camera camera)
    {
        // Bob is a purely visual wobble computed at render time rather than folded
        // into Y - Y stays the authoritative world position (e.g. for ShiftBy, if
        // this were ever placed in a drifting room), un-clobbered frame to frame.
        var bob = (int)MathF.Round(MathF.Sin(_bobTime * 2f) * 3f);
        var (screenX, screenY) = camera.WorldToScreen(X, Y - bob);
        // LogicalWidth, not Rect.W: a text texture is rasterized at display
        // resolution, so its pixel width is a multiple of what it covers here.
        _texture.Render(screenX - _texture.LogicalWidth / 2, screenY);
    }
}
