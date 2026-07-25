using System.Collections.Generic;

namespace Majorsilence.Games.Learning;

public enum ShopItemKind
{
    LifeJacket,
    DeckBoots,
    Blanket,
    FlareGun,
    PocketWatch,
    TixLauncher,
    Snack,
    Tnt
}

public enum SnackKind
{
    Candy,
    Timbits,
    Donut,
    Butter,
    Bread
}

public enum TntSize
{
    Small,
    Medium,
    Large
}

/// <summary>A snack's effect when eaten: a temporary speed multiplier that lasts DurationSeconds.</summary>
public record SnackStats(SnackKind Kind, float SpeedMultiplier, float DurationSeconds);

public record ShopItem(string Name, int Price, ShopItemKind Kind, SnackStats? Snack = null, TntSize? Tnt = null);

/// <summary>
/// Everything the purser sells, in menu order. Prices are balanced against the
/// session income budget (300 starting tix + roughly 830 collectible + escape
/// bonuses): a solo player who loots thoroughly can afford a survival kit
/// (jacket, boots, a blanket or two, the watch) but not everything - the
/// launcher and Large TNT are deliberate luxuries. Availability rules that
/// depend on live game state (pocket watch before the collision only, one
/// flare gun per voyage) live in Game.VisibleShopItems, not here.
/// </summary>
public static class ShopCatalog
{
    public static readonly IReadOnlyList<ShopItem> Items = new[]
    {
        new ShopItem("Life Jacket", 250, ShopItemKind.LifeJacket),
        new ShopItem("Deck Boots", 200, ShopItemKind.DeckBoots),
        new ShopItem("Blanket", 75, ShopItemKind.Blanket),
        new ShopItem("Flare Gun", 300, ShopItemKind.FlareGun),
        new ShopItem("Pocket Watch", 150, ShopItemKind.PocketWatch),
        new ShopItem("Tix Launcher", 600, ShopItemKind.TixLauncher),
        new ShopItem("Small TNT", 60, ShopItemKind.Tnt, Tnt: TntSize.Small),
        new ShopItem("Medium TNT", 150, ShopItemKind.Tnt, Tnt: TntSize.Medium),
        new ShopItem("Large TNT", 300, ShopItemKind.Tnt, Tnt: TntSize.Large),
        new ShopItem("Candy", 10, ShopItemKind.Snack, new SnackStats(SnackKind.Candy, 1.20f, 4f)),
        new ShopItem("Timbits", 15, ShopItemKind.Snack, new SnackStats(SnackKind.Timbits, 1.25f, 5f)),
        new ShopItem("Donut", 20, ShopItemKind.Snack, new SnackStats(SnackKind.Donut, 1.30f, 5f)),
        new ShopItem("Butter", 30, ShopItemKind.Snack, new SnackStats(SnackKind.Butter, 1.40f, 4f)),
        new ShopItem("Bread", 40, ShopItemKind.Snack, new SnackStats(SnackKind.Bread, 1.35f, 8f)),
    };
}
