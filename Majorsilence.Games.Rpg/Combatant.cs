namespace Majorsilence.Games.Rpg;

/// <summary>
/// Anything that can take a turn in a battle - the hero and every monster alike.
///
/// Deliberately a plain mutable bag of numbers with no behaviour: who hits whom
/// for how much lives in <see cref="Battle"/>, so the formulas are in one place
/// and can be read (and tested) without chasing methods across classes.
/// </summary>
public class Combatant
{
    public required string Name { get; init; }

    public required int MaxHealth { get; init; }
    public int Health { get; set; }

    /// <summary>Drives damage dealt.</summary>
    public int Attack { get; init; }

    /// <summary>Halves incoming attack power (see Battle.DamageFrom).</summary>
    public int Defense { get; init; }

    /// <summary>Turn order, hit chance and escape chance.</summary>
    public int Agility { get; init; }

    /// <summary>Experience this one is worth when defeated. Zero for the hero.</summary>
    public int Experience { get; init; }

    /// <summary>Frame in the monster sheet. Unused by the hero, who is drawn from the walker sheet.</summary>
    public int Frame { get; init; }

    /// <summary>Most monsters can show up in a group; the number here is the biggest that group gets.</summary>
    public int MaxGroup { get; init; } = 1;

    public bool IsAlive => Health > 0;

    /// <summary>
    /// A fresh instance with full health. Monsters are defined once and fought
    /// many times, so every encounter gets its own copy rather than a shared one
    /// that stays dead after the first fight.
    /// </summary>
    public Combatant Spawn(string name = "") => new()
    {
        Name = name == "" ? Name : name,
        MaxHealth = MaxHealth,
        Health = MaxHealth,
        Attack = Attack,
        Defense = Defense,
        Agility = Agility,
        Experience = Experience,
        Frame = Frame,
        MaxGroup = MaxGroup
    };
}
