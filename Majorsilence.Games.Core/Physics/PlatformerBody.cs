using System;

namespace Majorsilence.Games.Core.Physics;

/// <summary>
/// How a tile behaves for platformer collision. Solid blocks from every side;
/// OneWay only stops a body falling onto it from above (and can be dropped
/// through); Ladder is climbable and, like OneWay, standable on from above.
/// </summary>
public enum TileKind
{
    Empty,
    Solid,
    OneWay,
    Ladder
}

/// <summary>
/// Optional side-view physics for a DynamicObject: Y-axis gravity, jumping with
/// coyote time, axis-separated AABB collision against a tile grid, one-way
/// platforms, ladders, and simple water buoyancy. Attach one to
/// DynamicObject.Platformer and its Update integrates through MoveAndCollide
/// instead of the free 8-way movement + Z-hop.
///
/// Following the engine's convention (see DynamicObject.GroundZ), the body never
/// references a tilemap directly - collision knowledge is supplied externally
/// through the TileAt callback, keeping it decoupled from any particular
/// tilemap/room implementation.
/// </summary>
public class PlatformerBody
{
    /// <summary>Resolves a tile coordinate to its collision behavior. Out-of-range coordinates are the callback's concern (return Solid for level edges, Empty for open pits).</summary>
    public required Func<int, int, TileKind> TileAt { get; init; }

    public int TileWidth { get; init; } = 32;
    public int TileHeight { get; init; } = 32;

    /// <summary>World position of the tile grid's top-left corner (FlatTilemap.X/Y).</summary>
    public int MapOriginX { get; set; }
    public int MapOriginY { get; set; }

    /// <summary>
    /// Collision box, expressed relative to the owning object's top-left (X/Y).
    /// Defaults inset a 12x28 box, bottom-centered, into the standard 16x32
    /// character sprite so pixel-perfect corners don't snag.
    /// </summary>
    public int OffsetX { get; set; } = 2;
    public int OffsetY { get; set; } = 4;
    public int Width { get; set; } = 12;
    public int Height { get; set; } = 28;

    public float GravityY { get; set; } = 1500f; // world px/s^2, +down
    public float JumpVelocity { get; set; } = -480f; // world px/s, negative = up; clears a 2-tile (64px) rise at the default gravity
    public float MaxFallSpeed { get; set; } = 700f;
    public float ClimbSpeed { get; set; } = 110f;
    public float CoyoteSeconds { get; set; } = 0.1f;

    /// <summary>Current vertical velocity in world px/s, +down. Horizontal speed comes from the owner's Speed/DirectionX.</summary>
    public float VelocityY { get; set; }

    public bool OnGround { get; private set; }
    public bool OnLadder { get; private set; }

    /// <summary>
    /// World Y of the water surface, or null for a dry room. Supplied each frame
    /// by game code (it owns flood timing). A body whose feet are below it is
    /// InWater; what that means for health is the game's business, movement-wise
    /// it swims (Buoyant) or sinks slowly.
    /// </summary>
    public float? WaterLineY { get; set; }

    /// <summary>True when the owner floats (e.g. wearing a life jacket): rises toward the surface and Jump becomes a repeatable swim stroke.</summary>
    public bool Buoyant { get; set; }

    public bool InWater => WaterLineY is not null && BottomY(_lastPreciseY) > WaterLineY.Value;

    private float _coyoteTimer;
    private float _dropThroughTimer;
    private float _lastPreciseY;

    /// <summary>
    /// Begin ignoring one-way platforms for a moment so the body falls through
    /// the one it is standing on (classic Down+Jump drop). No effect on Solid.
    /// </summary>
    public void RequestDropThrough()
    {
        _dropThroughTimer = 0.25f;
        OnGround = false;
    }

    /// <summary>
    /// Jump if grounded (or just walked off a ledge, within the coyote window),
    /// detach-and-jump if on a ladder, or swim-stroke upward if buoyant in water.
    /// </summary>
    public void Jump()
    {
        if (OnLadder)
        {
            OnLadder = false;
            VelocityY = JumpVelocity;
            return;
        }

        if (OnGround || _coyoteTimer > 0f)
        {
            VelocityY = JumpVelocity;
            OnGround = false;
            _coyoteTimer = 0f;
            return;
        }

        if (Buoyant && InWater)
        {
            VelocityY = JumpVelocity * 0.6f; // repeatable stroke, weaker than a real jump
        }
    }

