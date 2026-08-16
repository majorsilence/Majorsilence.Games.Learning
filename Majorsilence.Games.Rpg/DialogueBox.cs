using Majorsilence.Games.Core;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;
using SDL3;

namespace Majorsilence.Games.Rpg;

/// <summary>
/// The bordered text window every console RPG runs its conversations through:
/// a screen-space panel across the bottom, one page of wrapped text at a time,
/// advanced with Confirm. Presentation only - RpgGame owns what is being said
/// and when the conversation ends. Row textures are rebuilt only when the page
/// actually changes, the same way ShopMenu does it.
/// </summary>
public class DialogueBox : GameObject
{
    private const int Margin = 8;
    private const int PanelHeight = 62;
    private const int PaddingX = 10;
    private const int PaddingY = 8;
    private const int LineHeight = 14;
    private const int FontSize = 11;
    private const int MaxLines = 3;
    // Character budget per line at FontSize in the ~320px-wide logical space.
    private const int LineLength = 40;

    private static readonly SDL.Color TextColor = new() { A = 0, R = 240, G = 240, B = 232 };
    private static readonly SDL.Color SpeakerColor = new() { A = 0, R = 248, G = 216, B = 120 };

    private readonly Renderer _renderer;
    private readonly string _fontPath;
    private readonly List<Texture> _lineTextures = new();
    private readonly List<string> _pages = new();
    private int _pageIndex;
    private string _speaker = "";
    private string _builtSignature = "";

    public DialogueBox(Renderer renderer, string fontPath)
    {
        _renderer = renderer;
        _fontPath = fontPath;
        SortOffsetY = 1_000_000f; // screen-space overlay: always on top
    }

    public bool IsOpen { get; private set; }

    /// <summary>Opens the window on the first page of what this speaker has to say.</summary>
    public void Show(string speaker, string text)
    {
        _speaker = speaker;
        _pages.Clear();
        _pages.AddRange(Paginate(text));
        _pageIndex = 0;
        IsOpen = _pages.Count > 0;
    }

    /// <summary>Advances a page; returns false once the last page has been dismissed (the conversation is over).</summary>
    public bool Advance()
    {
        _pageIndex++;
        if (_pageIndex < _pages.Count) return true;
        Close();
        return false;
    }

    public void Close()
    {
        IsOpen = false;
        _pages.Clear();
        _pageIndex = 0;
    }

    public override void Update(float deltaTime)
    {
    }

    public override void Render(Camera camera)
    {
        if (!IsOpen) return;

        RebuildIfChanged();

        var (viewWidth, viewHeight) = _renderer.LogicalSize;
        var panelY = viewHeight - PanelHeight - Margin;
        var panelWidth = viewWidth - Margin * 2;

        // Two nested rectangles: a light border around a near-black field, the
        // standard console-RPG window, and readable over any map beneath it.
        _renderer.FillRect(Margin, panelY, panelWidth, PanelHeight, 236, 236, 228, 245);
        _renderer.FillRect(Margin + 2, panelY + 2, panelWidth - 4, PanelHeight - 4, 16, 20, 40, 250);

        var y = panelY + PaddingY;
        foreach (var line in _lineTextures)
        {
            line.Render(Margin + PaddingX, y);
            y += LineHeight;
        }
    }

    private void RebuildIfChanged()
    {
        // Scale is part of the signature so a resized window re-rasterizes the
        // text for its new pixel density (see Texture.CreateTextTexture).
        var signature = $"{_renderer.PixelsPerLogicalUnit}|{_speaker}|{_pageIndex}|{CurrentPage}";
        if (signature == _builtSignature) return;
        _builtSignature = signature;

        foreach (var texture in _lineTextures) texture.Dispose();
        _lineTextures.Clear();

        if (_speaker != "")
            _lineTextures.Add(Texture.CreateTextTexture(_renderer, _fontPath, FontSize, SpeakerColor, _speaker));

        foreach (var line in Wrap(CurrentPage))
            _lineTextures.Add(Texture.CreateTextTexture(_renderer, _fontPath, FontSize, TextColor, line));
    }

    private string CurrentPage => _pageIndex < _pages.Count ? _pages[_pageIndex] : "";

    /// <summary>Splits a speech into window-sized pages, each at most MaxLines wrapped lines (one of which the speaker's name takes).</summary>
    private static IEnumerable<string> Paginate(string text)
    {
        var lines = Wrap(text);
        var perPage = MaxLines - 1;
        for (var i = 0; i < lines.Count; i += perPage)
            yield return string.Join(" ", lines.GetRange(i, Math.Min(perPage, lines.Count - i)));
    }

    private static List<string> Wrap(string text)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length == 0) line = word;
            else if (line.Length + 1 + word.Length <= LineLength) line += " " + word;
            else
            {
                lines.Add(line);
                line = word;
            }
        }

        if (line.Length > 0) lines.Add(line);
        return lines;
    }
}
