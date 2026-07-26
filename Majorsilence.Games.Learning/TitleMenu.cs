using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Majorsilence.Games.Core;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;
using SDL3;

namespace Majorsilence.Games.Learning;

/// <summary>The terminal outcome of the title screen - Program reads this once Choice != None to decide how to launch the game (or not, for Quit).</summary>
public enum TitleChoice
{
    None,
    Continue,
    New,
    FreePlay,
    Quit
}

/// <summary>
/// The title screen: a full-screen menu run in its own pre-game EventLoop
/// (see EventLoop.Stop), replacing the old console prompts so the game is
/// launchable from a store client with no attached terminal. Self-contained
/// (reads InputActions directly, single-input - co-op assignment happens
/// after a mode is chosen, same as before) unlike ShopMenu, which is driven
/// by Game. Same cached-texture-per-row rendering approach as ShopMenu.
/// </summary>
public class TitleMenu : GameObject
{
    private const int PanelX = 40;
    private const int PanelY = 60;
    private const int PanelWidth = 340;
    private const int RowHeight = 20;
    private const int PaddingX = 10;
    private const int PaddingY = 10;
    private const int TitleRowHeight = 30;
    private const int SubtitleRowHeight = 22;

    private static readonly SDL.Color TitleColor = new() { A = 0, R = 255, G = 210, B = 60 };
    private static readonly SDL.Color SelectedColor = new() { A = 0, R = 255, G = 255, B = 255 };
    private static readonly SDL.Color NormalColor = new() { A = 0, R = 160, G = 165, B = 175 };
    private static readonly SDL.Color HintColor = new() { A = 0, R = 110, G = 115, B = 125 };

    private readonly Renderer _renderer;
    private readonly string _fontPath;
    private readonly CloudSaveClient? _cloud;
    private readonly List<Texture> _lineTextures = new();
    private readonly List<(string Label, TitleChoice Choice)> _rows = new();
    private readonly int _coopRowIndex = -1;
    private readonly int _linkRowIndex = -1;
    private int _selectedIndex;
    private string _builtSignature = "";
    private bool _linkRequestInFlight;

    public bool IsVisible { get; set; } = true;
    public bool CoopEnabled { get; private set; }
    public TitleChoice Choice { get; private set; } = TitleChoice.None;

    public TitleMenu(Renderer renderer, string fontPath, string continueLabel, bool coopAvailable, CloudSaveClient? cloud = null)
    {
        _renderer = renderer;
        _fontPath = fontPath;
        _cloud = cloud;
        SortOffsetY = 2_000_000f;

        _rows.Add((continueLabel, TitleChoice.Continue));
        _rows.Add(("New Campaign (erases saved progress)", TitleChoice.New));
        _rows.Add(("Free Play - a single voyage, nothing saved", TitleChoice.FreePlay));
        if (coopAvailable)
        {
            _coopRowIndex = _rows.Count;
            _rows.Add(("Co-op: Off", TitleChoice.None));
        }
        if (_cloud is not null)
        {
            _linkRowIndex = _rows.Count;
            _rows.Add(("Link Device (cloud save)", TitleChoice.None));
        }
        _rows.Add(("Quit", TitleChoice.Quit));
    }

    public override void Update(float deltaTime)
    {
        if (Choice != TitleChoice.None) return;

        if (InputActions.IsJustPressed(InputAction.MoveDown))
            _selectedIndex = (_selectedIndex + 1) % _rows.Count;
        else if (InputActions.IsJustPressed(InputAction.MoveUp))
            _selectedIndex = (_selectedIndex - 1 + _rows.Count) % _rows.Count;

        if (InputActions.IsJustPressed(InputAction.Confirm))
        {
            if (_selectedIndex == _coopRowIndex)
            {
                CoopEnabled = !CoopEnabled;
                _rows[_coopRowIndex] = ($"Co-op: {(CoopEnabled ? "On" : "Off")}", TitleChoice.None);
            }
            else if (_selectedIndex == _linkRowIndex)
            {
                RequestLinkCode();
            }
            else
            {
                Choice = _rows[_selectedIndex].Choice;
            }
        }
    }

    private void RequestLinkCode()
    {
        if (_linkRequestInFlight || _cloud is null) return;
        _linkRequestInFlight = true;
        _rows[_linkRowIndex] = ("Requesting code...", TitleChoice.None);

        _ = RequestLinkCodeAsync();

        async Task RequestLinkCodeAsync()
        {
            var code = await _cloud.RequestLinkCodeAsync();
            _rows[_linkRowIndex] = code is not null
                ? ($"Code: {code} - enter at {_cloud.LinkSiteUrl}", TitleChoice.None)
                : ("Could not reach the server - try again later", TitleChoice.None);
            _linkRequestInFlight = false;
        }
    }

    public override void Render(Camera camera)
    {
        if (!IsVisible) return;

        RebuildIfChanged();

        var panelHeight = PaddingY * 2 + TitleRowHeight + SubtitleRowHeight + RowHeight * _rows.Count;
        _renderer.FillRect(PanelX, PanelY, PanelWidth, panelHeight, 10, 16, 28, 235);

        var y = PanelY + PaddingY;
        for (var i = 0; i < _lineTextures.Count; i++)
        {
            _lineTextures[i].Render(PanelX + PaddingX, y);
            y += i == 0 ? TitleRowHeight : i == 1 ? SubtitleRowHeight : RowHeight;
        }
    }

    private void RebuildIfChanged()
    {
        var signature = $"{_selectedIndex}{string.Join("", _rows.ConvertAll(r => r.Label))}";
        if (signature == _builtSignature) return;
        _builtSignature = signature;

        foreach (var texture in _lineTextures) texture.Dispose();
        _lineTextures.Clear();

        _lineTextures.Add(Texture.CreateTextTexture(_renderer, _fontPath, 22, TitleColor, "RMS TITANIC"));
        _lineTextures.Add(Texture.CreateTextTexture(_renderer, _fontPath, 12, HintColor, "A four-voyage disaster campaign"));
        for (var i = 0; i < _rows.Count; i++)
        {
            var selected = i == _selectedIndex;
            var text = (selected ? "> " : "  ") + _rows[i].Label;
            _lineTextures.Add(Texture.CreateTextTexture(_renderer, _fontPath, 14, selected ? SelectedColor : NormalColor, text));
        }
    }
}
