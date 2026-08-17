using System.Text.Json;
using Majorsilence.Games.Core;

namespace Majorsilence.Games.Rpg;

/// <summary>What a spell does when it lands.</summary>
public enum SpellKind
{
    /// <summary>Hurts one monster.</summary>
    Damage,

    /// <summary>Hurts every living monster.</summary>
    DamageAll,

    /// <summary>Heals one party member.</summary>
    Heal,

    /// <summary>Heals every living party member.</summary>
    HealAll
}

public class Spell
{
    public required string Key { get; init; }
    public required string Name { get; init; }

    /// <summary>Mana spent to cast it.</summary>
    public int Cost { get; init; }

    /// <summary>Damage or healing before the +/-20% spread Battle applies.</summary>
    public int Power { get; init; }

    public SpellKind Kind { get; init; }

    /// <summary>Fills the middle of the message: "Sella {verb} the Ash-wolf".</summary>
    public string Verb { get; init; } = "casts at";

    public bool TargetsMonsters => Kind is SpellKind.Damage or SpellKind.DamageAll;
    public bool TargetsEveryone => Kind is SpellKind.DamageAll or SpellKind.HealAll;
}

/// <summary>
/// Every spell in the game, loaded from assets/spells.json - data for the same
/// reason monsters and maps are, so adding one means editing a file and naming
/// it in a class's spell list.
/// </summary>
public class SpellBook
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, Spell> _byKey;

    private SpellBook(Dictionary<string, Spell> byKey) => _byKey = byKey;

    /// <summary>Builds a book from spells already in hand, rather than from the spell file.</summary>
    public static SpellBook Of(params Spell[] spells) =>
        new(spells.ToDictionary(spell => spell.Key));

    private class SpellJson
    {
        public string Name { get; set; } = "";
        public int Cost { get; set; }
        public int Power { get; set; }
        public string Kind { get; set; } = "damage";
        public string Verb { get; set; } = "";
    }

    public static SpellBook Load(string path)
    {
        if (!File.Exists(path)) throw new MajorsilenceException($"Spell book not found: {path}");

        Dictionary<string, SpellJson>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<Dictionary<string, SpellJson>>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException error)
        {
            throw new MajorsilenceException($"Spell book '{path}' is not valid JSON: {error.Message}");
        }

        if (raw is null) throw new MajorsilenceException($"Spell book '{path}' is empty.");

        var byKey = new Dictionary<string, Spell>();
        foreach (var (key, spell) in raw)
        {
            byKey[key] = new Spell
            {
                Key = key,
                Name = spell.Name == "" ? key : spell.Name,
                Cost = spell.Cost,
                Power = spell.Power,
                Kind = ParseKind(key, path, spell.Kind),
                Verb = spell.Verb == "" ? "casts at" : spell.Verb
            };
        }

        return new SpellBook(byKey);
    }

    private static SpellKind ParseKind(string key, string path, string kind) => kind.ToLowerInvariant() switch
    {
        "damage" => SpellKind.Damage,
        "damageall" => SpellKind.DamageAll,
        "heal" => SpellKind.Heal,
        "healall" => SpellKind.HealAll,
        _ => throw new MajorsilenceException(
            $"Spell '{key}' in '{path}' has unknown kind '{kind}'. Expected damage, damageAll, heal or healAll.")
    };

    public Spell this[string key] =>
        _byKey.TryGetValue(key, out var spell)
            ? spell
            : throw new MajorsilenceException($"No spell named '{key}' in the spell book.");

    public bool Contains(string key) => _byKey.ContainsKey(key);
}
