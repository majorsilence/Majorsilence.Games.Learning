using System;
using System.Runtime.InteropServices;

namespace Majorsilence.Games.Learning
{
    internal class AutoPinner : SafeHandle, IDisposable
    {
        // https://stackoverflow.com/questions/537573/how-to-get-intptr-from-byte-in-c-sharp/537652

        GCHandle _pinnedArray;

        public override bool IsInvalid => _disposed;

        public AutoPinner(Object obj) : base(IntPtr.Zero, true)
        {
            _pinnedArray = GCHandle.Alloc(obj, GCHandleType.Pinned);
        }

        public static implicit operator IntPtr(AutoPinner ap)
        {
            return ap._pinnedArray.AddrOfPinnedObject();
        }
        public new void Dispose()
        {
            Dispose(true);
        }

        bool _disposed;
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            base.Dispose();
            _pinnedArray.Free();

            _disposed = true;
        }

        protected override bool ReleaseHandle()
        {
            Dispose();
            return true;
        }
    }
}

