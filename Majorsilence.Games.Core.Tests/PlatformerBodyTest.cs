using Majorsilence.Games.Core.Physics;
using Xunit;

namespace Majorsilence.Games.Core.Tests;

/// <summary>
/// Pure-math checks for PlatformerBody.MoveAndCollide - no SDL required.
/// World: 32px tiles on a small ASCII grid; '#' solid, '-' one-way platform,
/// 'H' ladder, '.' empty. Out-of-range columns are solid walls, rows are open.
/// </summary>
public class PlatformerBodyTest
{
    private static readonly string[] Grid =
    {
        "..........",
        "..........",
        "...---....",
        "..........",
        "....H.....",
        "..#.H.....",
        "..#.H.....",
        "##########"
    };

    private static PlatformerBody MakeBody() => MakeBody(Grid);

    private static PlatformerBody MakeBody(string[] grid) => new()
    {
        TileAt = (column, row) =>
        {
            if (column < 0 || column >= grid[0].Length) return TileKind.Solid;
            if (row < 0 || row >= grid.Length) return TileKind.Empty;
            return grid[row][column] switch
            {
                '#' => TileKind.Solid,
                '-' => TileKind.OneWay,
                'H' => TileKind.Ladder,
                _ => TileKind.Empty
            };
        },
        TileWidth = 32,
        TileHeight = 32
    };

    // Places the body's feet-center on the given tile, returning the owner
    // top-left (preciseX, preciseY) like DynamicObject would hold.
    private static (float X, float Y) StandOn(PlatformerBody body, int column, int row)
    {
        var x = column * 32 + (32 - body.Width) / 2f - body.OffsetX;
        var y = row * 32 - body.Height - body.OffsetY;
        return (x, y);
    }

    private static void Step(PlatformerBody body, ref float x, ref float y, int dirX, int climbDir, float speed, float seconds)
    {
        // fixed 120Hz steps: deterministic and well under the substep threshold
        var steps = (int)(seconds * 120);
        for (var i = 0; i < steps; i++)
            body.MoveAndCollide(ref x, ref y, dirX, climbDir, speed, 1f / 120f);
    }

    /// <summary>
    /// Pins the reach a jump actually has at the player's walking speed, because
    /// every side-view level is laid out against it: gravity 1500 and a -480
    /// launch give a 0.64s flight and a 76.8px arc, which is two tiles of height
    /// and two tiles of gap - and emphatically not three. A level whose platforms
    /// are spaced beyond this is unfinishable, so if these numbers are ever
    /// retuned, the level grids have to be re-walked with them.
    /// </summary>
    [Fact]
    public void JumpBudgetIsTwoTiles()
    {
        const float walkSpeed = 120f; // Game.BasePlayerSpeed, no boots/snack buff

        // Two-tile gap, taken off from the very lip of the ledge.
        var crossable = MakeBody(new[]
        {
            "..........",
            "..........",
            "..........",
            "..........",
            "###..#####",
        });
        var x = 3 * 32f - crossable.Width - crossable.OffsetX; // box right edge on the ledge lip
        var y = 4 * 32f - crossable.Height - crossable.OffsetY;
        Step(crossable, ref x, ref y, 0, 0, 0f, 0.05f);
        Assert.True(crossable.OnGround, "setup: should start standing on the ledge");
        crossable.Jump();
        Step(crossable, ref x, ref y, 1, 0, walkSpeed, 0.8f);
        Assert.True(crossable.OnGround, "a jump at walking speed must clear a two-tile gap");
        Assert.True(Math.Abs(y + crossable.OffsetY + crossable.Height - 4 * 32) < 0.01f,
            "should have landed back on the floor row, not fallen past it");

        // The same jump over three tiles falls short - the budget's upper bound.
        var tooWide = MakeBody(new[]
        {
            "..........",
            "..........",
            "..........",
            "..........",
            "###...####",
        });
        x = 3 * 32f - tooWide.Width - tooWide.OffsetX;
        y = 4 * 32f - tooWide.Height - tooWide.OffsetY;
        Step(tooWide, ref x, ref y, 0, 0, 0f, 0.05f);
        tooWide.Jump();
        Step(tooWide, ref x, ref y, 1, 0, walkSpeed, 0.8f);
        Assert.True(!tooWide.OnGround && y + tooWide.OffsetY + tooWide.Height > 5 * 32,
            "a three-tile gap must be out of reach - level pits are sized against this");

        // Two tiles of height: a platform whose surface is 64px up is reachable.
        var upward = MakeBody(new[]
        {
            "..........",
            "..........",
            "..........",
            "....---...",
            "..........",
            "##########",
        });
        x = 4 * 32f + (32 - upward.Width) / 2f - upward.OffsetX;
        y = 5 * 32f - upward.Height - upward.OffsetY;
        Step(upward, ref x, ref y, 0, 0, 0f, 0.05f);
        Assert.True(upward.OnGround, "setup: should start standing on the floor");
        upward.Jump();
        Step(upward, ref x, ref y, 0, 0, 0f, 0.8f);
        Assert.True(upward.OnGround, "a jump must reach a platform two tiles up");
        Assert.True(Math.Abs(y + upward.OffsetY + upward.Height - 3 * 32) < 0.01f,
            "should be resting on the raised platform, not back on the floor");
    }

