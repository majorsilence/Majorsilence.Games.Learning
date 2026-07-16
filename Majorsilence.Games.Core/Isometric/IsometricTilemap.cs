using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Core.Isometric;

/// <summary>
/// Renders a grid of tile indices, addressed as tiles[row, col], as an isometric
/// map using a SpriteSheet as the tileset. Tiles are drawn back-to-front (row-major,
/// then column) which is correct draw order for the diamond projection used by IsometricGrid.
/// A tile index below zero is treated as empty and skipped. An optional parallel
/// elevations[row, col] grid (world pixels) lifts individual tiles for multi-level
/// platforms - defaults to flat (all zero) if omitted.
/// </summary>
public class IsometricTilemap : GameObject
{
    private readonly int[,] _tiles;
    private readonly SpriteSheet _tileset;
    private readonly IsometricGrid _grid;
    private readonly int[,] _elevations;

    public IsometricTilemap(int[,] tiles, SpriteSheet tileset, IsometricGrid grid, int[,]? elevations = null)
    {
        _tiles = tiles;
        _tileset = tileset;
        _grid = grid;
        _elevations = elevations ?? new int[tiles.GetLength(0), tiles.GetLength(1)];
    }

    public override void Update(float deltaTime)
    {
        // Tile layout is static; nothing to update.
    }

    /// <summary>
    /// Elevation (world pixels) of the given tile, or 0 if out of range - callers
    /// (e.g. a player standing on this tile) look this up every frame without
    /// needing to pre-validate bounds themselves.
    /// </summary>
    public int GetElevationPixels(int column, int row)
    {
        var rows = _elevations.GetLength(0);
        var columns = rows == 0 ? 0 : _elevations.GetLength(1);
        if (row < 0 || row >= rows || column < 0 || column >= columns) return 0;
        return _elevations[row, column];
    }

    /// <summary>Overwrites a single tile's frame index at runtime (e.g. flooding a floor tile).</summary>
    public void SetTile(int column, int row, int frameIndex)
    {
        _tiles[row, column] = frameIndex;
    }

    /// <summary>Overwrites a single tile's elevation at runtime (e.g. a listing/sinking deck).</summary>
    public void SetElevation(int column, int row, int elevationPixels)
    {
        _elevations[row, column] = elevationPixels;
    }

    public override void Render(Camera camera)
    {
        var rows = _tiles.GetLength(0);
        var columns = _tiles.GetLength(1);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var tileIndex = _tiles[row, column];
                if (tileIndex < 0) continue;

                var (worldX, worldY) = _grid.TileToWorld(column, row);
                var (screenX, screenY) = camera.WorldToScreen(X + worldX, Y + worldY - _elevations[row, column]);
                _tileset.Render(screenX, screenY, tileIndex);
            }
        }
    }

    /// <summary>
    /// Exposes each tile as an individually depth-sortable render item, so a
    /// RenderQueue can interleave moving GameObjects with individual tiles
    /// (e.g. a character passing behind a tall tile) instead of only being
    /// globally in front of or behind the whole tilemap. Uses the same tile
    /// iteration and TileToWorld math as Render(), which is kept as-is for
    /// standalone/non-sorted use. SortY is ground-based (ignores elevation, just
    /// like GameObject.SortY ignores Z) so an elevated tile still sorts by its
    /// footprint, not its lifted screen position; camera panning is a uniform
    /// translation so it never changes relative tile-vs-tile ordering either.
    /// </summary>
    public IEnumerable<(float SortY, Action Render)> EnumerateRenderItems(Camera camera)
    {
        var rows = _tiles.GetLength(0);
        var columns = _tiles.GetLength(1);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var tileIndex = _tiles[row, column];
                if (tileIndex < 0) continue;

                var (worldX, worldY) = _grid.TileToWorld(column, row);
                var drawWorldX = X + worldX;
                var drawWorldY = Y + worldY;
                var elevation = _elevations[row, column];
                yield return (drawWorldY + _grid.TileHeight, () =>
                {
                    var (screenX, screenY) = camera.WorldToScreen(drawWorldX, drawWorldY - elevation);
                    _tileset.Render(screenX, screenY, tileIndex);
                });
            }
        }
    }
}
