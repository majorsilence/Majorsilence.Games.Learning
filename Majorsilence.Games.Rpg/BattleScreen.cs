using Majorsilence.Games.Core;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;
using SDL3;

namespace Majorsilence.Games.Rpg;

/// <summary>
/// Draws a <see cref="Battle"/>: the monsters ranged across the field, the
/// party's condition, and whichever panel the current phase calls for - the
/// command list, the spell list, a target cursor, or the line describing what
/// just happened.
///
/// Presentation only. It reads the battle and never changes it, the same split
/// DialogueBox has with the conversation it shows.
/// </summary>
public class BattleScreen : GameObject
{
    private const int MonsterSize = 32;
    private const int Margin = 8;
    private const int PanelHeight = 46;
    private const int PaddingX = 10;
    private const int PaddingY = 6;
    private const int LineHeight = 14;
    private const int FontSize = 11;

    private static readonly SDL.Color TextColor = new() { A = 0, R = 240, G = 240, B = 232 };
    private static readonly SDL.Color CursorColor = new() { A = 0, R = 248, G = 216, B = 120 };
    private static readonly SDL.Color HurtColor = new() { A = 0, R = 232, G = 112, B = 96 };
    private static readonly SDL.Color DownColor = new() { A = 0, R = 128, G = 124, B = 132 };

    private readonly Renderer _renderer;
    private readonly string _fontPath;
    private readonly SpriteSheet _monsterArt;
    private readonly List<Texture> _statusLines = new();
    private readonly List<Texture> _panelLines = new();
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
        var statusHeight = Battle.Party.Count * LineHeight + PaddingY * 2;

        // A flat dark field rather than the map: the fight is somewhere else,
        // and drawing the town behind it would say otherwise.
        _renderer.FillRect(0, 0, viewWidth, viewHeight, 20, 18, 28, 255);
        _renderer.FillRect(0, viewHeight / 2, viewWidth, viewHeight / 2, 34, 30, 40, 255);

        RebuildIfChanged();

