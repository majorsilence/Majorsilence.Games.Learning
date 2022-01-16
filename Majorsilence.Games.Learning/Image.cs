using System;
using System.Runtime.InteropServices;
using SDL2;
using SixLabors.ImageSharp;

namespace Majorsilence.Games.Learning
{
    public class Image : IDisposable
    {

      
        IntPtr _surface;
        string _tempPath="";


        public Image(string path)
        {

            if (path.ToLower().Trim().EndsWith(".bmp"))
            {
                _surface = SDL.SDL_LoadBMP(path);
                return;
            }

            using (var image2 = SixLabors.ImageSharp.Image.Load(path))
            {

                var bmpEncoder = new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder();

                _tempPath = System.IO.Path.GetTempFileName();
                image2.SaveAsBmp(_tempPath, bmpEncoder);

                var bytes = System.IO.File.ReadAllBytes(path);

                _surface = SDL.SDL_LoadBMP(_tempPath);
            }
        }
        public static implicit operator IntPtr(Image ap)
        {
            if (ap._disposed) return IntPtr.Zero;
            return ap._surface;
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

