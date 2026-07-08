namespace Majorsilence.Games.Core.Isometric;

/// <summary>
/// Converts between tile (column, row) coordinates and screen pixel coordinates
/// for a 2:1 diamond isometric projection.
/// </summary>
public class IsometricGrid
{
    public int TileWidth { get; }
    public int TileHeight { get; }
    public int OriginX { get; set; }
    public int OriginY { get; set; }

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
    /// Top-left screen pixel position at which a tile's texture frame should be drawn.
    /// </summary>
    public (int X, int Y) TileToScreen(int column, int row)
    {
        var x = OriginX + (column - row) * (TileWidth / 2);
        var y = OriginY + (column + row) * (TileHeight / 2);
        return (x, y);
    }

    /// <summary>
    /// Tile (column, row) that contains the given screen pixel position.
    /// </summary>
    public (int Column, int Row) ScreenToTile(int screenX, int screenY)
    {
        var x = screenX - OriginX;
        var y = screenY - OriginY;

        var halfW = TileWidth / 2.0;
        var halfH = TileHeight / 2.0;

        var column = (x / halfW + y / halfH) / 2.0;
        var row = (y / halfH - x / halfW) / 2.0;

        return ((int)Math.Floor(column), (int)Math.Floor(row));
    }
}
