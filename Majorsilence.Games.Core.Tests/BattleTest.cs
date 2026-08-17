using Majorsilence.Games.Rpg;
using Xunit;

namespace Majorsilence.Games.Core.Tests;

/// <summary>
/// Turn resolution, spellcasting and the damage formulas. Pure logic - no SDL.
///
/// Nothing here asserts a specific roll: hit chance is clamped short of
/// certainty, so any single swing can miss. Tests instead drive a fight to its
/// end and assert on the outcome, which is decided by the numbers rather than
/// the dice.
/// </summary>
public class BattleTest
{
    private static readonly Spell Ember = new()
        { Key = "ember", Name = "Ember", Cost = 3, Power = 11, Kind = SpellKind.Damage, Verb = "burns" };

    private static readonly Spell Scour = new()
        { Key = "scour", Name = "Scour", Cost = 7, Power = 8, Kind = SpellKind.DamageAll, Verb = "scours" };

    private static readonly Spell Mend = new()
        { Key = "mend", Name = "Mend", Cost = 3, Power = 14, Kind = SpellKind.Heal, Verb = "mends" };

    private static readonly Spell Rally = new()
        { Key = "rally", Name = "Rally", Cost = 8, Power = 9, Kind = SpellKind.HealAll, Verb = "rallies" };

    private static SpellBook Book() => SpellBook.Of(Ember, Scour, Mend, Rally);

    private static readonly Item Salve = new()
        { Key = "salve", Name = "Salve", Price = 12, Power = 25, Kind = ItemKind.Heal, Verb = "binds up" };

    private static readonly Item WakingSalt = new()
        { Key = "waking-salt", Name = "Waking Salt", Price = 60, Power = 50, Kind = ItemKind.Revive, Verb = "wakes" };

    private static ItemBook Bag() => ItemBook.Of(Salve, WakingSalt);

    private static Combatant Fighter(string name, int health, int attack, int defense, int agility,
        int experience = 0, int mana = 0, params string[] spells) =>
        new()
        {
            Name = name,
            MaxHealth = health,
            Health = health,
            MaxMana = mana,
            Mana = mana,
            Attack = attack,
            Defense = defense,
            Agility = agility,
            Experience = experience,
            Spells = spells.ToList()
        };

    private static Combatant Warden(int health = 40, int attack = 20, int defense = 6, int agility = 8) =>
        Fighter("Wren", health, attack, defense, agility);

    private static Combatant Caster(int mana = 12, int agility = 7) =>
        Fighter("Sella", 18, 4, 2, agility, mana: mana, spells: new[] { "ember", "scour" });

    private static Combatant Healer(int mana = 10, int agility = 10) =>
        Fighter("Halt", 22, 5, 3, agility, mana: mana, spells: new[] { "mend", "rally" });

    private static Battle Start(Party party, params Combatant[] monsters) =>
        new(party, monsters, Book(), Bag(), new Random(20260816));

    /// <summary>Mashes Confirm - Fight, then the target, then the messages - until the fight ends.</summary>
    private static void FightToTheEnd(Battle battle, int maxPresses = 4000)
    {
        for (var i = 0; i < maxPresses && battle.Phase != BattlePhase.Over; i++)
            battle.Confirm();
    }

    /// <summary>Presses Confirm until the battle asks for something other than a message.</summary>
    private static void ClearMessages(Battle battle, int maxPresses = 200)
    {
        for (var i = 0; i < maxPresses && battle.Phase == BattlePhase.Message; i++)
            battle.Confirm();
    }

    [Fact]
    public void OpensOnALineIntroducingTheEnemy()
    {
        var battle = Start(Party.Of(Warden()), Fighter("Ash-wolf", 16, 7, 2, 10));

        Assert.Equal(BattlePhase.Message, battle.Phase);
        Assert.Contains("Ash-wolf", battle.Message);
    }

    [Fact]
    public void OrdersAreGivenForEveryLivingMemberInTurn()
    {
        var battle = Start(Party.Of(Warden(), Caster(), Healer()), Fighter("Ash-wolf", 200, 1, 0, 1));
        ClearMessages(battle);

        Assert.Equal("Wren", battle.Planning?.Name);
        battle.Confirm();   // Fight
        battle.Confirm();   // target
        Assert.Equal("Sella", battle.Planning?.Name);
        battle.Confirm();
        battle.Confirm();
        Assert.Equal("Halt", battle.Planning?.Name);
    }

