using System;
using System.Runtime.InteropServices;
using SDL2;
using SixLabors.ImageSharp;

namespace Majorsilence.Games.Learning
{

    public class ImageIS : Surface, IDisposable
    {

        string _tempPath = "";


        public ImageIS(string path)
        {

            if (path.ToLower().Trim().EndsWith(".bmp"))
            {
                _surface = SDL.SDL_LoadBMP(path);
                SetRect();
                return;
            }

            using (var image2 = SixLabors.ImageSharp.Image.Load(path))
            {

                var bmpEncoder = new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder();

                _tempPath = System.IO.Path.GetTempFileName();
                image2.SaveAsBmp(_tempPath, bmpEncoder);

                var bytes = System.IO.File.ReadAllBytes(path);

                _surface = SDL.SDL_LoadBMP(_tempPath);
                SetRect();
            }
        }

        private void SetRect()
        {
            var sur = Marshal.PtrToStructure<SDL.SDL_Surface>(_surface);
            var rect = this.Rect;
            rect.h = sur.h;
            rect.w = sur.w;
            rect.x = 0;
            rect.y = 0;
            this.Rect = rect;
        }

        public override void Dispose()
        {
            Dispose(true);
        }


        public void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                if (!string.IsNullOrWhiteSpace(_tempPath) && System.IO.File.Exists(_tempPath))
                {
                    System.IO.File.Delete(_tempPath);
                }
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.


            SDL.SDL_FreeSurface(_surface);


            _disposed = true;
        }
    }
}

