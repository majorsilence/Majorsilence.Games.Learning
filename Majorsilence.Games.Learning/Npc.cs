using System;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Input;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Learning;

/// <summary>
/// A crew member standing at a post (bridge, engine room, crow's nest): wanders a
/// short leash around its spawn point when idle, and has a rotating line of
/// dialogue shown when a player talks to it (see Game.CheckNpcInteractionAndRoleBonus).
/// Role is a free-form string ("captain", "engineer", "watcher") matching the
/// level entity's "role" property - Game looks it up to decide takeover effects.
/// Wall collision for the wander is handled externally by Game (same trial-move-
/// then-revert pattern used for players), since only Game knows about the Room.
/// </summary>
public class Npc : DynamicObject
{
    private static readonly Random Rng = new();

    private const float LeashPixels = 18f;
    private const float WanderSpeed = 20f;

    public string Role { get; }
    public string[] Lines { get; }

    /// <summary>Last position confirmed clear of walls - Game reverts here if a wander step lands on a solid tile.</summary>
    public int LastGoodX;
    public int LastGoodY;

    private int _anchorX;
    private int _anchorY;
    private float _stateTimer;
    private int _lineIndex = -1;

    public Npc(SpriteSheet sheet, string role, string[] lines) : base(sheet)
    {
        Role = role;
        Lines = lines;
        Speed = WanderSpeed;
        AnimateOnlyWhenMoving = true;
        SetAnimation(new Animation(frames: new[] { 0, 1, 2, 3 }, frameDurationMs: 220));
    }

    /// <summary>Anchors the wander leash to this NPC's placed position - call once, right after placement.</summary>
    public void SetHome(int x, int y)
    {
        SnapTo(x, y);
        _anchorX = x;
        _anchorY = y;
        LastGoodX = x;
        LastGoodY = y;
    }

    /// <summary>
    /// The next dialogue line for this NPC, cycling through Lines so repeated talk
    /// doesn't just echo the same sentence back every time.
    /// </summary>
    public string NextLine()
    {
        _lineIndex = (_lineIndex + 1) % Lines.Length;
        return Lines[_lineIndex];
    }

    public override void Update(float deltaTime)
    {
        _stateTimer -= deltaTime;
        if (_stateTimer <= 0f)
        {
            if (Rng.NextDouble() < 0.4)
            {
                DirectionX = HorizontalDirection.None;
                DirectionY = VerticalDirection.None;
                _stateTimer = 1f + (float)Rng.NextDouble() * 1.5f;
            }
            else
            {
                DirectionX = (HorizontalDirection)(Rng.Next(3) - 1);
                DirectionY = (VerticalDirection)(Rng.Next(3) - 1);
                _stateTimer = 0.5f + (float)Rng.NextDouble() * 0.8f;
            }
        }

        // Stay on a short leash around the post rather than roaming the whole room.
        var dx = X - _anchorX;
        var dy = Y - _anchorY;
        if (dx * dx + dy * dy > LeashPixels * LeashPixels)
        {
            DirectionX = dx > 0 ? HorizontalDirection.Left : dx < 0 ? HorizontalDirection.Right : HorizontalDirection.None;
            DirectionY = dy > 0 ? VerticalDirection.Up : dy < 0 ? VerticalDirection.Down : VerticalDirection.None;
            _stateTimer = 0.4f;
        }

        base.Update(deltaTime);
    }
}
