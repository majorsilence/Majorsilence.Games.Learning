using Majorsilence.Games.Core;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;
using SDL3;

namespace Majorsilence.Games.Rpg;

/// <summary>
/// Draws a <see cref="Battle"/>: the monsters ranged across the field, the
/// hero's condition, and whichever panel the current phase calls for - the
/// command list, the target cursor, or the line describing what just happened.
///
/// Presentation only. It reads the battle and never changes it, the same split
/// DialogueBox has with the conversation it shows.
/// </summary>
public class BattleScreen : GameObject
{
    private const int MonsterSize = 32;
    private const int Margin = 8;
    private const int PanelHeight = 62;
    private const int StatusHeight = 22;
    private const int PaddingX = 10;
    private const int PaddingY = 7;
    private const int LineHeight = 15;
    private const int FontSize = 11;

    private static readonly SDL.Color TextColor = new() { A = 0, R = 240, G = 240, B = 232 };
    private static readonly SDL.Color CursorColor = new() { A = 0, R = 248, G = 216, B = 120 };
    private static readonly SDL.Color HurtColor = new() { A = 0, R = 232, G = 112, B = 96 };

    private readonly Renderer _renderer;
    private readonly string _fontPath;
    private readonly SpriteSheet _monsterArt;
    private readonly List<Texture> _lines = new();
    private string _builtSignature = "";

    public BattleScreen(Renderer renderer, string fontPath, SpriteSheet monsterArt)
    {
        _renderer = renderer;
        _fontPath = fontPath;
        _monsterArt = monsterArt;
        SortOffsetY = 2_000_000f; // above everything, including the dialogue window
    }

    /// <summary>The fight being shown, or null when the game is out on the map.</summary>
    public Battle? Battle { get; set; }

    public override void Update(float deltaTime)
    {
    }

    public override void Render(Camera camera)
    {
        if (Battle is null) return;

        var (viewWidth, viewHeight) = _renderer.LogicalSize;

        // A flat dark field rather than the map: the fight is somewhere else,
        // and drawing the town behind it would say otherwise.
        _renderer.FillRect(0, 0, viewWidth, viewHeight, 20, 18, 28, 255);
        _renderer.FillRect(0, viewHeight / 2, viewWidth, viewHeight / 2, 34, 30, 40, 255);

        DrawMonsters(viewWidth, viewHeight);
        DrawStatus(viewWidth, viewHeight);
        DrawPanel(viewWidth, viewHeight);
    }

    private void DrawMonsters(int viewWidth, int viewHeight)
    {
        var living = Battle!.Monsters.Where(m => m.IsAlive).ToList();
        if (living.Count == 0) return;

        var spacing = Math.Min(64, viewWidth / (living.Count + 1));
        var top = viewHeight / 2 - MonsterSize - 18;

        for (var i = 0; i < living.Count; i++)
        {
            var x = (viewWidth - spacing * (living.Count - 1)) / 2 + spacing * i - MonsterSize / 2;
            _monsterArt.Render(x, top, living[i].Frame);

            // The cursor sits under whoever is about to be hit, and only while
            // the player is actually choosing.
            if (Battle.Phase == BattlePhase.Target && ReferenceEquals(living[i], Battle.Monsters[Battle.TargetIndex]))
                _renderer.FillRect(x + MonsterSize / 2 - 3, top + MonsterSize + 3, 6, 3, 248, 216, 120, 255);
        }
    }

    private void DrawStatus(int viewWidth, int viewHeight)
    {
        var y = viewHeight - PanelHeight - StatusHeight - Margin - 2;
        var width = 128;

        _renderer.FillRect(Margin, y, width, StatusHeight, 236, 236, 228, 245);
        _renderer.FillRect(Margin + 2, y + 2, width - 4, StatusHeight - 4, 16, 20, 40, 250);
    }

    private void DrawPanel(int viewWidth, int viewHeight)
    {
        var panelY = viewHeight - PanelHeight - Margin;
        var panelWidth = viewWidth - Margin * 2;

        _renderer.FillRect(Margin, panelY, panelWidth, PanelHeight, 236, 236, 228, 245);
        _renderer.FillRect(Margin + 2, panelY + 2, panelWidth - 4, PanelHeight - 4, 16, 20, 40, 250);

        RebuildIfChanged();

        // First line is always the hero's condition, drawn into the status box
        // above the panel; the rest fill the panel itself.
        var statusY = viewHeight - PanelHeight - StatusHeight - Margin - 2 + 5;
        var y = panelY + PaddingY;

        for (var i = 0; i < _lines.Count; i++)
        {
            if (i == 0) _lines[i].Render(Margin + PaddingX, statusY);
            else
            {
                _lines[i].Render(Margin + PaddingX, y);
                y += LineHeight;
            }
        }
    }

    private void RebuildIfChanged()
    {
        var battle = Battle!;
        var signature = string.Join("|",
            _renderer.PixelsPerLogicalUnit,
            battle.Phase,
            battle.Message,
            battle.Command,
            battle.TargetIndex,
            battle.Hero.Health,
            string.Join(",", battle.Monsters.Select(m => m.Health)));

        if (signature == _builtSignature) return;
        _builtSignature = signature;

        foreach (var texture in _lines) texture.Dispose();
        _lines.Clear();

        var hurt = battle.Hero.Health * 4 <= battle.Hero.MaxHealth;
        Add($"{battle.Hero.Name}   HP {battle.Hero.Health}/{battle.Hero.MaxHealth}", hurt ? HurtColor : TextColor);

        switch (battle.Phase)
        {
            case BattlePhase.Command:
                Add(battle.Command == BattleCommand.Fight ? "> Fight" : "  Fight",
                    battle.Command == BattleCommand.Fight ? CursorColor : TextColor);
                Add(battle.Command == BattleCommand.Run ? "> Run" : "  Run",
                    battle.Command == BattleCommand.Run ? CursorColor : TextColor);
                break;

            case BattlePhase.Target:
                Add("Strike which?", TextColor);
                var target = battle.Monsters[battle.TargetIndex];
                Add($"> {target.Name}   HP {target.Health}/{target.MaxHealth}", CursorColor);
                break;

            case BattlePhase.Message:
            case BattlePhase.Over:
                Add(battle.Message, TextColor);
                break;
        }
    }

    private void Add(string text, SDL.Color color)
    {
        if (text == "") return;
        _lines.Add(Texture.CreateTextTexture(_renderer, _fontPath, FontSize, color, text));
    }
}
