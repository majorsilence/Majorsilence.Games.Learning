using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Core.Tilemaps;

/// <summary>
/// Renders a grid of tile indices, addressed as tiles[row, col], as a flat
/// orthogonal (non-isometric) map - for side-scroller/platformer levels. Unlike
/// IsometricTilemap, tiles don't visually overlap so no per-tile depth-sort
/// against moving sprites is needed - the whole map draws as one ZIndex layer.
/// A tile index below zero is treated as empty and skipped.
/// </summary>
public class FlatTilemap : GameObject
{
    private readonly int[,] _tiles;
    private readonly SpriteSheet _tileset;
    private readonly int _tileWidth;
    private readonly int _tileHeight;
    private readonly int[,] _elevations;

    public FlatTilemap(int[,] tiles, SpriteSheet tileset, int tileWidth, int tileHeight, int[,]? elevations = null)
    {
        _tiles = tiles;
        _tileset = tileset;
        _tileWidth = tileWidth;
        _tileHeight = tileHeight;
        _elevations = elevations ?? new int[tiles.GetLength(0), tiles.GetLength(1)];
    }

    /// <summary>World pixel size of the whole grid - handy for camera bounds clamping.</summary>
    public int PixelWidth => _tiles.GetLength(1) * _tileWidth;
    public int PixelHeight => _tiles.GetLength(0) * _tileHeight;

    public override void Update(float deltaTime)
    {
        // Tile layout is static; nothing to update.
    }

    /// <summary>Elevation (world pixels) of the given tile, or 0 if out of range.</summary>
    public int GetElevationPixels(int column, int row)
    {
        var rows = _elevations.GetLength(0);
        var columns = rows == 0 ? 0 : _elevations.GetLength(1);
        if (row < 0 || row >= rows || column < 0 || column >= columns) return 0;
        return _elevations[row, column];
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

                var worldX = X + column * _tileWidth;
                var worldY = Y + row * _tileHeight - _elevations[row, column];
                var (screenX, screenY) = camera.WorldToScreen(worldX, worldY);
                _tileset.Render(screenX, screenY, tileIndex);
            }
        }
    }
}