        var statusY = viewHeight - PanelHeight - statusHeight - Margin - 2;
        DrawMonsters(viewWidth, statusY);
        DrawBox(Margin, statusY, viewWidth - Margin * 2, statusHeight, _statusLines);
        DrawBox(Margin, viewHeight - PanelHeight - Margin, viewWidth - Margin * 2, PanelHeight, _panelLines);
    }

    private void DrawBox(int x, int y, int width, int height, List<Texture> lines)
    {
        // Two nested rectangles: a light border around a near-black field.
        _renderer.FillRect(x, y, width, height, 236, 236, 228, 245);
        _renderer.FillRect(x + 2, y + 2, width - 4, height - 4, 16, 20, 40, 250);

        var lineY = y + PaddingY;
        foreach (var line in lines)
        {
            line.Render(x + PaddingX, lineY);
            lineY += LineHeight;
        }
    }

    private void DrawMonsters(int viewWidth, int fieldBottom)
    {
        var living = Battle!.Monsters.Where(m => m.IsAlive).ToList();
        if (living.Count == 0) return;

        var spacing = Math.Min(64, viewWidth / (living.Count + 1));
        var top = Math.Max(Margin, fieldBottom / 2 - MonsterSize / 2);

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

    private void RebuildIfChanged()
    {
        var battle = Battle!;
        var signature = string.Join("|",
            _renderer.PixelsPerLogicalUnit,
            battle.Phase,
            battle.Message,
            battle.Planning?.Name,
            battle.CommandIndex,
            battle.SpellIndex,
            battle.ItemIndex,
            battle.TargetIndex,
            battle.AllyIndex,
            string.Join(",", battle.Party.Select(m => $"{m.Health}/{m.Mana}/{m.Level}")),
            string.Join(",", battle.Monsters.Select(m => m.Health)),
            string.Join(",", battle.Bag.Keys.Select(k => $"{k}{battle.Bag.CountOf(k)}")),
            battle.Bag.Coin);

        if (signature == _builtSignature) return;
        _builtSignature = signature;

        foreach (var texture in _statusLines) texture.Dispose();
        foreach (var texture in _panelLines) texture.Dispose();
        _statusLines.Clear();
        _panelLines.Clear();

        BuildStatus(battle);
        BuildPanel(battle);
    }

    private void BuildStatus(Battle battle)
    {
        foreach (var member in battle.Party)
        {
            // A marker rather than a separate cursor sprite: it shows who is
            // being given orders while their stats are already on the line.
            var planning = ReferenceEquals(member, battle.Planning);
            var mana = member.MaxMana > 0 ? $"   MP {member.Mana}/{member.MaxMana}" : "";
            var text = $"{(planning ? ">" : " ")} {member.Name,-6} L{member.Level}  HP {member.Health}/{member.MaxHealth}{mana}";

            var color = !member.IsAlive ? DownColor
                : planning ? CursorColor
                : member.Health * 4 <= member.MaxHealth ? HurtColor
                : TextColor;

            _statusLines.Add(Text(text, color));
        }
    }

    private void BuildPanel(Battle battle)
    {
        switch (battle.Phase)
        {
            case BattlePhase.Command:
                _panelLines.Add(Text($"{battle.Planning?.Name} - what will you do?", TextColor));
                _panelLines.Add(Text(Row(battle.Commands.Select(c => c.ToString()), battle.CommandIndex), CursorColor));
                break;

            case BattlePhase.Item:
                _panelLines.Add(Text($"{battle.Planning?.Name} reaches for:", TextColor));
                _panelLines.Add(Text(Row(battle.Bag.Keys.Select(ItemLabel), battle.ItemIndex), CursorColor));
                break;

            case BattlePhase.Spell when battle.Planning is { } caster:
                _panelLines.Add(Text($"{caster.Name} calls on:", TextColor));
                _panelLines.Add(Text(Row(caster.Spells.Select(SpellLabel), battle.SpellIndex), CursorColor));
                break;

            case BattlePhase.Target:
            {
                var target = battle.Monsters[battle.TargetIndex];
                _panelLines.Add(Text("Against which?", TextColor));
                _panelLines.Add(Text($"> {target.Name}   HP {target.Health}/{target.MaxHealth}", CursorColor));
                break;
            }

            case BattlePhase.AllyTarget:
            {
                var ally = battle.Party[battle.AllyIndex];
                _panelLines.Add(Text("For whom?", TextColor));
                _panelLines.Add(Text($"> {ally.Name}   HP {ally.Health}/{ally.MaxHealth}"
                    + (ally.IsAlive ? "" : "   (down)"), CursorColor));
                break;
            }

            case BattlePhase.Message:
            case BattlePhase.Over:
                _panelLines.Add(Text(battle.Message, TextColor));
                break;
        }
    }

    /// <summary>Name and price for one entry in a caster's list - every entry, not just the selected one.</summary>
    private string SpellLabel(string key)
    {
        var spell = Battle!.SpellFor(key);
        return $"{spell.Name} {spell.Cost}";
    }

    /// <summary>Name and how many are left - the count is the thing you actually decide on.</summary>
    private string ItemLabel(string key)
    {
        var item = Battle!.ItemFor(key);
        return $"{item.Name} x{Battle.Bag.CountOf(key)}";
    }

    /// <summary>Lays choices out along one line with the cursor on the chosen one - menus here are short enough not to need a column.</summary>
    private static string Row(IEnumerable<string> options, int selected)
    {
        var list = options.ToList();
        return string.Join("   ", list.Select((option, i) => i == selected ? $"> {option}" : $"  {option}"));
    }

    private Texture Text(string text, SDL.Color color) =>
        Texture.CreateTextTexture(_renderer, _fontPath, FontSize, color, text == "" ? " " : text);
}
