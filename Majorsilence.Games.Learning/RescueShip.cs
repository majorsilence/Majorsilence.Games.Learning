using Majorsilence.Games.Core.GameObjects;

namespace Majorsilence.Games.Learning;

public enum RescueShipState
{
    NotSummoned,
    Steaming,
    Boarding,
    Departed
}

/// <summary>
/// The RMS Carpathia's live state. The simulation (position, state, boarding
/// countdown) runs in Game whether or not the exterior is on screen; the Sprite
/// exists only while a drifting (open-ocean) room is loaded, and is rebuilt
/// from the sim position on every return to the deck - same pattern as launched
/// lifeboats, deliberately never room-anchored so ship drift and the sinking
/// waterline can't touch her.
/// PreciseX/PreciseY are the world position of the boarding gangway (the
/// center-bottom of her hull): lifeboats row toward it, swimmers and stern
/// survivors are rescued within Game's board radius of it, and the sprite is
/// laid out around it.
/// </summary>
public class RescueShip
{
    public RescueShipState State = RescueShipState.NotSummoned;
    public float PreciseX;
    public float PreciseY;
    public float BoardingSecondsRemaining;
    public Sprite? Sprite;
}
