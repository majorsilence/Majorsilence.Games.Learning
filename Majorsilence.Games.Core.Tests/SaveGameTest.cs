using Majorsilence.Games.Rpg;
using Xunit;

namespace Majorsilence.Games.Core.Tests;

/// <summary>
/// Writing a game to disk and getting it back. Every test works in its own
/// temporary directory, so none of them can touch a real save or each other.
/// </summary>
public class SaveGameTest : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "vale-of-ash-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static SaveGame SampleSave() => new()
    {
        MapPath = "assets/levels/valeroad.json",
        Column = 12,
        Row = 7,
        Experience = 140,
        Coin = 63,
        Bag = new Dictionary<string, int> { ["salve"] = 3, ["tonic"] = 1 },
        Members = new List<SavedMember>
        {
            new() { Name = "Wren", Level = 4, Health = 20, MaxHealth = 43, Attack = 15, Defense = 7, Agility = 11 },
            new() { Name = "Sella", Level = 4, Health = 27, MaxHealth = 27, Mana = 8, MaxMana = 24, Attack = 8, Defense = 5, Agility = 10 }
        }
    };

    private static Combatant Member(string name, int health = 20, int mana = 8) => new()
    {
        Name = name,
        MaxHealth = health,
        Health = health,
        MaxMana = mana,
        Mana = mana,
        Attack = 5,
        Defense = 3,
        Agility = 4
    };

    [Fact]
    public void ASaveSurvivesTheRoundTrip()
    {
        SampleSave().Save(_directory);
        var loaded = SaveGame.Load(_directory);

        Assert.NotNull(loaded);
        Assert.Equal("assets/levels/valeroad.json", loaded!.MapPath);
        Assert.Equal(12, loaded.Column);
        Assert.Equal(7, loaded.Row);
        Assert.Equal(140, loaded.Experience);
        Assert.Equal(63, loaded.Coin);
        Assert.Equal(3, loaded.Bag["salve"]);
        Assert.Equal(2, loaded.Members.Count);

        var wren = loaded.Members[0];
        Assert.Equal("Wren", wren.Name);
        Assert.Equal(4, wren.Level);
        Assert.Equal(20, wren.Health);
        Assert.Equal(43, wren.MaxHealth);
        Assert.Equal(15, wren.Attack);
    }

    [Fact]
    public void SavingStampsTheTime()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        SampleSave().Save(_directory);

        Assert.True(SaveGame.Load(_directory)!.UpdatedUtc > before);
    }

    [Fact]
    public void NoSaveMeansNoGameToResume()
    {
        Assert.False(SaveGame.Exists(_directory));
        Assert.Null(SaveGame.Load(_directory));
    }

    [Fact]
    public void AnUnreadableSaveStartsAFreshGameRatherThanFailing()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SaveGame.PathIn(_directory), "{ this is not json");

        // A corrupt save must not stop the player getting into the game.
        Assert.Null(SaveGame.Load(_directory));
    }

    [Fact]
    public void ASaveWithNowhereToStandIsNoSave()
    {
        new SaveGame { MapPath = "" }.Save(_directory);

        Assert.Null(SaveGame.Load(_directory));
    }

    [Fact]
    public void DeleteRemovesIt()
    {
        SampleSave().Save(_directory);
        Assert.True(SaveGame.Exists(_directory));

        SaveGame.Delete(_directory);

        Assert.False(SaveGame.Exists(_directory));
        Assert.Null(SaveGame.Load(_directory));
    }

    [Fact]
    public void DeletingNothingIsHarmless()
    {
        SaveGame.Delete(_directory);
    }

    // ------------------------------------------------- putting it back ----

    [Fact]
    public void RestoreMatchesMembersByName()
    {
        var wren = Member("Wren");
        var sella = Member("Sella");
        var party = Party.Of(wren, sella);

        // Deliberately in the other order: a save must not depend on the roster
        // file keeping its ordering.
        party.Restore(140, new[]
        {
            new SavedMember { Name = "Sella", Level = 3, Health = 9, MaxHealth = 26, Mana = 4, MaxMana = 18, Attack = 7, Defense = 4, Agility = 9 },
            new SavedMember { Name = "Wren", Level = 4, Health = 20, MaxHealth = 43, Attack = 15, Defense = 7, Agility = 11 }
        });

        Assert.Equal(140, party.Experience);
        Assert.Equal(4, wren.Level);
        Assert.Equal(43, wren.MaxHealth);
        Assert.Equal(15, wren.Attack);
        Assert.Equal(3, sella.Level);
        Assert.Equal(4, sella.Mana);
    }

    [Fact]
    public void SomebodyTheSaveNeverHeardOfKeepsTheirStartingNumbers()
    {
        var wren = Member("Wren");
        var newcomer = Member("Halt", health: 22);
        var party = Party.Of(wren, newcomer);

        party.Restore(20, new[]
        {
            new SavedMember { Name = "Wren", Level = 2, Health = 5, MaxHealth = 33, Attack = 11, Defense = 5, Agility = 9 }
        });

        // An older save loading into a roster that has since gained somebody
        // should bring them along, not drop them.
        Assert.Equal(2, wren.Level);
        Assert.Equal(1, newcomer.Level);
        Assert.Equal(22, newcomer.MaxHealth);
    }

    [Fact]
    public void RestoreNeverPutsHealthAboveItsMaximum()
    {
        var wren = Member("Wren");
        var party = Party.Of(wren);

        // A hand-edited or stale save shouldn't be able to make somebody
        // permanently over-full.
        party.Restore(0, new[]
        {
            new SavedMember { Name = "Wren", Level = 1, Health = 9999, MaxHealth = 30, Mana = 9999, MaxMana = 5, Attack = 5, Defense = 3, Agility = 4 }
        });

        Assert.Equal(30, wren.Health);
        Assert.Equal(5, wren.Mana);
    }

    [Fact]
    public void ClearingTheBagEmptiesTheCoinToo()
    {
        var bag = new Inventory();
        bag.Add("salve", 3);
        bag.EarnCoin(50);

        bag.Clear();

        Assert.False(bag.Any);
        Assert.Equal(0, bag.Coin);
        Assert.Equal(0, bag.CountOf("salve"));
    }
}