    [Fact]
    public void MagicIsOfferedOnlyToThoseWhoCanCast()
    {
        var battle = Start(Party.Of(Warden(), Caster()), Fighter("Ash-wolf", 200, 1, 0, 1));
        ClearMessages(battle);

        // Wren carries no spells at all.
        Assert.Equal(new[] { BattleCommand.Fight, BattleCommand.Run }, battle.Commands);

        battle.Confirm();   // Fight
        battle.Confirm();   // target

        Assert.Equal(new[] { BattleCommand.Fight, BattleCommand.Magic, BattleCommand.Run }, battle.Commands);
    }

    [Fact]
    public void CastingSpendsMana()
    {
        var caster = Caster(mana: 12);
        var battle = Start(Party.Of(caster), Fighter("Ash-wolf", 200, 1, 0, 1));
        ClearMessages(battle);

        battle.MoveCursor(1);                       // Fight -> Magic
        Assert.Equal(BattleCommand.Magic, battle.Command);
        battle.Confirm();                           // spell list
        Assert.Equal(BattlePhase.Spell, battle.Phase);
        battle.Confirm();                           // Ember -> pick a target
        battle.Confirm();                           // commit; the round runs
        ClearMessages(battle);

        Assert.Equal(12 - Ember.Cost, caster.Mana);
    }

    [Fact]
    public void ASpellBeyondYourMeansIsRefused()
    {
        var caster = Caster(mana: 1);
        var battle = Start(Party.Of(caster), Fighter("Ash-wolf", 200, 1, 0, 1));
        ClearMessages(battle);

        battle.MoveCursor(1);   // Magic
        battle.Confirm();       // spell list
        battle.Confirm();       // Ember, which costs more than she has

        Assert.Contains("hasn't the strength", battle.Message);
        Assert.Equal(1, caster.Mana);
    }

    [Fact]
    public void HealingRestoresAnAlly()
    {
        var warden = Warden();
        var healer = Healer();
        warden.Health = 5;

        var battle = Start(Party.Of(warden, healer), Fighter("Ash-wolf", 400, 1, 0, 1));
        ClearMessages(battle);

        battle.Confirm();       // Wren: Fight
        battle.Confirm();       // target
        battle.MoveCursor(1);   // Halt: Fight -> Magic
        battle.Confirm();       // open the spell list
        Assert.Equal(BattlePhase.Spell, battle.Phase);
        battle.Confirm();       // Mend, which is first, and asks who for
        Assert.Equal(BattlePhase.AllyTarget, battle.Phase);
        battle.Confirm();       // on Wren, who is first in the party
        ClearMessages(battle);

        Assert.True(warden.Health > 5, $"Mend should have healed Wren; health is {warden.Health}");
        Assert.Equal(10 - Mend.Cost, healer.Mana);
    }

    [Fact]
    public void ASpellThatHitsEveryoneSkipsTargetSelection()
    {
        var caster = Caster(mana: 20);
        var wolfA = Fighter("Ash-wolf A", 200, 1, 0, 1);
        var wolfB = Fighter("Ash-wolf B", 200, 1, 0, 1);
        var battle = Start(Party.Of(caster), wolfA, wolfB);
        ClearMessages(battle);

        battle.MoveCursor(1);   // Magic
        battle.Confirm();       // spell list
        battle.MoveCursor(1);   // Ember -> Scour
        Assert.Equal("Scour", battle.SelectedSpell?.Name);
        battle.Confirm();       // straight to resolution, no target to choose
        ClearMessages(battle);

        Assert.True(wolfA.Health < 200, "Scour should have caught the first wolf");
        Assert.True(wolfB.Health < 200, "Scour should have caught the second wolf");
    }

    [Fact]
    public void VictoryAwardsExperienceToTheWholeParty()
    {
        var party = Party.Of(Warden(), Caster(), Healer());
        var battle = Start(party, Fighter("Ash-wolf", 12, 1, 0, 2, experience: 40));

        FightToTheEnd(battle);

        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(40, battle.ExperienceEarned);
        Assert.Equal(40, party.Experience);
        // 40 is past the 20 needed for level 2, and everyone shares it.
        Assert.All(party.Members, m => Assert.Equal(2, m.Level));
    }

    [Fact]
    public void ExperienceIsTheWholeGroupsWorth()
    {
        var party = Party.Of(Warden());
        var battle = Start(party,
            Fighter("Ash-wolf A", 6, 1, 0, 2, experience: 5),
            Fighter("Ash-wolf B", 6, 1, 0, 2, experience: 5));

        FightToTheEnd(battle);

        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(10, battle.ExperienceEarned);
    }