    /// <summary>
    /// Integrates one frame of platformer movement, mutating the owner's
    /// sub-pixel position accumulators in place. dirX is -1/0/1 horizontal
    /// intent, climbDir is -1 (up)/0/1 (down) ladder intent.
    /// </summary>
    public void MoveAndCollide(ref float preciseX, ref float preciseY, int dirX, int climbDir, float speed, float deltaTime)
    {
        _lastPreciseY = preciseY;
        if (_coyoteTimer > 0f) _coyoteTimer -= deltaTime;
        if (_dropThroughTimer > 0f) _dropThroughTimer -= deltaTime;

        UpdateLadderState(preciseX, preciseY, climbDir);

        var inWater = WaterLineY is not null && BottomY(preciseY) > WaterLineY.Value;

        // Vertical velocity: ladder overrides gravity entirely; water damps it.
        if (OnLadder)
        {
            VelocityY = climbDir * ClimbSpeed;
        }
        else if (inWater)
        {
            // Buoyant bodies experience net lift back toward the surface;
            // others sink, slowly. Both are speed-capped so water feels thick.
            VelocityY += (Buoyant ? -900f : 350f) * deltaTime;
            VelocityY = Math.Clamp(VelocityY, -140f, 140f);
        }
        else
        {
            VelocityY += GravityY * deltaTime;
            if (VelocityY > MaxFallSpeed) VelocityY = MaxFallSpeed;
        }

        var deltaX = dirX * speed * deltaTime;
        var deltaY = VelocityY * deltaTime;

        // Sub-step so fast movement (low frame rates, high fall speed) can't
        // tunnel through a tile between two checks.
        var maxStep = Math.Max(1f, Math.Min(TileWidth, TileHeight) / 2f);
        var steps = Math.Max(1, (int)MathF.Ceiling(Math.Max(MathF.Abs(deltaX), MathF.Abs(deltaY)) / maxStep));
        var wasGrounded = OnGround;
        OnGround = false;

        for (var i = 0; i < steps; i++)
        {
            MoveHorizontal(ref preciseX, preciseY, deltaX / steps);
            MoveVertical(preciseX, ref preciseY, deltaY / steps);
            if (OnGround) deltaY = 0f;
        }

        if (OnGround) _coyoteTimer = CoyoteSeconds;
        else if (wasGrounded && VelocityY >= 0f) _coyoteTimer = CoyoteSeconds; // stepped off a ledge

        _lastPreciseY = preciseY;
    }

    private void UpdateLadderState(float preciseX, float preciseY, int climbDir)
    {
        var overlapping = LadderAtCenter(preciseX, preciseY);

        if (OnLadder)
        {
            if (!overlapping) OnLadder = false;
            return;
        }

        // Grab by climbing up while overlapping a ladder, or by pressing down
        // while standing on top of one (the ladder tile right below the feet).
        if (climbDir < 0 && overlapping)
        {
            OnLadder = true;
            VelocityY = 0f;
        }
        else if (climbDir > 0 && OnGround && LadderJustBelowFeet(preciseX, preciseY))
        {
            OnLadder = true;
            OnGround = false;
            VelocityY = 0f;
        }
    }

    private bool LadderAtCenter(float preciseX, float preciseY)
    {
        var centerX = preciseX + OffsetX + Width / 2f;
        var centerY = preciseY + OffsetY + Height / 2f;
        return TileAt(ToColumn(centerX), ToRow(centerY)) == TileKind.Ladder;
    }

    private bool LadderJustBelowFeet(float preciseX, float preciseY)
    {
        var centerX = preciseX + OffsetX + Width / 2f;
        var below = BottomY(preciseY) + 1f;
        return TileAt(ToColumn(centerX), ToRow(below)) == TileKind.Ladder;
    }

    private void MoveHorizontal(ref float preciseX, float preciseY, float deltaX)
    {
        if (deltaX == 0f) return;
        preciseX += deltaX;

        var top = preciseY + OffsetY;
        var bottom = top + Height - 1f;
        if (deltaX > 0f)
        {
            var edge = preciseX + OffsetX + Width - 1f;
            var column = ToColumn(edge);
            if (AnySolidInColumn(column, top, bottom))
                preciseX = MapOriginX + column * TileWidth - Width - OffsetX;
        }
        else
        {
            var edge = preciseX + OffsetX;
            var column = ToColumn(edge);
            if (AnySolidInColumn(column, top, bottom))
                preciseX = MapOriginX + (column + 1) * TileWidth - OffsetX;
        }
    }

    private void MoveVertical(float preciseX, ref float preciseY, float deltaY)
    {
        var prevBottom = BottomY(preciseY);
        preciseY += deltaY;

        var left = preciseX + OffsetX;
        var right = left + Width - 1f;

        if (deltaY >= 0f)
        {
            // Probe the row the feet edge is entering (ToRow of the exact edge,
            // not edge-1): a body standing precisely on a surface re-lands every
            // frame instead of sinking a pixel and flickering OnGround.
            var bottom = BottomY(preciseY);
            var row = ToRow(bottom);
            var rowTop = MapOriginY + row * TileHeight;
            for (var column = ToColumn(left); column <= ToColumn(right); column++)
            {
                var kind = TileAt(column, row);
                var lands = kind switch
                {
                    TileKind.Solid => true,
                    // One-way surfaces only catch a body whose feet were at or
                    // above them before this step - never from below/inside.
                    TileKind.OneWay => prevBottom <= rowTop && _dropThroughTimer <= 0f && !OnLadder,
                    TileKind.Ladder => prevBottom <= rowTop && !OnLadder && _dropThroughTimer <= 0f,
                    _ => false
                };
                if (!lands) continue;

                preciseY = rowTop - Height - OffsetY;
                VelocityY = 0f;
                OnGround = true;
                return;
            }
        }
        else
        {
            var top = preciseY + OffsetY;
            var row = ToRow(top);
            for (var column = ToColumn(left); column <= ToColumn(right); column++)
            {
                if (TileAt(column, row) != TileKind.Solid) continue;
                preciseY = MapOriginY + (row + 1) * TileHeight - OffsetY;
                if (VelocityY < 0f) VelocityY = 0f;
                return;
            }
        }
    }

    private bool AnySolidInColumn(int column, float top, float bottom)
    {
        for (var row = ToRow(top); row <= ToRow(bottom); row++)
        {
            if (TileAt(column, row) == TileKind.Solid) return true;
        }

        return false;
    }

    private float BottomY(float preciseY) => preciseY + OffsetY + Height;
    private int ToColumn(float worldX) => (int)MathF.Floor((worldX - MapOriginX) / TileWidth);
    private int ToRow(float worldY) => (int)MathF.Floor((worldY - MapOriginY) / TileHeight);
}
