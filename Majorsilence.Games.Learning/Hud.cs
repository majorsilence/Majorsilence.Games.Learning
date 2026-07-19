using Majorsilence.Games.Core;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;
using SDL3;

namespace Majorsilence.Games.Learning;

/// <summary>
/// A screen-space text overlay whose displayed string can change at runtime
/// (unlike StationaryObject, which is built once around a fixed texture).
/// Re-renders its backing texture only when the text actually changes.
/// </summary>
public class Hud : GameObject
{
    private readonly Renderer _renderer;
    private readonly string _fontPath;
    private readonly int _size;
    private readonly SDL.Color _color;
    private Texture? _texture;
    private string _lastText = "";

    public Hud(Renderer renderer, string fontPath, int size, SDL.Color color)
    {
        _renderer = renderer;
        _fontPath = fontPath;
        _size = size;
        _color = color;
        // Screen-space overlay: sort above every world tile/sprite (its X/Y are
        // screen coords near 0, which would otherwise bury it behind the map).
        SortOffsetY = 1_000_000f;
    }

    /// <summary>The text currently shown - readable so game logic/tests can inspect what the player sees.</summary>
    public string Text { get; private set; } = "";

    public void SetText(string text)
    {
        Text = text;
        if (text == _lastText) return;
        _lastText = text;
        _texture?.Dispose();
        _texture = string.IsNullOrEmpty(text) ? null : Texture.CreateTextTexture(_renderer, _fontPath, _size, _color, text);
    }

    public override void Update(float deltaTime)
    {
        // Text is pushed in via SetText from Game's per-frame logic.
    }

    public override void Render(Camera camera)
    {
        _texture?.Render(X, Y);
    }
}
