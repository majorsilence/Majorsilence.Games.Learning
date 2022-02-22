using System;
using Majorsilence.Games.Learning.Surfaces;

namespace Majorsilence.Games.Learning.Textures
{
    public class PlaceholderMovingObject : Texture
    {
        public PlaceholderMovingObject(Renderer renderer, Surface surface)
            : base(renderer, surface)
        {
        }

        public override void Render(int x, int y)
        {
            base.Render(x, y);
        }
    }
}

