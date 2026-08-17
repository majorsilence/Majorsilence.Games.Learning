using System.Text.Json;
using Majorsilence.Games.Core;

namespace Majorsilence.Games.Rpg;

/// <summary>
/// The roster of things that can be fought, loaded from assets/monsters.json.
///
/// Monsters are data for the same reason maps are: adding one should mean
/// editing a file and naming it in a level's encounter list, not editing game
/// code. Entries here are templates - a fight gets its own copies (see
/// <see cref="Combatant.Spawn"/>), so a wolf killed on the road is not still
/// dead the next time one turns up.
/// </summary>
public class MonsterBook
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, Combatant> _byKey;

    private MonsterBook(Dictionary<string, Combatant> byKey) => _byKey = byKey;

    private class MonsterJson
    {
        public string Name { get; set; } = "";
        public int Health { get; set; } = 1;
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Agility { get; set; }
        public int Experience { get; set; }
        public int Coin { get; set; }
        public int Frame { get; set; }
        public int MaxGroup { get; set; } = 1;
    }

    public static MonsterBook Load(string path)
    {
        if (!File.Exists(path)) throw new MajorsilenceException($"Monster book not found: {path}");

        Dictionary<string, MonsterJson>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<Dictionary<string, MonsterJson>>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException error)
        {
            throw new MajorsilenceException($"Monster book '{path}' is not valid JSON: {error.Message}");
        }

        if (raw is null) throw new MajorsilenceException($"Monster book '{path}' is empty.");

        var byKey = new Dictionary<string, Combatant>();
        foreach (var (key, monster) in raw)
        {
            if (monster.Health < 1)
                throw new MajorsilenceException($"Monster '{key}' in '{path}' needs at least 1 health.");

            byKey[key] = new Combatant
            {
                Name = monster.Name == "" ? key : monster.Name,
                MaxHealth = monster.Health,
                Health = monster.Health,
                Attack = monster.Attack,
                Defense = monster.Defense,
                Agility = monster.Agility,
                Experience = monster.Experience,
                Coin = monster.Coin,
                Frame = monster.Frame,
                MaxGroup = Math.Max(1, monster.MaxGroup)
            };
        }

        return new MonsterBook(byKey);
    }

    public Combatant this[string key] =>
        _byKey.TryGetValue(key, out var monster)
            ? monster
            : throw new MajorsilenceException($"No monster named '{key}' in the monster book.");

    public bool Contains(string key) => _byKey.ContainsKey(key);

    /// <summary>
    /// Rolls one encounter group from a level's encounter list. The list carries
    /// its own weighting by repetition - naming a monster twice makes it twice
    /// as likely - which keeps the level file readable next to a table of
    /// percentages that has to sum to 100.
    /// </summary>
    public List<Combatant> RollGroup(IReadOnlyList<string> table, Random random)
    {
        var key = table[random.Next(table.Count)];
        var template = this[key];
        var count = 1 + random.Next(template.MaxGroup);

        var group = new List<Combatant>();
        for (var i = 0; i < count; i++)
        {
            // Two of a kind need telling apart in the target cursor and the
            // messages, so they get a letter.
            var name = count > 1 ? $"{template.Name} {(char)('A' + i)}" : template.Name;
            group.Add(template.Spawn(name));
        }

        return group;
    }
}