    [Fact]
    public void FallsAndLands()
    {
        var body = MakeBody();
        var (x, y) = StandOn(body, 6, 7);
        y -= 80f; // start mid-air above the floor
        Step(body, ref x, ref y, 0, 0, 0f, 1.0f);
        Assert.True(body.OnGround, "body should have landed on the solid floor");
        Assert.True(Math.Abs(y + body.OffsetY + body.Height - 7 * 32) < 0.01f, "feet should rest exactly on the floor top");
        Assert.True(body.VelocityY == 0f, "landing should zero vertical velocity");
    }

    [Fact]
    public void WallStopsHorizontal()
    {
        var body = MakeBody();
        var (x, y) = StandOn(body, 5, 7);
        Step(body, ref x, ref y, -1, 0, 120f, 1.5f); // walk left into the column-2 wall
        var bodyLeft = x + body.OffsetX;
        Assert.True(Math.Abs(bodyLeft - 3 * 32) < 0.01f, $"wall should stop the body at x=96, got {bodyLeft}");
        Assert.True(body.OnGround, "should still be grounded while pushing a wall");
    }

    [Fact]
    public void JumpsThroughOneWayFromBelowThenLands()
    {
        var body = MakeBody();
        var (x, y) = StandOn(body, 4, 7);
        // Falling from above: must land on the one-way platform surface.
        y = 2 * 32 - body.Height - body.OffsetY - 40f;
        body.VelocityY = 0f;
        Step(body, ref x, ref y, 0, 0, 0f, 1.0f);
        Assert.True(body.OnGround, "body should land on the one-way platform from above");
        Assert.True(Math.Abs(y + body.OffsetY + body.Height - 2 * 32) < 0.01f, "feet should rest on the platform top");

        // From below: place just under the platform moving up - must NOT collide.
        var below = 2 * 32 + 10f; // feet inside the tile under the platform surface
        y = below - body.Height - body.OffsetY;
        body.VelocityY = -300f;
        body.MoveAndCollide(ref x, ref y, 0, 0, 0f, 1f / 120f);
        Assert.True(body.VelocityY < 0f, "moving up through a one-way platform must not stop the body");
    }

    [Fact]
    public void DropsThroughOneWay()
    {
        var body = MakeBody();
        // column 5: platform above, clear air below (no ladder in the way)
        var (x, y) = StandOn(body, 5, 7);
        // land on the platform first
        y = 2 * 32 - body.Height - body.OffsetY;
        Step(body, ref x, ref y, 0, 0, 0f, 0.1f);
        Assert.True(body.OnGround, "setup: should be standing on the one-way platform");
        body.RequestDropThrough();
        Step(body, ref x, ref y, 0, 0, 0f, 0.15f);
        Assert.True(!body.OnGround || y + body.OffsetY + body.Height > 2 * 32 + 4,
            "drop-through should let the body fall past the platform");
        Step(body, ref x, ref y, 0, 0, 0f, 1.5f);
        Assert.True(body.OnGround, "body should eventually land on the floor below");
        Assert.True(Math.Abs(y + body.OffsetY + body.Height - 7 * 32) < 0.01f, "feet should rest on the solid floor");
    }

    [Fact]
    public void ClimbsLadder()
    {
        var body = MakeBody();
        var (x, y) = StandOn(body, 4, 7); // floor tile right below the ladder column
        Step(body, ref x, ref y, 0, 0, 0f, 0.05f);
        // climb briefly - long enough to be clearly off the floor, short enough
        // not to top out and detach
        Step(body, ref x, ref y, 0, -1, 0f, 0.3f);
        Assert.True(body.OnLadder, "holding up over a ladder should grab it");
        var feet = y + body.OffsetY + body.Height;
        Assert.True(feet < 7 * 32 - 20, $"body should have climbed well above the floor, feet at {feet}");
    }

    [Fact]
    public void CoyoteJumpAfterLedge()
    {
        var body = MakeBody();
        var (x, y) = StandOn(body, 3, 7);
        // walk right off... floor spans the whole row; instead walk off the row-2 platform edge
        y = 2 * 32 - body.Height - body.OffsetY;
        x = 5 * 32 + (32 - body.Width) / 2f - body.OffsetX; // rightmost platform tile
        Step(body, ref x, ref y, 0, 0, 0f, 0.05f);
        Assert.True(body.OnGround, "setup: standing on the platform edge");
        // 0.15s at 200px/s carries the whole 12px-wide box past the platform
        // edge (~22px) with ~0.04s airborne - inside the 0.1s coyote window
        Step(body, ref x, ref y, 1, 0, 200f, 0.15f);
        Assert.True(!body.OnGround, "should be airborne after leaving the ledge");
        body.Jump();
        Assert.True(body.VelocityY < -400f, "coyote window should still allow a full jump just after the ledge");
    }
}
