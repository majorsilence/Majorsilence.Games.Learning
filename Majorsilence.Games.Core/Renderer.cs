using System;
using System.Runtime.InteropServices;
using SDL3;

namespace Majorsilence.Games.Core;

public class Renderer : IDisposable
{
    private IntPtr _renderer;
    private readonly Window _window;

    public Renderer(Window window)
    {
        _renderer = SDL.CreateRenderer(window, null);
        SDL.SetRenderVSync(_renderer, 1);
        _window = window;
        LogicalSize = window.Size;
    }

    /// <summary>
    /// The coordinate space rendering happens in: the window's point size divided
    /// by the zoom applied in SyncLogicalPresentationToWindow. Camera viewports and
    /// screen-space overlays should size themselves from this, not from Size (raw
    /// output pixels), or they'll disagree with the render space on zoomed/HiDPI
    /// presentations.
    /// </summary>
    public (int Width, int Height) LogicalSize { get; private set; }

    public static implicit operator IntPtr(Renderer ap)
    {
        if (ap._disposed) return IntPtr.Zero;
        return ap._renderer;
    }

    public void Clear()
    {
        SDL.RenderClear(this);
    }

    public void Present()
    {
        SDL.RenderPresent(this);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    /// <summary>
    /// Current logical (point) render size - i.e. the coordinate space game code
    /// should reason in, adjusted for active logical presentation.
    /// </summary>
    public (int Width, int Height) Size
    {
        get
        {
            SDL.GetCurrentRenderOutputSize(_renderer, out int width, out int height);
            return (width, height);
        }
    }

    /// <summary>
    /// Keeps the renderer's logical presentation matching the window's current
    /// point-size, so the visible world adapts to the window's aspect ratio
    /// (more world on a tall/mobile-shaped window, more on a wide/desktop one)
    /// instead of being pinned to one fixed design resolution with letterbox bars.
    /// Since source and destination aspect are always kept identical, Stretch mode
    /// never actually distorts anything - it only absorbs the points-to-physical-pixel
    /// HiDPI scale factor.
    /// </summary>
    /// <param name="targetShortSide">When positive, zooms the presentation so the
    /// window's shorter edge shows about this many logical pixels (never zooming
    /// below 1:1), keeping sprites a consistent, readable fraction of any screen -
    /// a 32px tile is invisible mapped 1:1 onto a 2340px phone. 0 keeps 1:1.</param>
    public void SyncLogicalPresentationToWindow(int targetShortSide = 0)
    {
        var (w, h) = _window.Size;
        var zoom = targetShortSide > 0 ? MathF.Max(1f, Math.Min(w, h) / (float)targetShortSide) : 1f;
        var logicalW = Math.Max(1, (int)MathF.Round(w / zoom));
        var logicalH = Math.Max(1, (int)MathF.Round(h / zoom));
        if (!SDL.SetRenderLogicalPresentation(_renderer, logicalW, logicalH, SDL.RendererLogicalPresentation.Stretch))
            throw new MajorsilenceException("Failed to set logical presentation: " + SDL.GetError());
        LogicalSize = (logicalW, logicalH);
    }

    public bool IsFullscreen => (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Fullscreen) != 0;
    public void SetFullscreen(bool fullscreen)
    {
        SDL.SetWindowFullscreen(_window, fullscreen);
    }

    private bool _disposed;

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
        // TODO: set large fields to null.
        SDL.DestroyRenderer(_renderer);
        _disposed = true;
    }

    /// <summary>
    /// Save a screenshot of the current screen.
    /// </summary>
    /// <param name="savePath">supports file extensions .bmp, .jpg, .png</param>
    public void SaveScreenshot(string savePath = "screenshot.bmp")
    {
        var surface = SDL.RenderReadPixels(_renderer, null);

        if (savePath.EndsWith(".bmp", StringComparison.InvariantCultureIgnoreCase))
            SDL.SaveBMP(surface, savePath);
        else if (savePath.EndsWith(".jpg", StringComparison.InvariantCultureIgnoreCase))
            Image.SaveJPG(surface, savePath, 85);
        else if (savePath.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase))
            Image.SavePNG(surface, savePath);
        else
            throw new MajorsilenceException("Only bmp, jpg, png file formats are supported.");

        SDL.DestroySurface(surface);
    }

    public void DrawColor(byte r, byte g, byte b, byte a)
    {
        SDL.SetRenderDrawColor(this, r, g, b, a);
    }
}
