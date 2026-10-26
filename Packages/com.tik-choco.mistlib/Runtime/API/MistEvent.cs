using System;

namespace TikChoco.MistLib
{
    public enum MistEventType : uint
    {
        Raw = 0,
        Overlay = 1,
        PeerConnected = 2,
        PeerDisconnected = 3,
        Neighbors = 4,
        AreaOfInterestEntered = 5,
        AreaOfInterestLeft = 6,
        NodePositionUpdated = 7,
        AreaOfInterestNodes = 8,
        AllConnectionsLost = 9,
        RoomJoined = 10,
        RoomJoinFailed = 11,
        RoomLeft = 12
    }

    public sealed class MistEvent
    {
        public MistEvent(MistEventType type, string roomId, string fromNodeId, byte[] data)
        {
            Type = type;
            RoomId = roomId ?? string.Empty;
            FromNodeId = fromNodeId ?? string.Empty;
            Data = data ?? Array.Empty<byte>();
        }

        public MistEventType Type { get; }
        public string RoomId { get; }
        public string FromNodeId { get; }
        public byte[] Data { get; }
    }

    public enum MistLogLevel : uint
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    public sealed class MistLogEvent
    {
        public MistLogEvent(MistLogLevel level, string message)
        {
            Level = level;
            Message = message ?? string.Empty;
        }

        public MistLogLevel Level { get; }
        public string Message { get; }
    }

    public enum MistDeliveryMethod : uint
    {
        ReliableOrdered = 0,
        UnreliableOrdered = 1,
        Unreliable = 2,
        // Falls back to reliable ordered delivery on peers without support.
        ReliableUnordered = 3
    }
}
