using System;
using System.Runtime.InteropServices;
using SDL2;

namespace Majorsilence.Games.Learning
{
    public class Renderer : IDisposable
    {
        IntPtr _renderer;

        public Renderer(IntPtr window)
        {
            _renderer = SDL.SDL_CreateRenderer(window, -1, 0);
        }
        public static implicit operator IntPtr(Renderer ap)
        {
            if (ap._disposed) return IntPtr.Zero;
            return ap._renderer;
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


            SDL.SDL_DestroyRenderer(_renderer);


            _disposed = true;
        }

        public void SaveScreenshot(string savePath = "screenshot.bmp")
        {
            uint format = SDL.SDL_PIXELFORMAT_RGBX8888;
            int width = 0;
            int height = 0;

            SDL.SDL_GetRendererOutputSize(_renderer, out width, out height);
            var surface = SDL.SDL_CreateRGBSurfaceWithFormat(0, width, height, 32, format);
      
            SDL.SDL_Rect rect = new SDL.SDL_Rect()
            {
                x = 0,
                y = 0,
                w = width,
                h = height
            };


            var sur = Marshal.PtrToStructure<SDL.SDL_Surface>(surface);

            SDL.SDL_RenderReadPixels(_renderer, ref rect, format, sur.pixels, sur.pitch);
            SDL.SDL_SaveBMP(surface, savePath);
            SDL.SDL_FreeSurface(surface);
        }
    }
}

