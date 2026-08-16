using Majorsilence.Games.Core.Physics;
using Xunit;

namespace Majorsilence.Games.Core.Tests;

/// <summary>
/// Pure-math checks for TopDownBody.MoveAndCollide - no SDL required.
/// World: 16px tiles on a small ASCII grid; '#' solid, '.' open. Out-of-range
/// tiles are solid, so the grid's edge is a wall.
/// </summary>
public class TopDownBodyTest
{
    private const float WalkSpeed = 64f; // RpgGame.WalkSpeed

    private static TopDownBody MakeBody(string[] grid) => new()
    {
        IsSolid = (column, row) =>
        {
            if (row < 0 || row >= grid.Length) return true;
            if (column < 0 || column >= grid[row].Length) return true;
            return grid[row][column] == '#';
        }
    };

    /// <summary>Owner top-left that centers the body's box on the given tile - where a spawn or a door drop puts it.</summary>
    private static (float X, float Y) CenterOn(TopDownBody body, int column, int row) =>
    (
        column * 16f + (16 - body.Width) / 2f - body.OffsetX,
        row * 16f + (16 - body.Height) / 2f - body.OffsetY
    );

    private static void Step(TopDownBody body, ref float x, ref float y, int dirX, int dirY, float seconds)
    {
        // fixed 120Hz steps: deterministic, and well under the sub-step threshold
        var steps = (int)(seconds * 120);
        for (var i = 0; i < steps; i++)
            body.MoveAndCollide(ref x, ref y, dirX, dirY, WalkSpeed, 1f / 120f);
    }

    private static readonly string[] Room =
    {
        "######",
        "#....#",
        "#....#",
        "#....#",
        "######"
    };

    [Fact]
    public void WallStopsHorizontalMovement()
    {
        var body = MakeBody(Room);
        var (x, y) = CenterOn(body, 1, 2);

        Step(body, ref x, ref y, 1, 0, 2f); // walk right into the column-5 wall

        Assert.Equal(5 * 16 - body.Width - body.OffsetX, x, 2);
    }

    [Fact]
    public void WallStopsVerticalMovement()
    {
        var body = MakeBody(Room);
        var (x, y) = CenterOn(body, 2, 1);

        Step(body, ref x, ref y, 0, 1, 2f); // walk down into the row-4 wall

        Assert.Equal(4 * 16 - body.Height - body.OffsetY, y, 2);
    }

    /// <summary>
    /// Axis separation, the thing that makes walls feel right: pushing diagonally
    /// into a wall keeps the component that isn't blocked, so the body slides
    /// along the wall instead of sticking to it.
    /// </summary>
    [Fact]
    public void DiagonalIntoAWallSlidesAlongIt()
    {
        var body = MakeBody(Room);
        var (x, y) = CenterOn(body, 1, 1);
        var startY = y;

        Step(body, ref x, ref y, -1, 1, 0.5f); // into the left wall, and downward

        Assert.Equal(1 * 16 - body.OffsetX, x, 2);
        Assert.True(y > startY + 20f, $"blocked horizontally, the body should still have slid down; moved {y - startY:0.0}px");
    }

    // A one-tile gap in a vertical wall - the shape of every doorway in the game.
    private static readonly string[] Doorway =
    {
        "..#..",
        ".....",
        "..#.."
    };

    [Fact]
    public void DoorwayAssistSquaresUpASlightlyOffCentreWalker()
    {
        var body = MakeBody(Doorway);
        var (x, y) = CenterOn(body, 0, 1);
        y -= 5f; // a few pixels high - the box now clips the tile above the gap

        Step(body, ref x, ref y, 1, 0, 2f);

        var (column, _) = body.CenterTile(x, y);
        Assert.True(column > 2, $"a walker 5px off-centre should be nudged through the gap, stopped at column {column}");
    }

    /// <summary>
    /// The assist has to have a limit, or it becomes teleportation. It reaches
    /// only while the body's box is still centered on the open row; once the
    /// centre has crossed into the wall's row, the wall simply stops you.
    /// </summary>
    [Fact]
    public void DoorwayAssistDoesNotRescueAWalkerCentredOnTheWall()
    {
        var body = MakeBody(Doorway);
        var (x, y) = CenterOn(body, 0, 1);
        y -= 10f; // box centre is now in the wall's row, not the gap's

        Step(body, ref x, ref y, 1, 0, 2f);

        Assert.Equal(2 * 16 - body.Width - body.OffsetX, x, 2);
    }

    [Fact]
    public void DoorwayAssistCanBeTurnedOff()
    {
        var body = MakeBody(Doorway);
        body.DoorwayAssistSpeed = 0f;
        var (x, y) = CenterOn(body, 0, 1);
        y -= 5f;

        Step(body, ref x, ref y, 1, 0, 2f);

        Assert.Equal(2 * 16 - body.Width - body.OffsetX, x, 2);
    }

    [Fact]
    public void CenterTileIsTheTileUnderTheBoxCentre()
    {
        var body = MakeBody(Room);
        var (x, y) = CenterOn(body, 3, 2);

        Assert.Equal((3, 2), body.CenterTile(x, y));
    }

    /// <summary>
    /// Only the feet collide: the box is inset into the bottom of a 16x16 sprite,
    /// so a character's head overlaps the tile above without catching on it. This
    /// is what lets a 16px character walk a 16px corridor.
    /// </summary>
    [Fact]
    public void HeadOverlappingTheTileAboveIsNotACollision()
    {
        var body = MakeBody(Room);
        var (x, y) = CenterOn(body, 2, 1);

        Assert.True(y < 1 * 16, "setup: the sprite's top-left should sit inside the solid row above");
        Assert.False(body.Blocked(x, y), "only the feet box should collide, not the sprite's full square");
    }

    [Fact]
    public void BlockedReportsOverlapWithASolidTile()
    {
        var body = MakeBody(Room);

        var (openX, openY) = CenterOn(body, 2, 2);
        Assert.False(body.Blocked(openX, openY));

        var (wallX, wallY) = CenterOn(body, 0, 2); // the left wall column
        Assert.True(body.Blocked(wallX, wallY));
    }

    [Fact]
    public void MapEdgeIsAWallWhenTheCallbackSaysSo()
    {
        var body = MakeBody(new[] { "..." });
        var (x, y) = CenterOn(body, 1, 0);

        Step(body, ref x, ref y, 0, -1, 1f); // walk up, off the top of the grid

        Assert.Equal(0f, y + body.OffsetY, 2); // box top rests on the grid's top edge
    }
}
