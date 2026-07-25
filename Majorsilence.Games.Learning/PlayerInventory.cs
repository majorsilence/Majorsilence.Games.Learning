using System.Collections.Generic;

namespace Majorsilence.Games.Learning;

/// <summary>
/// One player's purchases and their live effect state. Everything here is
/// per-player - team-wide purchases (the flare gun, the pocket watch) and the
/// shared tix wallet live on Game instead. Consumables are FIFO queues: buy
/// order is use order, no selection UI needed.
/// </summary>
public class PlayerInventory
{
    public bool HasLifeJacket;
    public bool HasDeckBoots;
    public bool HasTixLauncher;
    public int Blankets;
    public readonly Queue<SnackStats> Snacks = new();
    public readonly Queue<TntSize> TntCharges = new();

    /// <summary>Active snack buff - multiplier applies to walk speed until the timer runs out.</summary>
    public float FoodBuffSecondsRemaining;
    public float FoodBuffMultiplier = 1f;

    /// <summary>Seconds a just-consumed blanket keeps this player immune to hazard tiles (and swim-slowed).</summary>
    public float HazardGraceSeconds;

    /// <summary>True while a life jacket (or blanket grace) is keeping this player alive on a water hazard tile.</summary>
    public bool IsSwimming;

    /// <summary>Spawn timer for the foam trail shown while swimming.</summary>
    public float SwimFoamTimer;
}
