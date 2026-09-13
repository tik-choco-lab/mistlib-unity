using System.Text;
using TikChoco.MistLib;
using UnityEngine;

namespace TikChoco.MistLib.Samples
{
    public sealed class BasicRoomExample : MonoBehaviour
    {
        [Tooltip("Leave empty for a generated unique ID.")]
        [SerializeField] private string nodeId;
        [SerializeField] private string roomId = "lobby";
        [Tooltip("Leave empty to run on the engine defaults. Only the keys you set are overridden.")]
        [SerializeField, TextArea(6, 20)] private string configJson;

        private MistClient client;

        private void Start()
        {
            client = new MistClient();
            client.EventReceived += OnMistEvent;
            client.LogReceived += OnMistLog;
            client.Initialize(new MistClientOptions
            {
                NodeId = nodeId,
                ConfigJson = configJson,
                InitialRoom = roomId
            });
            Debug.Log($"MistLib node {client.NodeId} joined {roomId}.");
        }

        private void Update()
        {
            client?.PumpCallbacks();
        }

        private void OnDestroy()
        {
            client?.Dispose();
            client = null;
        }

        [ContextMenu("Broadcast Hello")]
        private void BroadcastHello()
        {
            client?.SendMessage(
                string.Empty,
                Encoding.UTF8.GetBytes("hello from Unity"),
                MistDeliveryMethod.ReliableOrdered,
                roomId);
        }

        private static void OnMistEvent(MistEvent item)
        {
            Debug.Log($"MistLib event={item.Type} room={item.RoomId} from={item.FromNodeId} bytes={item.Data.Length}");
        }

        private static void OnMistLog(MistLogEvent item)
        {
            Debug.Log($"MistLib [{item.Level}] {item.Message}");
        }
    }
}
