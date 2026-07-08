using System.Runtime.InteropServices;
using SDL3;

namespace Majorsilence.Games.Core.Surfaces;

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

    public SDL.Rect Rect { get; set; } = new SDL.Rect();

    public void ColorAsTransparent(int r, int g, int b)
    {
        var sur = Marshal.PtrToStructure<SDL.Surface>(_surface);

        var formatDetails = SDL.GetPixelFormatDetails(sur.Format);
        var rgb = SDL.MapRGB(formatDetails, IntPtr.Zero,
            Convert.ToByte(r),
            Convert.ToByte(g),
            Convert.ToByte(b));
        SDL.SetSurfaceColorKey(_surface, true, rgb);
    }
}
