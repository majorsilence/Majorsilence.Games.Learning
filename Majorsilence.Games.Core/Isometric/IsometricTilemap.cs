using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Core.Isometric;

/// <summary>
/// Renders a grid of tile indices, addressed as tiles[row, col], as an isometric
/// map using a SpriteSheet as the tileset. Tiles are drawn back-to-front (row-major,
/// then column) which is correct draw order for the diamond projection used by IsometricGrid.
/// A tile index below zero is treated as empty and skipped.
/// </summary>
public class IsometricTilemap : GameObject
{
    private readonly int[,] _tiles;
    private readonly SpriteSheet _tileset;
    private readonly IsometricGrid _grid;

    public IsometricTilemap(int[,] tiles, SpriteSheet tileset, IsometricGrid grid)
    {
        _tiles = tiles;
        _tileset = tileset;
        _grid = grid;
    }

    public override void Update(float deltaTime)
    {
        // Tile layout is static; nothing to update.
    }

    public override void Render()
    {
        var rows = _tiles.GetLength(0);
        var columns = _tiles.GetLength(1);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var tileIndex = _tiles[row, column];
                if (tileIndex < 0) continue;

                var (screenX, screenY) = _grid.TileToScreen(column, row);
                _tileset.Render(X + screenX, Y + screenY, tileIndex);
            }
        }
    }

    /// <summary>
    /// Exposes each tile as an individually depth-sortable render item, so a
    /// RenderQueue can interleave moving GameObjects with individual tiles
    /// (e.g. a character passing behind a tall tile) instead of only being
    /// globally in front of or behind the whole tilemap. Uses the same tile
    /// iteration and TileToScreen math as Render(), which is kept as-is for
    /// standalone/non-sorted use.
    /// </summary>
    public IEnumerable<(float SortY, Action Render)> EnumerateRenderItems()
    {
        var rows = _tiles.GetLength(0);
        var columns = _tiles.GetLength(1);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var tileIndex = _tiles[row, column];
                if (tileIndex < 0) continue;

                var (screenX, screenY) = _grid.TileToScreen(column, row);
                var drawX = X + screenX;
                var drawY = Y + screenY;
                yield return (drawY + _grid.TileHeight, () => _tileset.Render(drawX, drawY, tileIndex));
            }
        }
    }
}
