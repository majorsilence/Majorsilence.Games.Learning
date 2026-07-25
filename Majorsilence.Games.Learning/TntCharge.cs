using System;
using Majorsilence.Games.Core.GameObjects;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Learning;

/// <summary>
/// A placed stick of TNT burning down its fuse, blinking faster as detonation
/// nears (frame 0 = idle, frame 1 = flash). Placement anchors it to a tile
/// (Column/Row - stable under ship drift, unlike world X/Y) which becomes the
/// blast center. The charge itself only counts down and blinks; Game owns the
/// detonation (walls, players, iceberg, shake) since only Game sees the Room.
/// </summary>
public class TntCharge : Sprite
{
    public TntSize Size { get; }
    public int Column { get; }
    public int Row { get; }
    public float FuseSecondsRemaining { get; private set; }
    public bool FuseExpired => FuseSecondsRemaining <= 0f;

    private float _blinkTimer;
    private bool _flashFrame;

    public TntCharge(SpriteSheet sheet, TntSize size, int column, int row) : base(sheet)
    {
        Size = size;
        Column = column;
        Row = row;
        FuseSecondsRemaining = FuseSeconds(size);
    }

    public static float FuseSeconds(TntSize size) => size switch
    {
        TntSize.Small => 2f,
        TntSize.Medium => 2.5f,
        _ => 3f
    };

    public static int BlastRadiusTiles(TntSize size) => size switch
    {
        TntSize.Small => 1,
        TntSize.Medium => 2,
        _ => 3
    };

    public override void Update(float deltaTime)
    {
        FuseSecondsRemaining -= deltaTime;

        _blinkTimer -= deltaTime;
        if (_blinkTimer <= 0f)
        {
            _flashFrame = !_flashFrame;
            _blinkTimer = Math.Clamp(FuseSecondsRemaining / 5f, 0.06f, 0.4f);
            SetFrame(_flashFrame ? 1 : 0);
        }
    }
}
