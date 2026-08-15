using System;
using System.Collections.Generic;
using Majorsilence.Games.Core;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;
using SDL3;

namespace Majorsilence.Games.Learning;

/// <summary>
/// The purser's shop panel: a screen-space overlay listing items for sale.
/// Presentation only - Game owns which rows exist, the current selection, and
/// what buying does; this draws whatever rows it was last given, highlighting
/// the selected one. Row textures are rebuilt only when the content actually
/// changes (same idea as Hud.SetText), since text rendering is expensive
/// relative to everything else on screen.
/// </summary>
public class ShopMenu : GameObject
{
    private const int PanelX = 8;
    private const int PanelY = 44;
    private const int PanelWidth = 250;
    private const int RowHeight = 15;
    private const int PaddingX = 8;
    private const int PaddingY = 6;

    private static readonly SDL.Color TitleColor = new() { A = 0, R = 255, G = 210, B = 60 };
    private static readonly SDL.Color SelectedColor = new() { A = 0, R = 255, G = 255, B = 255 };
    private static readonly SDL.Color NormalColor = new() { A = 0, R = 160, G = 165, B = 175 };

    private readonly Renderer _renderer;
    private readonly string _fontPath;
    private readonly List<Texture> _lineTextures = new();
    private string _title = "";
    private IReadOnlyList<string> _rows = Array.Empty<string>();
    private int _selectedIndex;
    private string _builtSignature = "";

    public bool IsVisible { get; set; }

    public ShopMenu(Renderer renderer, string fontPath)
    {
        _renderer = renderer;
        _fontPath = fontPath;
        // Above the Hud (1M) - the panel must cover the status line area behind it.
        SortOffsetY = 2_000_000f;
    }

    /// <summary>Row strings should NOT include a selection marker - the selected row is highlighted here by color and a "&gt;" prefix.</summary>
    public void SetContent(string title, IReadOnlyList<string> rows, int selectedIndex)
    {
        _title = title;
        _rows = rows;
        _selectedIndex = selectedIndex;
    }

    public override void Update(float deltaTime)
    {
    }

    public override void Render(Camera camera)
    {
        if (!IsVisible) return;

        RebuildIfChanged();

        var panelHeight = PaddingY * 2 + RowHeight * (_rows.Count + 1);
        _renderer.FillRect(PanelX, PanelY, PanelWidth, panelHeight, 10, 16, 28, 235);

        var y = PanelY + PaddingY;
        foreach (var line in _lineTextures)
        {
            line.Render(PanelX + PaddingX, y);
            y += RowHeight;
        }
    }

    private void RebuildIfChanged()
    {
        // The render scale is part of the signature: a resized window needs the
        // rows re-rasterized for its new pixel density, not just new content.
        var signature = $"{_renderer.PixelsPerLogicalUnit}|{_title}{_selectedIndex}{string.Join("", _rows)}";
        if (signature == _builtSignature) return;
        _builtSignature = signature;

        foreach (var texture in _lineTextures) texture.Dispose();
        _lineTextures.Clear();

        _lineTextures.Add(Texture.CreateTextTexture(_renderer, _fontPath, 12, TitleColor, _title));
        for (var i = 0; i < _rows.Count; i++)
        {
            var selected = i == _selectedIndex;
            var text = (selected ? "> " : "  ") + _rows[i];
            _lineTextures.Add(Texture.CreateTextTexture(_renderer, _fontPath, 12, selected ? SelectedColor : NormalColor, text));
        }
    }
}