    [Fact]
    public void DefeatNeedsEveryoneDown()
    {
        var party = Party.Of(Warden(health: 6, attack: 1, defense: 0, agility: 1),
            Fighter("Sella", 6, 1, 0, 1),
            Fighter("Halt", 6, 1, 0, 1));

        var battle = Start(party, Fighter("Ash-wraith", 4000, 40, 30, 30));
        FightToTheEnd(battle);

        Assert.Equal(BattleOutcome.Defeat, battle.Outcome);
        Assert.True(party.Wiped);
    }

    [Fact]
    public void RunEndsTheFight()
    {
        var battle = Start(Party.Of(Warden(agility: 40)), Fighter("Ash-wolf", 16, 7, 2, 1));

        for (var i = 0; i < 400 && battle.Outcome == BattleOutcome.None; i++)
        {
            if (battle.Phase == BattlePhase.Command)
            {
                while (battle.Command != BattleCommand.Run) battle.MoveCursor(1);
            }
            battle.Confirm();
        }

        Assert.Equal(BattleOutcome.Fled, battle.Outcome);
    }

    [Fact]
    public void CancelStepsBackThroughTheMenus()
    {
        var battle = Start(Party.Of(Caster()), Fighter("Ash-wolf", 16, 7, 2, 10));
        ClearMessages(battle);

        battle.MoveCursor(1);   // Magic
        battle.Confirm();
        Assert.Equal(BattlePhase.Spell, battle.Phase);

        battle.Confirm();       // Ember -> target
        Assert.Equal(BattlePhase.Target, battle.Phase);

        battle.Cancel();
        Assert.Equal(BattlePhase.Spell, battle.Phase);

        battle.Cancel();
        Assert.Equal(BattlePhase.Command, battle.Phase);
    }

    [Fact]
    public void CancelOnTheSecondMemberTakesBackTheFirstsOrders()
    {
        var battle = Start(Party.Of(Warden(), Caster()), Fighter("Ash-wolf", 200, 1, 0, 1));
        ClearMessages(battle);

        Assert.Equal("Wren", battle.Planning?.Name);
        battle.Confirm();   // Fight
        battle.Confirm();   // target -> Sella is next
        Assert.Equal("Sella", battle.Planning?.Name);

        battle.Cancel();
        Assert.Equal("Wren", battle.Planning?.Name);
        Assert.Equal(BattlePhase.Command, battle.Phase);
    }

    [Fact]
    public void TargetCursorSkipsTheDead()
    {
        var alive = Fighter("Ash-wolf", 16, 7, 2, 10);
        var dead = Fighter("Cinder-crow", 11, 6, 1, 13);
        var battle = Start(Party.Of(Warden()), alive, dead);
        dead.Health = 0;

        ClearMessages(battle);
        battle.Confirm();   // Fight -> Target

        for (var i = 0; i < 4; i++)
        {
            battle.MoveCursor(1);
            Assert.True(battle.Monsters[battle.TargetIndex].IsAlive,
                "the cursor should never come to rest on a defeated monster");
        }
    }

