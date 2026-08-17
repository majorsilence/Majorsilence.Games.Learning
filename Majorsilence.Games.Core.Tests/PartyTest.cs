using Majorsilence.Games.Rpg;
using Xunit;

namespace Majorsilence.Games.Core.Tests;

/// <summary>
/// Levelling, and a check that the roster and spell book the game actually
/// ships with parse and agree with each other - a typo in a spell key is
/// otherwise a crash the first time somebody opens the magic menu.
/// </summary>
public class PartyTest
{
    private static Combatant Member(string name, int health = 20, int mana = 0, Growth? growth = null) => new()
    {
        Name = name,
        MaxHealth = health,
        Health = health,
        MaxMana = mana,
        Mana = mana,
        Attack = 5,
        Defense = 3,
        Agility = 4,
        Growth = growth ?? new Growth { Health = 5, Mana = 2, Attack = 2, Defense = 1, Agility = 1 }
    };

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 20)]
    [InlineData(3, 60)]
    [InlineData(4, 120)]
    [InlineData(5, 200)]
    public void LevelThresholdsClimbSteadily(int level, int expected)
    {
        Assert.Equal(expected, Party.ExperienceForLevel(level));
    }

    [Fact]
    public void ReachingAThresholdRaisesEveryStat()
    {
        var wren = Member("Wren");
        var party = Party.Of(wren);

        var announcements = party.AwardExperience(20);

        Assert.Equal(2, wren.Level);
        Assert.Equal(25, wren.MaxHealth);
        Assert.Equal(7, wren.Attack);
        Assert.Equal(4, wren.Defense);
        Assert.Equal(5, wren.Agility);
        Assert.Contains("Wren reaches level 2.", announcements);
    }

    [Fact]
    public void GrowthAddsToCurrentHealthAsWellAsTheMaximum()
    {
        var wren = Member("Wren");
        wren.Health = 4; // came out of that fight badly
        var party = Party.Of(wren);

        party.AwardExperience(20);

        // Levelling is not a heal, but the new capacity is real: the gain lands
        // on both, so a level-up never leaves someone at a smaller fraction of
        // their health than they had before.
        Assert.Equal(9, wren.Health);
        Assert.Equal(25, wren.MaxHealth);
    }

    [Fact]
    public void OneLargeAwardCanCarryYouSeveralLevels()
    {
        var wren = Member("Wren");
        var party = Party.Of(wren);

        var announcements = party.AwardExperience(200);

        Assert.Equal(5, wren.Level);
        Assert.Equal(4, announcements.Count);
    }

    [Fact]
    public void ExperienceIsSharedRatherThanSplit()
    {
        var wren = Member("Wren");
        var sella = Member("Sella");
        var halt = Member("Halt");
        var party = Party.Of(wren, sella, halt);

        party.AwardExperience(60);

        // A bigger party is not a slower one, and nobody falls behind.
        Assert.Equal(60, party.Experience);
        Assert.All(party.Members, m => Assert.Equal(3, m.Level));
    }

    [Fact]
    public void TheFallenStillEarnTheirShare()
    {
        var wren = Member("Wren");
        var sella = Member("Sella");
        sella.Health = 0;
        var party = Party.Of(wren, sella);

        party.AwardExperience(20);

        Assert.Equal(2, sella.Level);
    }

    [Fact]
    public void RestoreAllBringsEveryoneBack()
    {
        var wren = Member("Wren", mana: 8);
        var sella = Member("Sella", mana: 8);
        wren.Health = 0;
        sella.Health = 3;
        sella.Mana = 0;
        var party = Party.Of(wren, sella);

        party.RestoreAll();

        Assert.All(party.Members, m =>
        {
            Assert.Equal(m.MaxHealth, m.Health);
            Assert.Equal(m.MaxMana, m.Mana);
        });
        Assert.True(wren.IsAlive, "a full restore should put the fallen back on their feet");
    }

    [Fact]
    public void PartyOfNobodyIsRejected()
    {
        Assert.Throws<MajorsilenceException>(() => Party.Of());
    }

    // ------------------------------------------------- the shipped data ----

    [Fact]
    public void TheShippedSpellBookLoads()
    {
        var spells = SpellBook.Load("assets/spells.json");

        Assert.True(spells.Contains("ember"));
        Assert.Equal(SpellKind.DamageAll, spells["scour"].Kind);
        Assert.Equal(SpellKind.Heal, spells["mend"].Kind);
    }

    [Fact]
    public void TheShippedRosterLoadsAndEveryoneKnowsRealSpells()
    {
        var party = Party.Load("assets/party.json", SpellBook.Load("assets/spells.json"));

        Assert.Equal(3, party.Members.Count);
        Assert.All(party.Members, m =>
        {
            Assert.NotEqual("", m.Name);
            Assert.NotEqual("", m.ClassName);
            Assert.True(m.MaxHealth > 0, $"{m.Name} needs some health");
            Assert.Equal(1, m.Level);
        });

        // Party.Load throws on an unknown spell key, so reaching here proves
        // every spell named in the roster exists in the book.
        Assert.Contains(party.Members, m => m.CanCast);
    }

    [Fact]
    public void TheShippedMonsterBookLoads()
    {
        var monsters = MonsterBook.Load("assets/monsters.json");

        Assert.True(monsters.Contains("ash-wolf"));
        Assert.True(monsters["ash-wraith"].Experience > monsters["ash-wolf"].Experience,
            "the thing people are frightened of should be worth more than the common one");
    }

    [Fact]
    public void EveryMonsterCanBeSpawnedIntoAGroup()
    {
        var monsters = MonsterBook.Load("assets/monsters.json");
        var random = new Random(20260816);

        foreach (var key in new[] { "ash-wolf", "cinder-crow", "slagling", "ridge-bandit", "ash-wraith" })
        {
            var group = monsters.RollGroup(new[] { key }, random);

            Assert.NotEmpty(group);
            Assert.InRange(group.Count, 1, monsters[key].MaxGroup);
            Assert.All(group, m => Assert.True(m.IsAlive, "a freshly spawned monster should be at full health"));
            // Several of a kind need telling apart in the messages and the cursor.
            Assert.Equal(group.Count, group.Select(m => m.Name).Distinct().Count());
        }
    }
}
