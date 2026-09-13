using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace TikChoco.MistLib
{
    internal static class MistNativeCallbacks
    {
        private static readonly ConcurrentQueue<MistEvent> Events = new ConcurrentQueue<MistEvent>();
        private static readonly ConcurrentQueue<MistLogEvent> Logs = new ConcurrentQueue<MistLogEvent>();
        private static readonly MistNative.EventCallback EventCallback = OnEvent;
        private static readonly MistNative.LogCallback LogCallback = OnLog;
        private static bool installed;
        private static volatile bool accepting;

        internal static void Activate()
        {
            if (!installed)
            {
                MistNative.register_event_callback_v2(EventCallback);
                MistNative.register_log_callback(LogCallback);
                installed = true;
            }

            Clear();
            accepting = true;
        }

        internal static void Deactivate()
        {
            accepting = false;
            Clear();
        }

        internal static void Drain(MistClient client, int maximum)
        {
            var count = 0;
            while (count < maximum && Events.TryDequeue(out var item))
            {
                client.Dispatch(item);
                count++;
            }

            count = 0;
            while (count < maximum && Logs.TryDequeue(out var item))
            {
                client.Dispatch(item);
                count++;
            }
        }

        internal static void Clear()
        {
            while (Events.TryDequeue(out _))
            {
            }
            while (Logs.TryDequeue(out _))
            {
            }
        }

        [AOT.MonoPInvokeCallback(typeof(MistNative.EventCallback))]
        private static void OnEvent(
            uint eventType,
            IntPtr room,
            UIntPtr roomLength,
            IntPtr from,
            UIntPtr fromLength,
            IntPtr data,
            UIntPtr dataLength)
        {
            if (!accepting)
            {
                return;
            }

            try
            {
                Events.Enqueue(new MistEvent(
                    (MistEventType)eventType,
                    CopyUtf8(room, roomLength),
                    CopyUtf8(from, fromLength),
                    CopyBytes(data, dataLength)));
            }
            catch (Exception exception)
            {
                Logs.Enqueue(new MistLogEvent(MistLogLevel.Error, "MistLib event callback failed: " + exception));
            }
        }

        [AOT.MonoPInvokeCallback(typeof(MistNative.LogCallback))]
        private static void OnLog(uint level, IntPtr data, UIntPtr dataLength)
        {
            if (!accepting)
            {
                return;
            }

            try
            {
                Logs.Enqueue(new MistLogEvent((MistLogLevel)level, CopyUtf8(data, dataLength)));
            }
            catch (Exception exception)
            {
                Logs.Enqueue(new MistLogEvent(MistLogLevel.Error, "MistLib log callback failed: " + exception));
            }
        }

        private static string CopyUtf8(IntPtr pointer, UIntPtr nativeLength)
        {
            var bytes = CopyBytes(pointer, nativeLength);
            return bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);
        }

        private static byte[] CopyBytes(IntPtr pointer, UIntPtr nativeLength)
        {
            var length64 = nativeLength.ToUInt64();
            if (length64 > int.MaxValue)
            {
                throw new MistNativeException("A native callback payload exceeded the managed size limit.");
            }

            var length = checked((int)length64);
            if (length == 0)
            {
                return Array.Empty<byte>();
            }
            if (pointer == IntPtr.Zero)
            {
                throw new MistNativeException("A native callback returned a null pointer with a non-zero length.");
            }

            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return bytes;
        }
    }
}
