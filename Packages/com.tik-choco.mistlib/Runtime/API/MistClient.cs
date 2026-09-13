using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace TikChoco.MistLib
{
    public sealed class MistClient : IDisposable
    {
        private const int JsonBufferCapacity = 64 * 1024;
        private const int CidBufferCapacity = 256;
        private static readonly object GlobalLock = new object();
        private static MistClient activeClient;

        private readonly HashSet<string> joinedRooms = new HashSet<string>(StringComparer.Ordinal);
        private int ownerThreadId;
        private bool disposed;

        public event Action<MistEvent> EventReceived;
        public event Action<MistLogEvent> LogReceived;

        public bool IsInitialized { get; private set; }
        public string NodeId { get; private set; }
        public string NativeVersion { get; private set; }
        public string NativeBuildInfoJson { get; private set; }

        public void Initialize(MistClientOptions options)
        {
            ThrowIfDisposed();
            if (IsInitialized)
            {
                throw new InvalidOperationException("This MistClient is already initialized.");
            }
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            options.Validate();

            lock (GlobalLock)
            {
                if (activeClient != null && activeClient != this)
                {
                    throw new InvalidOperationException(
                        "mistlib-native is process-global; only one initialized MistClient is supported.");
                }
                activeClient = this;
            }

            try
            {
                EnsureSupportedRuntime();
                ownerThreadId = Thread.CurrentThread.ManagedThreadId;
                NativeVersion = NativeBuffer.ReadSizedUtf8(MistNative.get_version);
                NativeBuildInfoJson = NativeBuffer.ReadSizedUtf8(MistNative.get_build_info);
                if (!string.Equals(NativeVersion, MistVersion.Engine, StringComparison.Ordinal))
                {
                    throw new MistNativeException(
                        "MistLib native version " + NativeVersion +
                        " does not match the package engine version " + MistVersion.Engine + ".");
                }

                MistNativeCallbacks.Activate();
                NodeId = options.ResolveNodeId();

                var nodeBytes = NativeBuffer.Utf8(NodeId);
                if (!options.UsesSignalingUrl)
                {
                    var configBytes = NativeBuffer.Utf8(options.ResolveConfigJson());
                    var accepted = NativeBuffer.WithInput(nodeBytes, (nodePointer, nodeLength) =>
                        NativeBuffer.WithInput(configBytes, (configPointer, configLength) =>
                            MistNative.init_with_config(nodePointer, nodeLength, configPointer, configLength)));
                    if (!accepted)
                    {
                        throw new MistNativeException(
                            "The native engine rejected ConfigJson. A Nostr configuration needs a relay " +
                            "source and, when it names public relays, its own inviteSalt and inviteCode.");
                    }
                }
                else
                {
                    var urlBytes = NativeBuffer.Utf8(options.SignalingUrl);
                    NativeBuffer.WithInput(nodeBytes, (nodePointer, nodeLength) =>
                        NativeBuffer.WithInput(urlBytes, (urlPointer, urlLength) =>
                            MistNative.init(nodePointer, nodeLength, urlPointer, urlLength)));
                }

                IsInitialized = true;
                if (!string.IsNullOrWhiteSpace(options.InitialRoom))
                {
                    JoinRoom(options.InitialRoom);
                }
            }
            catch
            {
                MistNativeCallbacks.Deactivate();
                lock (GlobalLock)
                {
                    if (activeClient == this)
                    {
                        activeClient = null;
                    }
                }
                throw;
            }
        }

        public void JoinRoom(string roomId)
        {
            RequireInitialized();
            RequireNonEmpty(roomId, nameof(roomId));
            var bytes = NativeBuffer.Utf8(roomId);
            NativeBuffer.WithInput(bytes, MistNative.join_room);
            joinedRooms.Add(roomId);
        }

        public void LeaveRoom(string roomId)
        {
            RequireInitialized();
            RequireNonEmpty(roomId, nameof(roomId));
            var bytes = NativeBuffer.Utf8(roomId);
            NativeBuffer.WithInput(bytes, MistNative.leave_room_id);
            joinedRooms.Remove(roomId);
        }

        public void UpdatePosition(Vector3 position, string roomId = null)
        {
            RequireInitialized();
            if (string.IsNullOrEmpty(roomId))
            {
                MistNative.update_position(position.x, position.y, position.z);
                return;
            }

            var roomBytes = NativeBuffer.Utf8(roomId);
            NativeBuffer.WithInput(roomBytes, (pointer, length) =>
                MistNative.update_position_in_room(pointer, length, position.x, position.y, position.z));
        }

        public void SendMessage(
            string targetNodeId,
            byte[] data,
            MistDeliveryMethod deliveryMethod = MistDeliveryMethod.Unreliable,
            string roomId = null)
        {
            RequireInitialized();
            var targetBytes = NativeBuffer.Utf8(targetNodeId);
            var payload = data ?? Array.Empty<byte>();

            if (string.IsNullOrEmpty(roomId))
            {
                NativeBuffer.WithInput(targetBytes, (targetPointer, targetLength) =>
                    NativeBuffer.WithInput(payload, (dataPointer, dataLength) =>
                        MistNative.send_message(
                            targetPointer,
                            targetLength,
                            dataPointer,
                            dataLength,
                            (uint)deliveryMethod)));
                return;
            }

            var roomBytes = NativeBuffer.Utf8(roomId);
            NativeBuffer.WithInput(roomBytes, (roomPointer, roomLength) =>
                NativeBuffer.WithInput(targetBytes, (targetPointer, targetLength) =>
                    NativeBuffer.WithInput(payload, (dataPointer, dataLength) =>
                        MistNative.send_message_in_room(
                            roomPointer,
                            roomLength,
                            targetPointer,
                            targetLength,
                            dataPointer,
                            dataLength,
                            (uint)deliveryMethod))));
        }

        public string GetConfigJson()
        {
            RequireInitialized();
            return NativeBuffer.ReadFixedUtf8(JsonBufferCapacity, MistNative.get_config);
        }

        public void SetConfigJson(string configJson)
        {
            RequireInitialized();
            RequireNonEmpty(configJson, nameof(configJson));
            var bytes = NativeBuffer.Utf8(configJson);
            NativeBuffer.WithInput(bytes, MistNative.set_config);
        }

        public string GetStatsJson()
        {
            RequireInitialized();
            return NativeBuffer.ReadFixedUtf8(JsonBufferCapacity, MistNative.get_stats);
        }

        public string StorageAdd(string name, byte[] data)
        {
            return StorageAddInternal(name, data, null);
        }

        public string StorageAddAt(string name, byte[] data, Vector3 position)
        {
            return StorageAddInternal(name, data, position);
        }

        public byte[] StorageGet(string cid)
        {
            RequireInitialized();
            RequireNonEmpty(cid, nameof(cid));
            var cidBytes = NativeBuffer.Utf8(cid);
            var probe = new byte[1];
            var required = NativeBuffer.WithInput(cidBytes, (cidPointer, cidLength) =>
                NativeBuffer.WithOutput(probe, UIntPtr.Zero, (bufferPointer, bufferLength) =>
                    MistNative.storage_get(cidPointer, cidLength, bufferPointer, bufferLength)));
            if (required == 0)
            {
                return null;
            }

            var buffer = new byte[checked((int)required)];
            var written = NativeBuffer.WithInput(cidBytes, (cidPointer, cidLength) =>
                NativeBuffer.WithOutput(buffer, (UIntPtr)buffer.Length, (bufferPointer, bufferLength) =>
                    MistNative.storage_get(cidPointer, cidLength, bufferPointer, bufferLength)));
            if (written == 0 || written > buffer.Length)
            {
                return null;
            }
            if (written == buffer.Length)
            {
                return buffer;
            }

            var result = new byte[checked((int)written)];
            Array.Copy(buffer, result, result.Length);
            return result;
        }

        public void PumpCallbacks(int maximumPerQueue = 1024)
        {
            RequireInitialized();
            if (Thread.CurrentThread.ManagedThreadId != ownerThreadId)
            {
                throw new InvalidOperationException("PumpCallbacks must run on the thread that initialized MistClient.");
            }
            if (maximumPerQueue <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumPerQueue));
            }

            MistNativeCallbacks.Drain(this, maximumPerQueue);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            try
            {
                if (IsInitialized)
                {
                    foreach (var roomId in new List<string>(joinedRooms))
                    {
                        var bytes = NativeBuffer.Utf8(roomId);
                        NativeBuffer.WithInput(bytes, MistNative.leave_room_id);
                    }
                }
            }
            finally
            {
                joinedRooms.Clear();
                IsInitialized = false;
                MistNativeCallbacks.Deactivate();
                lock (GlobalLock)
                {
                    if (activeClient == this)
                    {
                        activeClient = null;
                    }
                }
                disposed = true;
            }
        }

        internal void Dispatch(MistEvent item)
        {
            EventReceived?.Invoke(item);
        }

        internal void Dispatch(MistLogEvent item)
        {
            LogReceived?.Invoke(item);
        }

        private string StorageAddInternal(string name, byte[] data, Vector3? position)
        {
            RequireInitialized();
            var nameBytes = NativeBuffer.Utf8(name);
            var payload = data ?? Array.Empty<byte>();
            var cidBuffer = new byte[CidBufferCapacity];

            var written = NativeBuffer.WithInput(nameBytes, (namePointer, nameLength) =>
                NativeBuffer.WithInput(payload, (dataPointer, dataLength) =>
                    NativeBuffer.WithOutput(cidBuffer, (UIntPtr)cidBuffer.Length, (cidPointer, cidLength) =>
                        position.HasValue
                            ? MistNative.storage_add_at(
                                namePointer,
                                nameLength,
                                dataPointer,
                                dataLength,
                                position.Value.x,
                                position.Value.y,
                                position.Value.z,
                                cidPointer,
                                cidLength)
                            : MistNative.storage_add(
                                namePointer,
                                nameLength,
                                dataPointer,
                                dataLength,
                                cidPointer,
                                cidLength))));

            if (written == 0 || written > cidBuffer.Length)
            {
                return null;
            }
            return System.Text.Encoding.UTF8.GetString(cidBuffer, 0, checked((int)written));
        }

        private void RequireInitialized()
        {
            ThrowIfDisposed();
            if (!IsInitialized)
            {
                throw new InvalidOperationException("MistClient is not initialized.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MistClient));
            }
        }

        private static void RequireNonEmpty(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", name);
            }
        }

        private static void EnsureSupportedRuntime()
        {
            var architecture = RuntimeInformation.ProcessArchitecture;
            var supported =
                (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && architecture == Architecture.X64) ||
                (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && architecture == Architecture.X64) ||
                (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && architecture == Architecture.Arm64);

            if (!supported)
            {
                throw new PlatformNotSupportedException(
                    "MistLib 0.6.2 supports Windows x86_64, Linux x86_64, and macOS ARM64 only. " +
                    "Current process architecture: " + architecture + ".");
            }
        }
    }
}
