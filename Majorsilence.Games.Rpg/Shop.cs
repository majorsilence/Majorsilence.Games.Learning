namespace Majorsilence.Games.Rpg;

public enum ShopKind
{
    /// <summary>A counter with things on it.</summary>
    Goods,

    /// <summary>A bed: one price, and everyone gets up whole.</summary>
    Inn
}

public enum ShopPhase
{
    /// <summary>Cursor is on the list.</summary>
    Browsing,

    /// <summary>Showing a line - what was bought, or why it wasn't.</summary>
    Message,

    /// <summary>Done; the caller should put the map back.</summary>
    Closed
}

/// <summary>
/// A transaction with somebody who wants paying: the store counter and the inn
/// bed are the same shape, so they are the same class with a different list.
///
/// Pure logic - no SDL, no input - so what a purchase costs and what it leaves
/// you with is testable without opening a window. ShopScreen draws it; RpgGame
/// decides when one opens.
/// </summary>
public class Shop
{
    private readonly ItemBook _items;
    private readonly Party _party;

    public Shop(string keeper, ShopKind kind, IEnumerable<string> stock, int restPrice, ItemBook items, Party party)
    {
        Keeper = keeper;
        Kind = kind;
        Stock = stock.ToList();
        RestPrice = restPrice;
        _items = items;
        _party = party;

        Message = kind == ShopKind.Inn
            ? $"A bed is {restPrice} coin, and you'll wake whole."
            : "Have a look, then.";
        Phase = ShopPhase.Message;
    }

    public string Keeper { get; }
    public ShopKind Kind { get; }

    /// <summary>Item keys on offer. Empty at an inn, which sells one thing that isn't an item.</summary>
    public IReadOnlyList<string> Stock { get; }

    public int RestPrice { get; }

    public ShopPhase Phase { get; private set; }
    public string Message { get; private set; }
    public int Index { get; private set; }

    public int Coin => _party.Bag.Coin;

    /// <summary>
    /// Set the moment a bed is actually taken. The inn is where a console RPG
    /// saves, so the caller watches this to know when to write to disk - and
    /// clears it once it has.
    /// </summary>
    public bool Rested { get; set; }

    /// <summary>The item under the cursor, or null at an inn.</summary>
    public Item? Selected =>
        Stock.Count > 0 ? _items[Stock[Math.Clamp(Index, 0, Stock.Count - 1)]] : null;

    /// <summary>What one line of the list costs.</summary>
    public int PriceOf(string key) => _items[key].Price;

    /// <summary>The printed name for one line of the list.</summary>
    public string NameOf(string key) => _items[key].Name;

    public void MoveCursor(int delta)
    {
        if (Phase != ShopPhase.Browsing || Stock.Count == 0 || delta == 0) return;
        Index = (Index + Math.Sign(delta) + Stock.Count) % Stock.Count;
    }

    public void Confirm()
    {
        switch (Phase)
        {
            case ShopPhase.Message:
                Phase = ShopPhase.Browsing;
                break;

            case ShopPhase.Browsing when Kind == ShopKind.Inn:
                Rest();
                break;

            case ShopPhase.Browsing:
                Buy();
                break;
        }
    }

    /// <summary>Backs out - from a message to the list, and from the list out of the shop entirely.</summary>
    public void Cancel()
    {
        Phase = Phase == ShopPhase.Message ? ShopPhase.Browsing : ShopPhase.Closed;
    }

    private void Buy()
    {
        if (Selected is not { } item) return;

        if (!_party.Bag.SpendCoin(item.Price))
        {
            Say($"That's {item.Price}. You have {Coin}.");
            return;
        }

        _party.Bag.Add(item.Key);
        Say($"One {item.Name}. {Coin} coin left.");
    }

    private void Rest()
    {
        // Turning up already whole and paying for it anyway is a mistake the
        // shop can save you from making.
        if (_party.Members.All(m => m.Health == m.MaxHealth && m.Mana == m.MaxMana))
        {
            Say("You've no need of a bed yet.");
            return;
        }

        if (!_party.Bag.SpendCoin(RestPrice))
        {
            Say($"It's {RestPrice} for the night. You have {Coin}.");
            return;
        }

        _party.RestoreAll();
        Rested = true;
        Say("You sleep, and wake to the shutters open. Everyone is whole.");
    }

    private void Say(string line)
    {
        Message = line;
        Phase = ShopPhase.Message;
    }
}
