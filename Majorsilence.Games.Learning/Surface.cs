namespace Majorsilence.Games.Learning
{
    public abstract class Surface : IDisposable
    {
        protected bool _disposed;
        public abstract void Dispose();
        protected IntPtr _surface=IntPtr.Zero;
        public static implicit operator IntPtr(Surface ap)
        {
            if (ap._disposed) return IntPtr.Zero;
            return ap._surface;
        }

        public  SDL2.SDL.SDL_Rect Rect { get; set; } = new SDL2.SDL.SDL_Rect();
    }
}

