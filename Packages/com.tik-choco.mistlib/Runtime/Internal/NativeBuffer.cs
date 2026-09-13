using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TikChoco.MistLib
{
    internal static class NativeBuffer
    {
        private static readonly byte[] EmptySentinel = new byte[1];

        internal static byte[] Utf8(string value)
        {
            return string.IsNullOrEmpty(value) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(value);
        }

        internal static TResult WithInput<TResult>(byte[] bytes, Func<IntPtr, UIntPtr, TResult> call)
        {
            var actual = bytes ?? Array.Empty<byte>();
            var storage = actual.Length == 0 ? EmptySentinel : actual;
            var handle = GCHandle.Alloc(storage, GCHandleType.Pinned);
            try
            {
                // A non-null pointer is deliberately passed even when length is zero.
                return call(handle.AddrOfPinnedObject(), (UIntPtr)actual.Length);
            }
            finally
            {
                handle.Free();
            }
        }

        internal static void WithInput(byte[] bytes, Action<IntPtr, UIntPtr> call)
        {
            WithInput(bytes, (pointer, length) =>
            {
                call(pointer, length);
                return 0;
            });
        }

        internal static TResult WithOutput<TResult>(byte[] bytes, UIntPtr exposedLength, Func<IntPtr, UIntPtr, TResult> call)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("Output storage must contain at least one byte.", nameof(bytes));
            }

            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return call(handle.AddrOfPinnedObject(), exposedLength);
            }
            finally
            {
                handle.Free();
            }
        }

        internal static string ReadSizedUtf8(Func<IntPtr, UIntPtr, uint> nativeCall)
        {
            var probe = new byte[1];
            var required = WithOutput(probe, UIntPtr.Zero, nativeCall);
            if (required == 0)
            {
                return string.Empty;
            }

            var buffer = new byte[checked((int)required)];
            var written = WithOutput(buffer, (UIntPtr)buffer.Length, nativeCall);
            if (written == 0 || written > buffer.Length)
            {
                throw new MistNativeException("The native library returned an invalid UTF-8 buffer length.");
            }

            return Encoding.UTF8.GetString(buffer, 0, checked((int)written));
        }

        internal static string ReadFixedUtf8(int capacity, Func<IntPtr, UIntPtr, uint> nativeCall)
        {
            var buffer = new byte[capacity];
            var written = WithOutput(buffer, (UIntPtr)buffer.Length, nativeCall);
            if (written == 0)
            {
                return string.Empty;
            }
            if (written > buffer.Length)
            {
                throw new MistNativeException("The native library returned a length larger than the supplied buffer.");
            }

            return Encoding.UTF8.GetString(buffer, 0, checked((int)written));
        }
    }
}