    /// <summary>
    /// The regression that matters for how a fight reads: a round is queued and
    /// played one turn at a time, so health on screen can never run ahead of the
    /// line describing it.
    /// </summary>
    [Fact]
    public void HealthOnlyChangesOnTheMessageThatExplainsIt()
    {
        var party = Party.Of(
            Fighter("Wren", 200, 4, 0, 5),
            Fighter("Sella", 200, 4, 0, 6));

        var battle = Start(party,
            Fighter("Ash-wolf", 60, 6, 0, 10),
            Fighter("Cinder-crow", 60, 6, 0, 12));

        var partyHealth = party.Members.Select(m => m.Health).ToArray();
        var monsterHealth = battle.Monsters.Select(m => m.Health).ToArray();

        for (var i = 0; i < 800 && battle.Phase != BattlePhase.Over; i++)
        {
            battle.Confirm();

            for (var m = 0; m < party.Members.Count; m++)
            {
                if (party.Members[m].Health == partyHealth[m]) continue;
                Assert.Contains("lunges", battle.Message);
                partyHealth[m] = party.Members[m].Health;
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
        var battle = Start(Party.Of(Warden()), Fighter("Slagling", 22, 8, 6, 4));
        var feeble = Fighter("Feeble", 10, 1, 0, 1);
        var armoured = Fighter("Armoured", 10, 1, 200, 1);

        for (var i = 0; i < 200; i++)
            Assert.True(battle.DamageFrom(feeble, armoured) >= 1);
    }

    [Theory]
    [InlineData(40, 1)]
    [InlineData(1, 40)]
    [InlineData(8, 8)]
    public void HitChanceStaysShortOfCertaintyBothWays(int attackerAgility, int defenderAgility)
    {
        var attacker = Fighter("a", 10, 5, 0, attackerAgility);
        var defender = Fighter("d", 10, 5, 0, defenderAgility);

        Assert.InRange(Battle.HitChance(attacker, defender), 0.55, 0.97);
    }

    // ----------------------------------------------------------- items ----

    [Fact]
    public void ItemIsOfferedOnlyWhenTheBagHasSomething()
    {
        var empty = Start(Party.Of(Warden()), Fighter("Ash-wolf", 200, 1, 0, 1));
        ClearMessages(empty);
        Assert.DoesNotContain(BattleCommand.Item, empty.Commands);

        var party = Party.Of(Warden());
        party.Bag.Add("salve");
        var stocked = Start(party, Fighter("Ash-wolf", 200, 1, 0, 1));
        ClearMessages(stocked);
        Assert.Contains(BattleCommand.Item, stocked.Commands);
    }

    [Fact]
    public void UsingAnItemSpendsItAndHeals()
    {
        var warden = Warden();
        warden.Health = 4;
        var party = Party.Of(warden);
        party.Bag.Add("salve", 2);

        var battle = Start(party, Fighter("Ash-wolf", 400, 1, 0, 1));
        ClearMessages(battle);

        battle.MoveCursor(1);   // Fight -> Item
        Assert.Equal(BattleCommand.Item, battle.Command);
        battle.Confirm();       // open the bag
        Assert.Equal(BattlePhase.Item, battle.Phase);
        battle.Confirm();       // Salve, which asks who for
        Assert.Equal(BattlePhase.AllyTarget, battle.Phase);
        battle.Confirm();       // on Wren
        ClearMessages(battle);

        Assert.True(warden.Health > 4, $"the salve should have healed Wren; health is {warden.Health}");
        Assert.Equal(1, party.Bag.CountOf("salve"));
    }

    /// <summary>
    /// An item leaves the bag when the order is given, so two characters cannot
    /// both be told to use the last salve. Taking that order back has to put it
    /// straight back, or cancelling would quietly destroy it.
    /// </summary>
    [Fact]
    public void TakingBackAnOrderReturnsItsItem()
    {
        var party = Party.Of(Warden(), Caster());
        party.Bag.Add("salve");

        var battle = Start(party, Fighter("Ash-wolf", 400, 1, 0, 1));
        ClearMessages(battle);

        battle.MoveCursor(1);   // Wren: Fight -> Item
        battle.Confirm();       // open the bag
        battle.Confirm();       // Salve
        battle.Confirm();       // on Wren; the order is given, so it leaves the bag
        Assert.Equal(0, party.Bag.CountOf("salve"));
        Assert.Equal("Sella", battle.Planning?.Name);

        battle.Cancel();        // take Wren's order back
        Assert.Equal("Wren", battle.Planning?.Name);
        Assert.Equal(1, party.Bag.CountOf("salve"));
    }

    [Fact]
    public void AReviveBringsBackTheFallen()
    {
        var warden = Warden();
        var caster = Caster();
        caster.Health = 0;

        var party = Party.Of(warden, caster);
        party.Bag.Add("waking-salt");

        var battle = Start(party, Fighter("Ash-wolf", 400, 1, 0, 1));
        ClearMessages(battle);

        battle.MoveCursor(1);   // Wren: Fight -> Item
        battle.Confirm();       // open the bag
        battle.Confirm();       // Waking Salt; its cursor starts on somebody down
        Assert.Equal(BattlePhase.AllyTarget, battle.Phase);
        Assert.False(battle.Party[battle.AllyIndex].IsAlive,
            "a revive should open pointed at somebody who actually needs it");
        battle.Confirm();
        ClearMessages(battle);

        Assert.True(caster.IsAlive, "the waking salt should have brought Sella round");
    }

    [Fact]
    public void VictoryAwardsCoinAsWellAsExperience()
    {
        var party = Party.Of(Warden());
        var battle = Start(party, Fighter("Ash-wolf", 8, 1, 0, 2, experience: 5));

        FightToTheEnd(battle);

        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(0, battle.CoinEarned); // this one carried none
        Assert.Equal(0, party.Bag.Coin);
    }

    [Fact]
    public void CoinFromTheFallenReachesThePurse()
    {
        var party = Party.Of(Warden());
        var bandit = new Combatant
        {
            Name = "Ridge bandit", MaxHealth = 8, Health = 8,
            Attack = 1, Defense = 0, Agility = 2, Experience = 9, Coin = 14
        };

        var battle = Start(party, bandit);
        FightToTheEnd(battle);

        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(14, battle.CoinEarned);
        Assert.Equal(14, party.Bag.Coin);
    }
}
