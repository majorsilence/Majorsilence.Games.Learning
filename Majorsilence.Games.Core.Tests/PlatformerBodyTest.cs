using System.Diagnostics;
using Majorsilence.Games.Core.Physics;

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

    private static PlatformerBody MakeBody() => new()
    {
        TileAt = (column, row) =>
        {
            if (column < 0 || column >= Grid[0].Length) return TileKind.Solid;
            if (row < 0 || row >= Grid.Length) return TileKind.Empty;
            return Grid[row][column] switch
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

    public void Test1()
    {
        FallsAndLands();
        WallStopsHorizontal();
        JumpsThroughOneWayFromBelowThenLands();
        DropsThroughOneWay();
        ClimbsLadder();
        CoyoteJumpAfterLedge();
        Console.WriteLine("PlatformerBodyTest passed.");
    }

    private void FallsAndLands()
    {
        var body = MakeBody();
        var (x, y) = StandOn(body, 6, 7);
        y -= 80f; // start mid-air above the floor
        Step(body, ref x, ref y, 0, 0, 0f, 1.0f);
        Debug.Assert(body.OnGround, "body should have landed on the solid floor");
        Debug.Assert(Math.Abs(y + body.OffsetY + body.Height - 7 * 32) < 0.01f, "feet should rest exactly on the floor top");
        Debug.Assert(body.VelocityY == 0f, "landing should zero vertical velocity");
    }

    private void WallStopsHorizontal()
    {
        var body = MakeBody();
        var (x, y) = StandOn(body, 5, 7);
        Step(body, ref x, ref y, -1, 0, 120f, 1.5f); // walk left into the column-2 wall
        var bodyLeft = x + body.OffsetX;
        Debug.Assert(Math.Abs(bodyLeft - 3 * 32) < 0.01f, $"wall should stop the body at x=96, got {bodyLeft}");
        Debug.Assert(body.OnGround, "should still be grounded while pushing a wall");
    }

    private void JumpsThroughOneWayFromBelowThenLands()
    {
        var body = MakeBody();
        var (x, y) = StandOn(body, 4, 7);
        // Falling from above: must land on the one-way platform surface.
        y = 2 * 32 - body.Height - body.OffsetY - 40f;
        body.VelocityY = 0f;
        Step(body, ref x, ref y, 0, 0, 0f, 1.0f);
        Debug.Assert(body.OnGround, "body should land on the one-way platform from above");
        Debug.Assert(Math.Abs(y + body.OffsetY + body.Height - 2 * 32) < 0.01f, "feet should rest on the platform top");

        // From below: place just under the platform moving up - must NOT collide.
        var below = 2 * 32 + 10f; // feet inside the tile under the platform surface
        y = below - body.Height - body.OffsetY;
        body.VelocityY = -300f;
        body.MoveAndCollide(ref x, ref y, 0, 0, 0f, 1f / 120f);
        Debug.Assert(body.VelocityY < 0f, "moving up through a one-way platform must not stop the body");
    }

    private void DropsThroughOneWay()
    {
        var body = MakeBody();
        // column 5: platform above, clear air below (no ladder in the way)
        var (x, y) = StandOn(body, 5, 7);
        // land on the platform first
        y = 2 * 32 - body.Height - body.OffsetY;
        Step(body, ref x, ref y, 0, 0, 0f, 0.1f);
        Debug.Assert(body.OnGround, "setup: should be standing on the one-way platform");
        body.RequestDropThrough();
        Step(body, ref x, ref y, 0, 0, 0f, 0.15f);
        Debug.Assert(!body.OnGround || y + body.OffsetY + body.Height > 2 * 32 + 4,
            "drop-through should let the body fall past the platform");
        Step(body, ref x, ref y, 0, 0, 0f, 1.5f);
        Debug.Assert(body.OnGround, "body should eventually land on the floor below");
        Debug.Assert(Math.Abs(y + body.OffsetY + body.Height - 7 * 32) < 0.01f, "feet should rest on the solid floor");
    }

    private void ClimbsLadder()
    {
        var body = MakeBody();
        var (x, y) = StandOn(body, 4, 7); // floor tile right below the ladder column
        Step(body, ref x, ref y, 0, 0, 0f, 0.05f);
        // climb briefly - long enough to be clearly off the floor, short enough
        // not to top out and detach
        Step(body, ref x, ref y, 0, -1, 0f, 0.3f);
        Debug.Assert(body.OnLadder, "holding up over a ladder should grab it");
        var feet = y + body.OffsetY + body.Height;
        Debug.Assert(feet < 7 * 32 - 20, $"body should have climbed well above the floor, feet at {feet}");
    }

    private void CoyoteJumpAfterLedge()
    {
        var body = MakeBody();
        var (x, y) = StandOn(body, 3, 7);
        // walk right off... floor spans the whole row; instead walk off the row-2 platform edge
        y = 2 * 32 - body.Height - body.OffsetY;
        x = 5 * 32 + (32 - body.Width) / 2f - body.OffsetX; // rightmost platform tile
        Step(body, ref x, ref y, 0, 0, 0f, 0.05f);
        Debug.Assert(body.OnGround, "setup: standing on the platform edge");
        // 0.15s at 200px/s carries the whole 12px-wide box past the platform
        // edge (~22px) with ~0.04s airborne - inside the 0.1s coyote window
        Step(body, ref x, ref y, 1, 0, 200f, 0.15f);
        Debug.Assert(!body.OnGround, "should be airborne after leaving the ledge");
        body.Jump();
        Debug.Assert(body.VelocityY < -400f, "coyote window should still allow a full jump just after the ledge");
    }
}
