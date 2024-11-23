﻿using System;
using Majorsilence.Games.Core.Textures;

namespace Majorsilence.Games.Core;

public class PlaceholderStationaryObject
{
    private readonly Texture _texture;
    private readonly int _x, _y;

    public PlaceholderStationaryObject(Texture texture, int x, int y)
    {
        _texture = texture;
        _x = x;
        _y = y;
    }

    /// <summary>
    /// Higher value is on top
    /// </summary>
    public int ZIndex { get; set; } = 1;

    public void Render()
    {
        _texture.Render(_x, _y);
    }
}