namespace Majorsilence.Games.Core.Isometric;

/// <summary>
/// Converts between tile (column, row) coordinates and world pixel coordinates
/// for a 2:1 diamond isometric projection. Origin is a fixed world-space offset
/// for this grid, set once at construction - panning/following/recentering on
/// screen is Camera's job, not this class's.
/// </summary>
public class IsometricGrid
{
    public int TileWidth { get; }
    public int TileHeight { get; }
    public int OriginX { get; }
    public int OriginY { get; }

    public IsometricGrid(int tileWidth, int tileHeight, int originX = 0, int originY = 0)
    {
        if (tileWidth <= 0 || tileHeight <= 0)
            throw new MajorsilenceException("Isometric tile dimensions must be greater than zero.");

        TileWidth = tileWidth;
        TileHeight = tileHeight;
        OriginX = originX;
        OriginY = originY;
    }

    /// <summary>
    /// Top-left world pixel position at which a tile's texture frame should be drawn.
    /// </summary>
    public (int X, int Y) TileToWorld(int column, int row)
    {
        var x = OriginX + (column - row) * (TileWidth / 2);
        var y = OriginY + (column + row) * (TileHeight / 2);
        return (x, y);
    }

    /// <summary>
    /// Tile (column, row) that contains the given world pixel position.
    /// </summary>
    public (int Column, int Row) WorldToTile(int worldX, int worldY)
    {
        var x = worldX - OriginX;
        var y = worldY - OriginY;

        var halfW = TileWidth / 2.0;
        var halfH = TileHeight / 2.0;

        var column = (x / halfW + y / halfH) / 2.0;
        var row = (y / halfH - x / halfW) / 2.0;

        return ((int)Math.Floor(column), (int)Math.Floor(row));
    }
}
