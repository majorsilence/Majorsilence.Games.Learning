using System;
using SDL2;

namespace Majorsilence.Games.Learning
{
    public class Texture : IDisposable
    {
        IntPtr _texture;

        public SDL2.SDL.SDL_Rect Rect { get; set; }

        public Texture(Renderer renderer, Surface surface)
        {
            _texture = SDL.SDL_CreateTextureFromSurface(renderer, surface);
            this.Rect = surface.Rect;
        }
        public static implicit operator IntPtr(Texture ap)
        {
            if (ap._disposed) return IntPtr.Zero;
            return ap._texture;
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

