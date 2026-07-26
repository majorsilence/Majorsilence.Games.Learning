using Majorsilence.Games.Core;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Rendering;

namespace Majorsilence.Games.Learning;

/// <summary>
/// Renders the rising floodwater in a platformer room as a translucent band
/// from Room.WaterLineY down to the bottom of the map - a world-space hazard
/// boundary, not a tile grid, so it reads as continuous water climbing through
/// a tall vertical shaft rather than an instant tile swap. Present only in
/// platformer RoomObjects (see Room's constructor); torn down with the room
/// like any other prop, and simply draws nothing while the room is dry.
/// </summary>
public class WaterOverlay : GameObject
{
    private readonly Renderer _renderer;
    private readonly Room _room;

    public WaterOverlay(Renderer renderer, Room room)
    {
        _renderer = renderer;
        _room = room;
        ZIndex = 2;
    }

    public override void Update(float deltaTime)
    {
    }

    public override void Render(Camera camera)
    {
        if (_room.WaterLineY is not { } waterY) return;
        if (_room.FlatMap is null) return;

        var bottom = _room.FlatMap.Y + _room.FlatMap.PixelHeight;
        if (waterY >= bottom) return;

        var (screenX, screenTop) = camera.WorldToScreen(_room.FlatMap.X, waterY);
        var (_, screenBottom) = camera.WorldToScreen(_room.FlatMap.X, bottom);
        _renderer.FillRect(screenX, screenTop, _room.FlatMap.PixelWidth, screenBottom - screenTop, 30, 90, 140, 150);
    }
}
