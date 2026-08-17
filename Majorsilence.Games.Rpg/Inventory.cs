namespace Majorsilence.Games.Rpg;

/// <summary>
/// The party's bag and purse. One of each, shared - there is no per-character
/// carrying in a game this size, and splitting a stack of salves three ways
/// would be book-keeping without a decision in it.
///
/// Counts are kept rather than a list of instances: items here are
/// interchangeable, so "three salves" is the whole truth about them.
/// </summary>
public class Inventory
{
    private readonly Dictionary<string, int> _counts = new();

    /// <summary>How much coin the party is carrying.</summary>
    public int Coin { get; private set; }

    /// <summary>Item keys currently held, in the order they were first acquired - so the battle menu doesn't reshuffle itself between fights.</summary>
    public IReadOnlyList<string> Keys => _order;

    private readonly List<string> _order = new();

    public int CountOf(string key) => _counts.GetValueOrDefault(key);

    public bool Has(string key) => CountOf(key) > 0;

    public bool Any => _order.Count > 0;

    public void Add(string key, int count = 1)
    {
        if (count <= 0) return;
        if (!_counts.ContainsKey(key)) _order.Add(key);
        _counts[key] = CountOf(key) + count;
    }

    /// <summary>Consumes one, returning false if there wasn't one to consume.</summary>
    public bool Remove(string key)
    {
        if (!Has(key)) return false;

        var left = _counts[key] - 1;
        if (left == 0)
        {
            _counts.Remove(key);
            _order.Remove(key);
        }
        else
        {
            _counts[key] = left;
        }

        return true;
    }

    public void EarnCoin(int amount) => Coin += Math.Max(0, amount);

    /// <summary>Spends coin if there is enough, returning false and spending nothing if there isn't.</summary>
    public bool SpendCoin(int amount)
    {
        if (amount < 0 || Coin < amount) return false;
        Coin -= amount;
        return true;
    }
}
