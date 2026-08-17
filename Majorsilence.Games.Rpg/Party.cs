using System.Text.Json;
using Majorsilence.Games.Core;

namespace Majorsilence.Games.Rpg;

/// <summary>
/// The characters you travel with: who they are, what they've earned, and how
/// they grow.
///
/// Experience is shared rather than split. Splitting it punishes you for the
/// party being larger, which is the opposite of what having a party should feel
/// like, and it means a character who was unconscious at the end doesn't fall
/// permanently behind the others.
/// </summary>
public class Party
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private Party(List<Combatant> members) => Members = members;

    /// <summary>Builds a party from characters already in hand, rather than from the roster file.</summary>
    public static Party Of(params Combatant[] members)
    {
        if (members.Length == 0) throw new MajorsilenceException("A party needs at least one member.");
        return new Party(members.ToList());
    }

    public List<Combatant> Members { get; }

    public int Experience { get; private set; }

    public IEnumerable<Combatant> Living => Members.Where(m => m.IsAlive);

    /// <summary>True when everyone is down - the party has lost.</summary>
    public bool Wiped => Members.All(m => !m.IsAlive);

    /// <summary>The one whose name and health the map HUD shows.</summary>
    public Combatant Leader => Members[0];

    private class MemberJson
    {
        public string Name { get; set; } = "";
        public string ClassName { get; set; } = "";
        public int Health { get; set; } = 1;
        public int Mana { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Agility { get; set; }
        public GrowthJson Growth { get; set; } = new();
        public List<string> Spells { get; set; } = new();
    }

    private class GrowthJson
    {
        public int Health { get; set; }
        public int Mana { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Agility { get; set; }
    }

    public static Party Load(string path, SpellBook spells)
    {
        if (!File.Exists(path)) throw new MajorsilenceException($"Party roster not found: {path}");

        List<MemberJson>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<MemberJson>>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException error)
        {
            throw new MajorsilenceException($"Party roster '{path}' is not valid JSON: {error.Message}");
        }

        if (raw is null || raw.Count == 0)
            throw new MajorsilenceException($"Party roster '{path}' has nobody in it.");

        var members = new List<Combatant>();
        foreach (var member in raw)
        {
            foreach (var spell in member.Spells)
            {
                if (!spells.Contains(spell))
                    throw new MajorsilenceException($"'{member.Name}' in '{path}' knows unknown spell '{spell}'.");
            }

            members.Add(new Combatant
            {
                Name = member.Name,
                ClassName = member.ClassName,
                MaxHealth = member.Health,
                Health = member.Health,
                MaxMana = member.Mana,
                Mana = member.Mana,
                Attack = member.Attack,
                Defense = member.Defense,
                Agility = member.Agility,
                Spells = new List<string>(member.Spells),
                Growth = new Growth
                {
                    Health = member.Growth.Health,
                    Mana = member.Growth.Mana,
                    Attack = member.Growth.Attack,
                    Defense = member.Growth.Defense,
                    Agility = member.Growth.Agility
                }
            });
        }

        return new Party(members);
    }

    /// <summary>
    /// Total experience needed to have reached a given level. Triangular, so
    /// each level costs a bit more than the last without running away: 20 to
    /// reach 2, 60 for 3, 120 for 4, 200 for 5.
    /// </summary>
    public static int ExperienceForLevel(int level) => 10 * level * (level - 1);

    /// <summary>
    /// Banks experience and levels up anyone it carries over a threshold,
    /// returning a line for each level gained (several are possible from one
    /// big fight). Levelling restores nothing - the growth is in the maximums,
    /// and topping people up is the inn's job.
    /// </summary>
    public List<string> AwardExperience(int amount)
    {
        Experience += amount;
        var announcements = new List<string>();

        foreach (var member in Members)
        {
            while (Experience >= ExperienceForLevel(member.Level + 1))
            {
                member.Level++;
                member.MaxHealth += member.Growth.Health;
                member.Health += member.Growth.Health;
                member.MaxMana += member.Growth.Mana;
                member.Mana += member.Growth.Mana;
                member.Attack += member.Growth.Attack;
                member.Defense += member.Growth.Defense;
                member.Agility += member.Growth.Agility;

                announcements.Add($"{member.Name} reaches level {member.Level}.");
            }
        }

        return announcements;
    }

    /// <summary>Full health and mana for everyone, the dead included - what a bed at the inn buys, and what a defeat costs you instead of the save.</summary>
    public void RestoreAll()
    {
        foreach (var member in Members)
        {
            member.Health = member.MaxHealth;
            member.Mana = member.MaxMana;
        }
    }
}
