using Majorsilence.Games.Rpg;
using Xunit;

namespace Majorsilence.Games.Core.Tests;

/// <summary>
/// Turn resolution and the damage formulas. Pure logic - no SDL required.
///
/// Nothing here asserts a specific roll: hit chance is clamped short of
/// certainty, so any single swing can miss. Tests instead drive a fight to its
/// end and assert on the outcome, which is decided by the numbers rather than
/// the dice.
/// </summary>
public class BattleTest
{
    private static Combatant Fighter(string name, int health, int attack, int defense, int agility, int experience = 0) =>
        new()
        {
            Name = name,
            MaxHealth = health,
            Health = health,
            Attack = attack,
            Defense = defense,
            Agility = agility,
            Experience = experience
        };

    private static Combatant Hero(int health = 40, int attack = 20, int defense = 6, int agility = 8) =>
        Fighter("Wren", health, attack, defense, agility);

    private static Battle Start(Combatant hero, params Combatant[] monsters) =>
        new(hero, monsters, new Random(20260816));

    /// <summary>Mashes Confirm - which is Fight, then the target, then the messages - until the fight ends.</summary>
    private static void FightToTheEnd(Battle battle, int maxPresses = 2000)
    {
        for (var i = 0; i < maxPresses && battle.Phase != BattlePhase.Over; i++)
            battle.Confirm();
    }

    [Fact]
    public void OpensOnALineIntroducingTheEnemy()
    {
        var battle = Start(Hero(), Fighter("Ash-wolf", 16, 7, 2, 10));

        // Not a blank panel waiting for a button - the opening line is already up.
        Assert.Equal(BattlePhase.Message, battle.Phase);
        Assert.Contains("Ash-wolf", battle.Message);
    }

    [Fact]
    public void OverwhelmingHeroWins()
    {
        var battle = Start(Hero(), Fighter("Ash-wolf", 16, 1, 0, 4, experience: 5));

        FightToTheEnd(battle);

        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(5, battle.ExperienceEarned);
        Assert.All(battle.Monsters, m => Assert.False(m.IsAlive));
    }

    [Fact]
    public void ExperienceIsTheWholeGroupsWorth()
    {
        var battle = Start(Hero(),
            Fighter("Ash-wolf A", 6, 1, 0, 2, experience: 5),
            Fighter("Ash-wolf B", 6, 1, 0, 2, experience: 5));

        FightToTheEnd(battle);

        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(10, battle.ExperienceEarned);
    }

    [Fact]
    public void OutmatchedHeroLoses()
    {
        var battle = Start(Hero(health: 6, attack: 1, defense: 0, agility: 1),
            Fighter("Ash-wraith", 400, 30, 20, 20));

        FightToTheEnd(battle);

        Assert.Equal(BattleOutcome.Defeat, battle.Outcome);
        Assert.False(battle.Hero.IsAlive);
        Assert.Equal(0, battle.Hero.Health);
    }

    [Fact]
    public void RunEndsTheFight()
    {
        var battle = Start(Hero(agility: 40), Fighter("Ash-wolf", 16, 7, 2, 1));

        // Off the opening message, switch Fight -> Run and commit. Escape can
        // fail, so keep trying; with this agility gap it will not fail forever.
        for (var i = 0; i < 200 && battle.Outcome == BattleOutcome.None; i++)
        {
            if (battle.Phase == BattlePhase.Command)
            {
                battle.MoveCommand(1);
                Assert.Equal(BattleCommand.Run, battle.Command);
            }
            battle.Confirm();
        }

        Assert.Equal(BattleOutcome.Fled, battle.Outcome);
    }

    [Fact]
    public void CancelBacksOutOfTargetSelection()
    {
        var battle = Start(Hero(), Fighter("Ash-wolf", 16, 7, 2, 10));
        battle.Confirm();                       // dismiss the opening line
        Assert.Equal(BattlePhase.Command, battle.Phase);

        battle.Confirm();                       // Fight -> choose a target
        Assert.Equal(BattlePhase.Target, battle.Phase);

        battle.Cancel();
        Assert.Equal(BattlePhase.Command, battle.Phase);
    }

    [Fact]
    public void TargetCursorSkipsTheDead()
    {
        var alive = Fighter("Ash-wolf", 16, 7, 2, 10);
        var dead = Fighter("Cinder-crow", 11, 6, 1, 13);
        var battle = Start(Hero(), alive, dead);
        dead.Health = 0;

        battle.Confirm();   // opening line
        battle.Confirm();   // Fight -> Target

        for (var i = 0; i < 4; i++)
        {
            battle.MoveTarget(1);
            Assert.True(battle.Monsters[battle.TargetIndex].IsAlive,
                "the cursor should never come to rest on a defeated monster");
        }
    }

    [Fact]
    public void ADeadMonsterTakesNoFurtherTurns()
    {
        var battle = Start(Hero(), Fighter("Ash-wolf", 1, 99, 0, 1));

        FightToTheEnd(battle);

        // The wolf hits hard enough to end this in one blow, but it is slower
        // than the hero and dies first - so it must never get to swing.
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(battle.Hero.MaxHealth, battle.Hero.Health);
    }

    /// <summary>
    /// The regression that matters for how a fight reads: a round is queued and
    /// played one turn at a time, so health on screen can never run ahead of the
    /// line describing it. Resolving the whole round up front showed a monster
    /// already at zero while its attack was still being narrated.
    /// </summary>
    [Fact]
    public void HealthOnlyChangesOnTheMessageThatExplainsIt()
    {
        var battle = Start(Hero(health: 200, attack: 4, defense: 0, agility: 5),
            Fighter("Ash-wolf", 60, 6, 0, 10),
            Fighter("Cinder-crow", 60, 6, 0, 12));

        var heroHealth = battle.Hero.Health;
        var monsterHealth = battle.Monsters.Select(m => m.Health).ToArray();

        for (var i = 0; i < 400 && battle.Phase != BattlePhase.Over; i++)
        {
            battle.Confirm();

            if (battle.Hero.Health != heroHealth)
            {
                Assert.Contains("lunges", battle.Message);
                heroHealth = battle.Hero.Health;
            }

            for (var m = 0; m < battle.Monsters.Count; m++)
            {
                if (battle.Monsters[m].Health == monsterHealth[m]) continue;
                Assert.Contains("strikes", battle.Message);
                monsterHealth[m] = battle.Monsters[m].Health;
            }
        }
    }

    [Fact]
    public void DamageIsNeverLessThanOne()
    {
        var battle = Start(Hero(), Fighter("Slagling", 22, 8, 6, 4));
        var feeble = Fighter("Feeble", 10, 1, 0, 1);
        var armoured = Fighter("Armoured", 10, 1, 200, 1);

        // However badly matched, an attack still makes progress - otherwise a
        // fight could deadlock with neither side able to hurt the other.
        for (var i = 0; i < 200; i++)
            Assert.True(battle.DamageFrom(feeble, armoured) >= 1);
    }

    [Theory]
    [InlineData(40, 1)]   // hugely faster
    [InlineData(1, 40)]   // hugely slower
    [InlineData(8, 8)]    // evenly matched
    public void HitChanceStaysShortOfCertaintyBothWays(int attackerAgility, int defenderAgility)
    {
        var attacker = Fighter("a", 10, 5, 0, attackerAgility);
        var defender = Fighter("d", 10, 5, 0, defenderAgility);

        var chance = Battle.HitChance(attacker, defender);

        Assert.InRange(chance, 0.55, 0.97);
    }
}
