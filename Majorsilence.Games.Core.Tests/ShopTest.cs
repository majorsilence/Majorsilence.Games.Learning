using Majorsilence.Games.Rpg;
using Xunit;

namespace Majorsilence.Games.Core.Tests;

/// <summary>
/// The bag, the purse, and the two things you can spend coin on. Pure logic -
/// no SDL.
/// </summary>
public class ShopTest
{
    private static readonly Item Salve = new()
        { Key = "salve", Name = "Salve", Price = 12, Power = 25, Kind = ItemKind.Heal, Verb = "binds up" };

    private static readonly Item Tonic = new()
        { Key = "tonic", Name = "Tonic", Price = 20, Power = 10, Kind = ItemKind.Mana, Verb = "clears the head of" };

    private static ItemBook Book() => ItemBook.Of(Salve, Tonic);

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

    // ------------------------------------------------------- inventory ----

    [Fact]
    public void AddingAndSpendingItemsKeepsCount()
    {
        var bag = new Inventory();

        bag.Add("salve", 2);
        Assert.Equal(2, bag.CountOf("salve"));
        Assert.True(bag.Has("salve"));

        Assert.True(bag.Remove("salve"));
        Assert.Equal(1, bag.CountOf("salve"));

        Assert.True(bag.Remove("salve"));
        Assert.False(bag.Has("salve"));
        Assert.False(bag.Any);
    }

    [Fact]
    public void RemovingWhatYouHaventGotFails()
    {
        var bag = new Inventory();
        Assert.False(bag.Remove("salve"));
    }

    [Fact]
    public void TheLastOfSomethingLeavesTheList()
    {
        var bag = new Inventory();
        bag.Add("salve");
        bag.Add("tonic");

        bag.Remove("salve");

        // The menu is built from Keys, so a spent item must not leave a gap
        // reading "Salve x0".
        Assert.Equal(new[] { "tonic" }, bag.Keys);
    }

    [Fact]
    public void CoinCannotBeOverspent()
    {
        var bag = new Inventory();
        bag.EarnCoin(10);

        Assert.False(bag.SpendCoin(11));
        Assert.Equal(10, bag.Coin);

        Assert.True(bag.SpendCoin(10));
        Assert.Equal(0, bag.Coin);
    }

    // ------------------------------------------------------------ shop ----

    private static Shop Counter(Party party, params string[] stock) =>
        new("Quartermaster Rhen", ShopKind.Goods, stock, 0, Book(), party);

    private static Shop Bed(Party party, int price = 8) =>
        new("Innkeeper Sorrel", ShopKind.Inn, Array.Empty<string>(), price, Book(), party);

    [Fact]
    public void AShopOpensOnAGreeting()
    {
        var shop = Counter(Party.Of(Member("Wren")), "salve");

        Assert.Equal(ShopPhase.Message, shop.Phase);
        shop.Confirm();
        Assert.Equal(ShopPhase.Browsing, shop.Phase);
    }

    [Fact]
    public void BuyingSpendsCoinAndFillsTheBag()
    {
        var party = Party.Of(Member("Wren"));
        party.Bag.EarnCoin(30);
        var shop = Counter(party, "salve", "tonic");

        shop.Confirm();     // past the greeting
        shop.Confirm();     // buy the salve

        Assert.Equal(1, party.Bag.CountOf("salve"));
        Assert.Equal(18, party.Bag.Coin);
        Assert.Contains("Salve", shop.Message);
    }

    [Fact]
    public void WhatYouCannotAffordIsRefusedWithoutCharging()
    {
        var party = Party.Of(Member("Wren"));
        party.Bag.EarnCoin(5);
        var shop = Counter(party, "salve");

        shop.Confirm();     // past the greeting
        shop.Confirm();     // try to buy

        Assert.Equal(0, party.Bag.CountOf("salve"));
        Assert.Equal(5, party.Bag.Coin);
        Assert.Contains("You have 5", shop.Message);
    }

    [Fact]
    public void TheCursorWrapsAroundTheStock()
    {
        var shop = Counter(Party.Of(Member("Wren")), "salve", "tonic");
        shop.Confirm();     // past the greeting

        Assert.Equal("Salve", shop.Selected?.Name);
        shop.MoveCursor(1);
        Assert.Equal("Tonic", shop.Selected?.Name);
        shop.MoveCursor(1);
        Assert.Equal("Salve", shop.Selected?.Name);
    }

    [Fact]
    public void CancelBacksOutOfAMessageThenOutOfTheShop()
    {
        var shop = Counter(Party.Of(Member("Wren")), "salve");

        shop.Cancel();
        Assert.Equal(ShopPhase.Browsing, shop.Phase);

        shop.Cancel();
        Assert.Equal(ShopPhase.Closed, shop.Phase);
    }

    [Fact]
    public void ABedRestoresEveryoneAndCharges()
    {
        var wren = Member("Wren");
        var sella = Member("Sella");
        wren.Health = 2;
        sella.Health = 0;
        sella.Mana = 0;

        var party = Party.Of(wren, sella);
        party.Bag.EarnCoin(20);
        var inn = Bed(party);

        inn.Confirm();      // past the greeting
        inn.Confirm();      // take the bed

        Assert.Equal(12, party.Bag.Coin);
        Assert.All(party.Members, m =>
        {
            Assert.Equal(m.MaxHealth, m.Health);
            Assert.Equal(m.MaxMana, m.Mana);
        });
        Assert.True(sella.IsAlive, "a night at the inn should put the fallen back on their feet");
    }

    [Fact]
    public void ABedYouCannotAffordIsRefused()
    {
        var wren = Member("Wren");
        wren.Health = 2;
        var party = Party.Of(wren);
        party.Bag.EarnCoin(3);
        var inn = Bed(party);

        inn.Confirm();
        inn.Confirm();

        Assert.Equal(3, party.Bag.Coin);
        Assert.Equal(2, wren.Health);
    }

    [Fact]
    public void APartyInGoodHealthIsTurnedAwayRatherThanCharged()
    {
        var party = Party.Of(Member("Wren"));
        party.Bag.EarnCoin(20);
        var inn = Bed(party);

        inn.Confirm();
        inn.Confirm();

        // Paying for a bed you don't need is a mistake worth saving someone from.
        Assert.Equal(20, party.Bag.Coin);
        Assert.Contains("no need", inn.Message);
    }

    [Fact]
    public void TheShippedItemBookLoads()
    {
        var items = ItemBook.Load("assets/items.json");

        Assert.True(items.Contains("salve"));
        Assert.Equal(ItemKind.Revive, items["waking-salt"].Kind);
        Assert.False(items["cordial"].NeedsTarget, "a flask passed round has nobody in particular to aim at");
        Assert.True(items["waking-salt"].TargetsTheFallen);
    }
}
