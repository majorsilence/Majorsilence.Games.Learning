using System;
using SDL2;

namespace Majorsilence.Games.Learning
{
    public class Texture : IDisposable
    {
        IntPtr _texture;
        readonly Renderer _renderer;

        public SDL2.SDL.SDL_Rect Rect { get; set; }

        public Texture(Renderer renderer, Surfaces.Surface surface)
        {
            _texture = SDL.SDL_CreateTextureFromSurface(renderer, surface);
            _renderer = renderer;
            this.Rect = surface.Rect;
        }
        public static implicit operator IntPtr(Texture ap)
        {
            if (ap._disposed) return IntPtr.Zero;
            return ap._texture;
        }


        public void Render(int x, int y)
        {
            int texW = 0;
            int texH = 0;
            SDL.SDL_QueryTexture(_texture, out uint format, out int access, out texW, out texH);
            SDL.SDL_Rect dstrect2 = new SDL.SDL_Rect { x = x, y = y, w = texW, h = texH };

            SDL.SDL_RenderCopy(_renderer, _texture, IntPtr.Zero, ref dstrect2);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        bool _disposed;
        public void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.


            SDL.SDL_DestroyTexture(_texture);


            _disposed = true;
        }
    }
}

