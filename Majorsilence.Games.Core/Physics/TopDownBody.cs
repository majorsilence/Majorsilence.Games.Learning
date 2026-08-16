using System;

namespace Majorsilence.Games.Core.Physics;

/// <summary>
/// Movement and collision for a body walking over a flat top-down map: no
/// gravity, no jumping, just axis-separated AABB collision against a tile grid.
/// The side-view counterpart is PlatformerBody, and this follows the same
/// contract - collision knowledge arrives through the IsSolid callback rather
/// than a tilemap reference, so it stays decoupled from any particular
/// tilemap/room implementation.
///
/// Axis separation is what makes walls feel right: a body pushing diagonally
/// into a wall keeps the component that isn't blocked, sliding along the wall
/// instead of sticking to it.
/// </summary>
public class TopDownBody
{
    /// <summary>True if the given tile blocks movement. Out-of-range coordinates are the callback's concern (usually solid, so the map edge is a wall).</summary>
    public required Func<int, int, bool> IsSolid { get; init; }

    public int TileWidth { get; init; } = 16;
    public int TileHeight { get; init; } = 16;

    /// <summary>World position of the tile grid's top-left corner (FlatTilemap.X/Y).</summary>
    public int MapOriginX { get; set; }
    public int MapOriginY { get; set; }

    /// <summary>
    /// Collision box relative to the owner's top-left (X/Y). The defaults inset
    /// a 12x10 box into the bottom of a 16x16 character sprite: only the feet
    /// collide, so a character's head can overlap the tile above - the standard
    /// top-down look, and what lets a 16px character walk a 16px corridor
    /// without catching on every doorway.
    /// </summary>
    public int OffsetX { get; set; } = 2;
    public int OffsetY { get; set; } = 6;
    public int Width { get; set; } = 12;
    public int Height { get; set; } = 10;

    /// <summary>
    /// World px/s a blocked step slides the body sideways to line it up with an
    /// opening it is nearly centered on. Doorways here are one tile wide and the
    /// body is only a few pixels narrower, so without this a walker a pixel or
    /// two off-centre stops dead against the door frame - technically correct
    /// and thoroughly annoying to play. 0 turns the assist off.
    /// </summary>
    public float DoorwayAssistSpeed { get; set; } = 48f;

    /// <summary>
    /// Integrates one frame of walking, mutating the owner's sub-pixel position
    /// accumulators in place. dirX/dirY are -1/0/1 intent; speed is world px/s.
    /// </summary>
    public void MoveAndCollide(ref float preciseX, ref float preciseY, int dirX, int dirY, float speed, float deltaTime)
    {
        var deltaX = dirX * speed * deltaTime;
        var deltaY = dirY * speed * deltaTime;

        // Sub-step so a large frame time can't carry the box through a wall
        // between two checks.
        var maxStep = Math.Max(1f, Math.Min(TileWidth, TileHeight) / 2f);
        var steps = Math.Max(1, (int)MathF.Ceiling(Math.Max(MathF.Abs(deltaX), MathF.Abs(deltaY)) / maxStep));
        var assist = DoorwayAssistSpeed * deltaTime / steps;

        for (var i = 0; i < steps; i++)
        {
            MoveHorizontal(ref preciseX, ref preciseY, deltaX / steps, assist);
            MoveVertical(ref preciseX, ref preciseY, deltaY / steps, assist);
        }
    }

    /// <summary>True if the box would overlap a solid tile at the given owner position - for placement checks (spawning, teleporting).</summary>
    public bool Blocked(float preciseX, float preciseY)
    {
        var left = preciseX + OffsetX;
        var top = preciseY + OffsetY;
        return AnySolid(left, top, left + Width - 1f, top + Height - 1f);
    }

    private void MoveHorizontal(ref float preciseX, ref float preciseY, float deltaX, float assist)
    {
        if (deltaX == 0f) return;
        preciseX += deltaX;

        var column = deltaX > 0f
            ? ToColumn(preciseX + OffsetX + Width - 1f)
            : ToColumn(preciseX + OffsetX);
        if (!ColumnBlocks(column, preciseY)) return;

        // Blocked - but if the row the body is centered on is clear, what it
        // caught was the corner of a neighbouring row: slide toward that row's
        // middle and try again. That is a body squeezing into a doorway.
        if (assist > 0f)
        {
            var centerRow = ToRow(preciseY + OffsetY + Height / 2f);
            if (!IsSolid(column, centerRow))
            {
                preciseY += NudgeToward(preciseY, MapOriginY + centerRow * TileHeight + (TileHeight - Height) / 2f - OffsetY, assist);
                if (!ColumnBlocks(column, preciseY)) return;
            }
        }

        preciseX = deltaX > 0f
            ? MapOriginX + column * TileWidth - Width - OffsetX
            : MapOriginX + (column + 1) * TileWidth - OffsetX;
    }

    private void MoveVertical(ref float preciseX, ref float preciseY, float deltaY, float assist)
    {
        if (deltaY == 0f) return;
        preciseY += deltaY;

        var row = deltaY > 0f
            ? ToRow(preciseY + OffsetY + Height - 1f)
            : ToRow(preciseY + OffsetY);
        if (!RowBlocks(row, preciseX)) return;

        if (assist > 0f)
        {
            var centerColumn = ToColumn(preciseX + OffsetX + Width / 2f);
            if (!IsSolid(centerColumn, row))
            {
                preciseX += NudgeToward(preciseX, MapOriginX + centerColumn * TileWidth + (TileWidth - Width) / 2f - OffsetX, assist);
                if (!RowBlocks(row, preciseX)) return;
            }
        }

        preciseY = deltaY > 0f
            ? MapOriginY + row * TileHeight - Height - OffsetY
            : MapOriginY + (row + 1) * TileHeight - OffsetY;
    }

    private static float NudgeToward(float value, float target, float limit) =>
        Math.Clamp(target - value, -limit, limit);

    private bool ColumnBlocks(int column, float preciseY)
    {
        var top = preciseY + OffsetY;
        return AnySolidInColumn(column, top, top + Height - 1f);
    }

    private bool RowBlocks(int row, float preciseX)
    {
        var left = preciseX + OffsetX;
        return AnySolidInRow(row, left, left + Width - 1f);
    }

    private bool AnySolid(float left, float top, float right, float bottom)
    {
        for (var row = ToRow(top); row <= ToRow(bottom); row++)
        for (var column = ToColumn(left); column <= ToColumn(right); column++)
        {
            if (IsSolid(column, row)) return true;
        }

        return false;
    }

    private bool AnySolidInColumn(int column, float top, float bottom)
    {
        for (var row = ToRow(top); row <= ToRow(bottom); row++)
        {
            if (IsSolid(column, row)) return true;
        }

        return false;
    }

    private bool AnySolidInRow(int row, float left, float right)
    {
        for (var column = ToColumn(left); column <= ToColumn(right); column++)
        {
            if (IsSolid(column, row)) return true;
        }

        return false;
    }

    /// <summary>The tile the body's box is centered on - what "the tile you're standing on" means for doors, encounters and interaction.</summary>
    public (int Column, int Row) CenterTile(float preciseX, float preciseY) =>
        (ToColumn(preciseX + OffsetX + Width / 2f), ToRow(preciseY + OffsetY + Height / 2f));

    private int ToColumn(float worldX) => (int)MathF.Floor((worldX - MapOriginX) / TileWidth);
    private int ToRow(float worldY) => (int)MathF.Floor((worldY - MapOriginY) / TileHeight);
}
