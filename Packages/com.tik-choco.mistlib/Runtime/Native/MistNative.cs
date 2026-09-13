using System;
using System.Runtime.InteropServices;

namespace TikChoco.MistLib
{
    internal static class MistNative
    {
        private const string LibraryName = "mistlib";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void EventCallback(
            uint eventType,
            IntPtr room,
            UIntPtr roomLength,
            IntPtr from,
            UIntPtr fromLength,
            IntPtr data,
            UIntPtr dataLength);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void LogCallback(uint level, IntPtr data, UIntPtr dataLength);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint get_version(IntPtr buffer, UIntPtr bufferLength);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint get_build_info(IntPtr buffer, UIntPtr bufferLength);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void register_event_callback_v2(EventCallback callback);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void register_log_callback(LogCallback callback);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void init(
            IntPtr nodeId,
            UIntPtr nodeIdLength,
            IntPtr signalingUrl,
            UIntPtr signalingUrlLength);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool init_with_config(
            IntPtr nodeId,
            UIntPtr nodeIdLength,
            IntPtr configJson,
            UIntPtr configJsonLength);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void join_room(IntPtr roomId, UIntPtr roomIdLength);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void leave_room_id(IntPtr roomId, UIntPtr roomIdLength);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void update_position(float x, float y, float z);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void update_position_in_room(
            IntPtr roomId,
            UIntPtr roomIdLength,
            float x,
            float y,
            float z);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void send_message(
            IntPtr target,
            UIntPtr targetLength,
            IntPtr data,
            UIntPtr dataLength,
            uint deliveryMethod);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void send_message_in_room(
            IntPtr roomId,
            UIntPtr roomIdLength,
            IntPtr target,
            UIntPtr targetLength,
            IntPtr data,
            UIntPtr dataLength,
            uint deliveryMethod);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void set_config(IntPtr data, UIntPtr dataLength);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint get_config(IntPtr buffer, UIntPtr bufferLength);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint get_stats(IntPtr buffer, UIntPtr bufferLength);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint storage_add(
            IntPtr name,
            UIntPtr nameLength,
            IntPtr data,
            UIntPtr dataLength,
            IntPtr cidBuffer,
            UIntPtr cidBufferLength);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint storage_add_at(
            IntPtr name,
            UIntPtr nameLength,
            IntPtr data,
            UIntPtr dataLength,
            float x,
            float y,
            float z,
            IntPtr cidBuffer,
            UIntPtr cidBufferLength);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint storage_get(
            IntPtr cid,
            UIntPtr cidLength,
            IntPtr buffer,
            UIntPtr bufferLength);
    }
}
