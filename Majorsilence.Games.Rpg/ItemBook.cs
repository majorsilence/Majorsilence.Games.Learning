using System.Text.Json;
using Majorsilence.Games.Core;

namespace Majorsilence.Games.Rpg;

/// <summary>What using an item does.</summary>
public enum ItemKind
{
    /// <summary>Restores health to one companion.</summary>
    Heal,

    /// <summary>Restores health to everyone still standing.</summary>
    HealAll,

    /// <summary>Restores mana to one companion.</summary>
    Mana,

    /// <summary>Puts a fallen companion back on their feet, at the fraction of health Power gives.</summary>
    Revive
}

public class Item
{
    public required string Key { get; init; }
    public required string Name { get; init; }

    /// <summary>What a shop charges. Selling back is not a thing yet.</summary>
    public int Price { get; init; }

    /// <summary>Health, mana, or - for a revive - the percentage of health it wakes you at.</summary>
    public int Power { get; init; }

    public ItemKind Kind { get; init; }

    /// <summary>Fills the middle of the message: "Halt {verb} Sella".</summary>
    public string Verb { get; init; } = "uses a thing on";

    /// <summary>Whether this one is aimed at somebody in particular, or just used.</summary>
    public bool NeedsTarget => Kind is not ItemKind.HealAll;

    /// <summary>Revives are the only thing that may be pointed at somebody already down.</summary>
    public bool TargetsTheFallen => Kind is ItemKind.Revive;
}

/// <summary>
/// Every item in the game, loaded from assets/items.json - data for the same
/// reason spells, monsters and maps are.
/// </summary>
public class ItemBook
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, Item> _byKey;

    private ItemBook(Dictionary<string, Item> byKey) => _byKey = byKey;

    /// <summary>Builds a book from items already in hand, rather than from the item file.</summary>
    public static ItemBook Of(params Item[] items) => new(items.ToDictionary(item => item.Key));

    private class ItemJson
    {
        public string Name { get; set; } = "";
        public int Price { get; set; }
        public int Power { get; set; }
        public string Kind { get; set; } = "heal";
        public string Verb { get; set; } = "";
    }

    public static ItemBook Load(string path)
    {
        if (!File.Exists(path)) throw new MajorsilenceException($"Item book not found: {path}");

        Dictionary<string, ItemJson>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<Dictionary<string, ItemJson>>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException error)
        {
            throw new MajorsilenceException($"Item book '{path}' is not valid JSON: {error.Message}");
        }

        if (raw is null) throw new MajorsilenceException($"Item book '{path}' is empty.");

        var byKey = new Dictionary<string, Item>();
        foreach (var (key, item) in raw)
        {
            byKey[key] = new Item
            {
                Key = key,
                Name = item.Name == "" ? key : item.Name,
                Price = item.Price,
                Power = item.Power,
                Kind = ParseKind(key, path, item.Kind),
                Verb = item.Verb == "" ? "uses a thing on" : item.Verb
            };
        }

        return new ItemBook(byKey);
    }

    private static ItemKind ParseKind(string key, string path, string kind) => kind.ToLowerInvariant() switch
    {
        "heal" => ItemKind.Heal,
        "healall" => ItemKind.HealAll,
        "mana" => ItemKind.Mana,
        "revive" => ItemKind.Revive,
        _ => throw new MajorsilenceException(
            $"Item '{key}' in '{path}' has unknown kind '{kind}'. Expected heal, healAll, mana or revive.")
    };

    public Item this[string key] =>
        _byKey.TryGetValue(key, out var item)
            ? item
            : throw new MajorsilenceException($"No item named '{key}' in the item book.");

    public bool Contains(string key) => _byKey.ContainsKey(key);
}
