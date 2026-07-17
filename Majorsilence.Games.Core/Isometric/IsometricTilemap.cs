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
///
/// Optionally surrounded by a much larger virtual world (worldMinColumn/MaxColumn/
/// MinRow/MaxRow) - e.g. an open ocean thousands of tiles across around a ship's
/// explicit hull tiles - which resolves to fallbackFrameIndex instead of being
/// empty/undefined. Only tiles visible in the camera's current viewport are ever
/// enumerated (padded by a small margin), so a virtual world of any size costs the
/// same per frame as a small one - it is never actually iterated in full.
/// </summary>
public class IsometricTilemap : GameObject
{
    private readonly int[,] _tiles;
    private readonly SpriteSheet _tileset;
    private readonly IsometricGrid _grid;
    private readonly int[,] _elevations;
    private readonly int _worldMinColumn;
    private readonly int _worldMaxColumn;
    private readonly int _worldMinRow;
    private readonly int _worldMaxRow;
    private readonly int _fallbackFrameIndex;

    public IsometricTilemap(int[,] tiles, SpriteSheet tileset, IsometricGrid grid, int[,]? elevations = null,
        int worldMinColumn = 0, int worldMaxColumn = 0, int worldMinRow = 0, int worldMaxRow = 0, int fallbackFrameIndex = -1)
    {
        _tiles = tiles;
        _tileset = tileset;
        _grid = grid;
        _elevations = elevations ?? new int[tiles.GetLength(0), tiles.GetLength(1)];
        _worldMinColumn = worldMinColumn;
        _worldMaxColumn = worldMaxColumn;
        _worldMinRow = worldMinRow;
        _worldMaxRow = worldMaxRow;
        _fallbackFrameIndex = fallbackFrameIndex;
    }

    private bool HasVirtualWorld => _worldMaxColumn > _worldMinColumn && _worldMaxRow > _worldMinRow;

    private Dictionary<int, int[]>? _frameVariants;

    /// <summary>
    /// Advancing this (e.g. on a timer) cycles every varianted tile to its next
    /// variant frame, animating them (water shimmer) without any per-tile state.
    /// </summary>
    public int AnimationPhase { get; set; }

    /// <summary>
    /// Registers alternate tileset frames for a base frame index: any tile that
    /// resolves to the base frame (explicit or virtual-world fallback) instead
    /// draws one of the variants, picked deterministically from its (column, row)
    /// plus AnimationPhase. Gives large uniform areas (open ocean) spatial
    /// variation and cheap animation - visibly scrolling past a moving ship -
    /// without storing anything per tile.
    /// </summary>
    public void SetFrameVariants(Dictionary<int, int[]> frameVariants)
    {
        _frameVariants = frameVariants;
    }

    private int ResolveVariant(int frameIndex, int column, int row)
    {
        if (_frameVariants is null || !_frameVariants.TryGetValue(frameIndex, out var variants) || variants.Length == 0)
            return frameIndex;

        var spatialHash = unchecked(column * 73856093 ^ row * 19349663);
        var pick = (spatialHash + AnimationPhase) % variants.Length;
        if (pick < 0) pick += variants.Length;
        return variants[pick];
    }

    public override void Update(float deltaTime)
    {
        // Tile layout is static; nothing to update.
    }

    /// <summary>
    /// Elevation (world pixels) of the given tile, or 0 if out of range - callers
    /// (e.g. a player standing on this tile) look this up every frame without
    /// needing to pre-validate bounds themselves. The virtual world area (if any)
    /// is always flat.
    /// </summary>
    public int GetElevationPixels(int column, int row)
    {
        var rows = _elevations.GetLength(0);
        var columns = rows == 0 ? 0 : _elevations.GetLength(1);
        if (row < 0 || row >= rows || column < 0 || column >= columns) return 0;
        return _elevations[row, column];
    }

    /// <summary>Overwrites a single tile's frame index at runtime (e.g. flooding a floor tile). Explicit-array tiles only.</summary>
    public void SetTile(int column, int row, int frameIndex)
    {
        _tiles[row, column] = frameIndex;
    }

    /// <summary>Overwrites a single tile's elevation at runtime (e.g. a listing/sinking deck). Explicit-array tiles only.</summary>
    public void SetElevation(int column, int row, int elevationPixels)
    {
        _elevations[row, column] = elevationPixels;
    }

    /// <summary>Frame index for a tile, resolving to the virtual-world fallback (or -1/empty) outside the explicit array.</summary>
    private int GetTileIndexOrFallback(int column, int row)
    {
        var rows = _tiles.GetLength(0);
        var columns = rows == 0 ? 0 : _tiles.GetLength(1);
        if (row >= 0 && row < rows && column >= 0 && column < columns)
            return _tiles[row, column];

        if (HasVirtualWorld && column >= _worldMinColumn && column < _worldMaxColumn && row >= _worldMinRow && row < _worldMaxRow)
            return _fallbackFrameIndex;

        return -1;
    }

