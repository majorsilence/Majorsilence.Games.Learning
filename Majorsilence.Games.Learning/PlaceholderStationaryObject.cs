using System;
using Majorsilence.Games.Learning.Surfaces;
using Majorsilence.Games.Learning.Textures;

namespace Majorsilence.Games.Learning
{
    public class PlaceholderStationaryObject
    {
        readonly Texture _texture;
        readonly int _x, _y;
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
}

