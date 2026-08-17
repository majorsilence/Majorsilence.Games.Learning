namespace Majorsilence.Games.Rpg;

/// <summary>
/// What one level adds to a character. Flat per-level gains rather than a curve:
/// a player can read "+5 health a level" off a class and know what they are
/// choosing, which a growth exponent does not give them.
/// </summary>
public class Growth
{
    public int Health { get; init; }
    public int Mana { get; init; }
    public int Attack { get; init; }
    public int Defense { get; init; }
    public int Agility { get; init; }
}

/// <summary>
/// Anything that can take a turn in a battle - every party member and every
/// monster alike.
///
/// Deliberately a plain mutable bag of numbers with no behaviour: who hits whom
/// for how much lives in <see cref="Battle"/>, so the formulas are in one place
/// and can be read (and tested) without chasing methods across classes. Growing
/// with a level lives in <see cref="Party"/> for the same reason.
/// </summary>
public class Combatant
{
    public required string Name { get; init; }

    /// <summary>What this one is - "Warden", "Emberwright". Empty for monsters, who are only ever their name.</summary>
    public string ClassName { get; init; } = "";

    public int Level { get; set; } = 1;

    /// <summary>Mutable because levelling raises it; Health is clamped to it everywhere it is spent.</summary>
    public required int MaxHealth { get; set; }
    public int Health { get; set; }

    /// <summary>Spell fuel. Zero for anyone who doesn't cast, which includes every monster so far.</summary>
    public int MaxMana { get; set; }
    public int Mana { get; set; }

    /// <summary>Keys into the spell book. Order is the order they're offered in the menu.</summary>
    public List<string> Spells { get; init; } = new();

    /// <summary>What each level adds. Monsters never level, so theirs is all zeroes.</summary>
    public Growth Growth { get; init; } = new();

    /// <summary>Drives damage dealt.</summary>
    public int Attack { get; set; }

    /// <summary>Halves incoming attack power (see Battle.DamageFrom).</summary>
    public int Defense { get; set; }

    /// <summary>Turn order, hit chance and escape chance.</summary>
    public int Agility { get; set; }

    /// <summary>Experience this one is worth when defeated. Zero for party members.</summary>
    public int Experience { get; init; }

    /// <summary>Coin this one is carrying. Zero for party members - the purse is the Inventory's.</summary>
    public int Coin { get; init; }

    /// <summary>Frame in the monster sheet. Unused by party members, who are drawn from the walker sheet.</summary>
    public int Frame { get; init; }

    /// <summary>Most monsters can show up in a group; the number here is the biggest that group gets.</summary>
    public int MaxGroup { get; init; } = 1;

    public bool IsAlive => Health > 0;

    public bool CanCast => MaxMana > 0 && Spells.Count > 0;

    /// <summary>
    /// A fresh instance at full health and mana. Monsters are defined once and
    /// fought many times, so every encounter gets its own copy rather than a
    /// shared one that stays dead after the first fight.
    /// </summary>
    public Combatant Spawn(string name = "") => new()
    {
        Name = name == "" ? Name : name,
        ClassName = ClassName,
        Level = Level,
        MaxHealth = MaxHealth,
        Health = MaxHealth,
        MaxMana = MaxMana,
        Mana = MaxMana,
        Spells = new List<string>(Spells),
        Growth = Growth,
        Attack = Attack,
        Defense = Defense,
        Agility = Agility,
        Experience = Experience,
        Coin = Coin,
        Frame = Frame,
        MaxGroup = MaxGroup
    };
}