    /// <summary>
    /// The range of tile coordinates that could be visible in the camera's current
    /// viewport (padded generously for tall/elevated tiles), intersected with this
    /// map's effective bounds (the virtual world if configured, else just the
    /// explicit array). Iterating only this range is what makes an arbitrarily large
    /// virtual world cheap to render.
    /// </summary>
    private (int ColStart, int ColEnd, int RowStart, int RowEnd) VisibleTileRange(Camera camera)
    {
        var explicitRows = _tiles.GetLength(0);
        var explicitColumns = explicitRows == 0 ? 0 : _tiles.GetLength(1);

        int minCol, maxCol, minRow, maxRow;
        if (HasVirtualWorld)
        {
            minCol = Math.Min(0, _worldMinColumn);
            maxCol = Math.Max(explicitColumns, _worldMaxColumn);
            minRow = Math.Min(0, _worldMinRow);
            maxRow = Math.Max(explicitRows, _worldMaxRow);
        }
        else
        {
            minCol = 0;
            maxCol = explicitColumns;
            minRow = 0;
            maxRow = explicitRows;
        }

        if (camera.ViewportWidth <= 0 || camera.ViewportHeight <= 0)
            return (minCol, maxCol, minRow, maxRow);

        var halfWidth = camera.ViewportWidth / 2f;
        var halfHeight = camera.ViewportHeight / 2f;
        // Grid.WorldToTile expects grid-local coordinates - this tilemap's own X/Y
        // (e.g. a ship's accumulated drift offset) is not part of that space, so it
        // has to be subtracted out of the camera's world-space position first.
        var localCenterX = camera.X - X;
        var localCenterY = camera.Y - Y;

        Span<float> cornerX = stackalloc float[]
        {
            localCenterX - halfWidth, localCenterX + halfWidth, localCenterX - halfWidth, localCenterX + halfWidth
        };
        Span<float> cornerY = stackalloc float[]
        {
            localCenterY - halfHeight, localCenterY - halfHeight, localCenterY + halfHeight, localCenterY + halfHeight
        };

        int colMin = int.MaxValue, colMax = int.MinValue, rowMin = int.MaxValue, rowMax = int.MinValue;
        for (var i = 0; i < 4; i++)
        {
            var (c, r) = _grid.WorldToTile((int)cornerX[i], (int)cornerY[i]);
            if (c < colMin) colMin = c;
            if (c > colMax) colMax = c;
            if (r < rowMin) rowMin = r;
            if (r > rowMax) rowMax = r;
        }

        const int pad = 3;
        var colStart = Math.Max(minCol, colMin - pad);
        var colEnd = Math.Min(maxCol, colMax + pad + 1);
        var rowStart = Math.Max(minRow, rowMin - pad);
        var rowEnd = Math.Min(maxRow, rowMax + pad + 1);
        return (colStart, colEnd, rowStart, rowEnd);
    }

    public override void Render(Camera camera)
    {
        var (colStart, colEnd, rowStart, rowEnd) = VisibleTileRange(camera);

        for (var row = rowStart; row < rowEnd; row++)
        {
            for (var column = colStart; column < colEnd; column++)
            {
                var tileIndex = GetTileIndexOrFallback(column, row);
                if (tileIndex < 0) continue;

                var (worldX, worldY) = _grid.TileToWorld(column, row);
                RenderTileStack(camera, X + worldX, Y + worldY, GetElevationPixels(column, row),
                    ResolveVariant(tileIndex, column, row));
            }
        }
    }

    /// <summary>
    /// Draws a tile at its elevation, with intermediate copies stacked every half
    /// tile-height from ground level up (bottom first), so an elevated tile reads
    /// as a solid block/terraced platform instead of a diamond floating over a
    /// gap. Ground-level tiles (the overwhelmingly common case) are one draw, as
    /// before. Intermediate layers only ever overlap tiles behind this one, which
    /// sort earlier in both render paths, so stacking never breaks paint order.
    /// </summary>
    private void RenderTileStack(Camera camera, int drawWorldX, int drawWorldY, int elevation, int frameIndex)
    {
        var step = Math.Max(1, _grid.TileHeight / 2);
        for (var layer = 0; layer < elevation; layer += step)
        {
            var (layerX, layerY) = camera.WorldToScreen(drawWorldX, drawWorldY - layer);
            _tileset.Render(layerX, layerY, frameIndex);
        }

        var (screenX, screenY) = camera.WorldToScreen(drawWorldX, drawWorldY - elevation);
        _tileset.Render(screenX, screenY, frameIndex);
    }

    /// <summary>
    /// Exposes each visible tile as an individually depth-sortable render item, so a
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
        var (colStart, colEnd, rowStart, rowEnd) = VisibleTileRange(camera);

        for (var row = rowStart; row < rowEnd; row++)
        {
            for (var column = colStart; column < colEnd; column++)
            {
                var tileIndex = GetTileIndexOrFallback(column, row);
                if (tileIndex < 0) continue;

                var (worldX, worldY) = _grid.TileToWorld(column, row);
                var drawWorldX = X + worldX;
                var drawWorldY = Y + worldY;
                var elevation = GetElevationPixels(column, row);
                var capturedColumn = column;
                var capturedRow = row;
                yield return (drawWorldY + _grid.TileHeight, () =>
                    RenderTileStack(camera, drawWorldX, drawWorldY, elevation,
                        ResolveVariant(tileIndex, capturedColumn, capturedRow)));
            }
        }
    }
}
