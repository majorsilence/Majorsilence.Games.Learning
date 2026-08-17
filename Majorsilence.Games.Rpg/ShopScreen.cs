using Majorsilence.Games.Core;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;
using SDL3;

namespace Majorsilence.Games.Rpg;

/// <summary>
/// Draws a <see cref="Shop"/> over the map: who is serving, what the party is
/// carrying, and either the list on offer or the line the keeper just said.
///
/// The map stays visible behind it - unlike a battle, you have not gone
/// anywhere, you are standing at a counter.
///
/// Presentation only. It reads the shop and never changes it.
/// </summary>
public class ShopScreen : GameObject
{
    private const int Margin = 8;
    private const int PanelHeight = 62;
    private const int PurseHeight = 22;
    private const int PaddingX = 10;
    private const int PaddingY = 6;
    private const int LineHeight = 14;
    private const int FontSize = 11;

    private static readonly SDL.Color TextColor = new() { A = 0, R = 240, G = 240, B = 232 };
    private static readonly SDL.Color CursorColor = new() { A = 0, R = 248, G = 216, B = 120 };
    private static readonly SDL.Color DearColor = new() { A = 0, R = 232, G = 112, B = 96 };

    private readonly Renderer _renderer;
    private readonly string _fontPath;
    private readonly List<Texture> _purseLines = new();
    private readonly List<Texture> _panelLines = new();
    private string _builtSignature = "";

    public ShopScreen(Renderer renderer, string fontPath)
    {
        _renderer = renderer;
        _fontPath = fontPath;
        SortOffsetY = 1_500_000f; // over the map, under a battle
    }

    /// <summary>The counter being stood at, or null when out on the map.</summary>
    public Shop? Shop { get; set; }

    public override void Update(float deltaTime)
    {
    }

    public override void Render(Camera camera)
    {
        if (Shop is null) return;

        var (viewWidth, viewHeight) = _renderer.LogicalSize;
        RebuildIfChanged();

        var purseY = viewHeight - PanelHeight - PurseHeight - Margin - 2;
        DrawBox(Margin, purseY, viewWidth - Margin * 2, PurseHeight, _purseLines);
        DrawBox(Margin, viewHeight - PanelHeight - Margin, viewWidth - Margin * 2, PanelHeight, _panelLines);
    }

    private void DrawBox(int x, int y, int width, int height, List<Texture> lines)
    {
        _renderer.FillRect(x, y, width, height, 236, 236, 228, 245);
        _renderer.FillRect(x + 2, y + 2, width - 4, height - 4, 16, 20, 40, 250);

        var lineY = y + PaddingY;
        foreach (var line in lines)
        {
            line.Render(x + PaddingX, lineY);
            lineY += LineHeight;
        }
    }

    private void RebuildIfChanged()
    {
        var shop = Shop!;
        var signature = string.Join("|",
            _renderer.PixelsPerLogicalUnit,
            shop.Keeper, shop.Phase, shop.Message, shop.Index, shop.Coin,
            string.Join(",", shop.Stock));

        if (signature == _builtSignature) return;
        _builtSignature = signature;

        foreach (var texture in _purseLines) texture.Dispose();
        foreach (var texture in _panelLines) texture.Dispose();
        _purseLines.Clear();
        _panelLines.Clear();

        _purseLines.Add(Text($"{shop.Keeper}          {shop.Coin} coin", TextColor));

        if (shop.Phase == ShopPhase.Message)
        {
            _panelLines.Add(Text(shop.Message, TextColor));
            return;
        }

        if (shop.Kind == ShopKind.Inn)
        {
            _panelLines.Add(Text($"> A bed for the night   {shop.RestPrice} coin",
                shop.Coin >= shop.RestPrice ? CursorColor : DearColor));
            _panelLines.Add(Text("  (Escape to leave)", TextColor));
            return;
        }

        // Three at a time, scrolled to keep the cursor in view - the panel has
        // room for three lines and a shop may carry more than that.
        var window = Math.Min(3, shop.Stock.Count);
        var first = Math.Clamp(shop.Index - window / 2, 0, Math.Max(0, shop.Stock.Count - window));

        for (var i = first; i < first + window; i++)
        {
            var key = shop.Stock[i];
            var price = shop.PriceOf(key);
            var selected = i == shop.Index;
            var affordable = shop.Coin >= price;

            _panelLines.Add(Text(
                $"{(selected ? ">" : " ")} {Shop!.NameOf(key),-14} {price,4} coin",
                !affordable ? DearColor : selected ? CursorColor : TextColor));
        }
    }

    private Texture Text(string text, SDL.Color color) =>
        Texture.CreateTextTexture(_renderer, _fontPath, FontSize, color, text == "" ? " " : text);
}
