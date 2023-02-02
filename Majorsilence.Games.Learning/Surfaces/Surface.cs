using System.Runtime.InteropServices;
using SDL2;

namespace Majorsilence.Games.Learning.Surfaces;

public abstract class Surface : IDisposable
{
    protected bool _disposed;
    public abstract void Dispose();
    protected IntPtr _surface = IntPtr.Zero;

    public static implicit operator IntPtr(Surface ap)
    {
        if (ap._disposed) return IntPtr.Zero;
        return ap._surface;
    }

    public SDL2.SDL.SDL_Rect Rect { get; set; } = new SDL2.SDL.SDL_Rect();

    public void ColorAsTransparent(int r, int g, int b)
    {
        var sur = Marshal.PtrToStructure<SDL.SDL_Surface>(_surface);

        var rgb = SDL2.SDL.SDL_MapRGB(sur.format,
            Convert.ToByte(r),
            Convert.ToByte(g),
            Convert.ToByte(b));
        SDL2.SDL.SDL_SetColorKey(_surface, (int)SDL2.SDL.SDL_bool.SDL_TRUE,
            rgb);
    }
}