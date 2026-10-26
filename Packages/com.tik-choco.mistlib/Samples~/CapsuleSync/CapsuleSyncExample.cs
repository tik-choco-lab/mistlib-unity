using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TikChoco.MistLib.Samples
{
    /// <summary>
    /// Two capsules in one MistLib room: the blue one is this process, every orange one is a peer.
    /// Run the sample in two processes (Editor plus a standalone build) and drive the blue capsule
    /// with WASD; the same motion appears on the other screen as an orange capsule, with the
    /// coordinates both sides hold printed above each capsule.
    ///
    /// Add this component to an empty GameObject and press Play. The ground, camera, light, and
    /// capsules are all created at runtime, so the sample needs no scene of its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CapsuleSyncExample : MonoBehaviour
    {
        [Header("Room")]
        [Tooltip("Leave empty for a generated unique ID.")]
        [SerializeField] private string nodeId;
        [Tooltip("Both processes must use the same room to see each other.")]
        [SerializeField] private string roomId = "capsule-sync";
        [Tooltip("Leave empty to run on the engine defaults. Only the keys you set are overridden.")]
        [SerializeField, TextArea(4, 12)] private string configJson;

        [Header("Capsules")]
        [SerializeField] private float moveSpeed = 4f;
        [Tooltip("Seconds between position publishes. The engine's own AOI update is about 1s.")]
        [SerializeField] private float sendInterval = 0.1f;
        [Tooltip("Higher values pull a remote capsule to its last reported position faster.")]
        [SerializeField] private float smoothing = 10f;
        [Tooltip("A silent capsule is hidden after this many seconds and removed after the same time hidden.")]
        [SerializeField] private float peerTimeoutSeconds = 15f;

        private static readonly Color LocalColor = new Color(0.29f, 0.62f, 1f);
        private static readonly Color RemoteColor = new Color(1f, 0.55f, 0.18f);

        private readonly Dictionary<string, SyncedCapsule> peers =
            new Dictionary<string, SyncedCapsule>(StringComparer.Ordinal);
        private readonly HashSet<string> inactivePeers = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> timedOut = new List<string>();
        private readonly StringBuilder hud = new StringBuilder();

        private MistClient client;
        private SyncedCapsule local;
        private Transform root;
        private GUIStyle tagStyle;
        private float sendTimer;
        private long positionSequence;
        private bool roomJoined;
        private string status = "starting";

        /// <summary>
        /// The payload this sample broadcasts. It is the same shape the engine sends with
        /// <see cref="MistEventType.NodePositionUpdated"/>, so one parser covers both sources.
        /// </summary>
        [Serializable]
        private struct PositionPayload
        {
            public float x;
            public float y;
            public float z;
            public long sequence;
        }

        private void Start()
        {
            root = new GameObject("CapsuleSync Stage").transform;
            BuildStage();

            client = new MistClient();
            client.EventReceived += OnMistEvent;
            client.LogReceived += OnMistLog;

            try
            {
                client.Initialize(new MistClientOptions
                {
                    NodeId = nodeId,
                    ConfigJson = configJson,
                    InitialRoom = roomId
                });
            }
            catch (Exception exception)
            {
                // Update() and OnGUI() both handle an uninitialized client, so the component stays
                // enabled and keeps the reason on screen.
                status = "initialize failed: " + exception.Message;
                Debug.LogException(exception, this);
                return;
            }

            // Offset the start position so two processes on one machine do not overlap exactly.
            var start = new Vector3(UnityEngine.Random.Range(-3f, 3f), 1f, UnityEngine.Random.Range(-3f, 3f));
            local = SyncedCapsule.Create(client.NodeId, true, start, LocalColor, smoothing, root);
            status = "joining " + roomId;
            Debug.Log($"CapsuleSync node {client.NodeId} joining {roomId}.");
        }

        private void Update()
        {
            if (client == null || !client.IsInitialized)
            {
                return;
            }

            client.PumpCallbacks();
            MoveLocalCapsule();
            if (roomJoined)
            {
                PublishLocalPosition();
            }
            RemoveSilentPeers();
        }

        private void OnDestroy()
        {
            client?.Dispose();
            client = null;
            if (root != null)
            {
                Destroy(root.gameObject);
            }
        }

        private void MoveLocalCapsule()
        {
            var input = ReadMoveInput();
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }
            if (input == Vector3.zero)
            {
                return;
            }

            local.MoveTo(local.transform.position + input * (moveSpeed * Time.deltaTime));
        }

        private void PublishLocalPosition()
        {
            sendTimer -= Time.deltaTime;
            if (sendTimer > 0f)
            {
                return;
            }
            sendTimer = Mathf.Max(sendInterval, 0.02f);

            var position = local.transform.position;
            positionSequence++;

            // The engine's own copy of this node's position. It drives the area of interest, so
            // peers appear and disappear as the capsules move apart, and it reaches every peer as
            // a NodePositionUpdated event about once per second.
            client.UpdatePosition(position, roomId);

            // Once per second is too coarse to look like movement, so the sample also broadcasts
            // its position at sendInterval. This arrives as a Raw event.
            var payload = JsonUtility.ToJson(new PositionPayload
            {
                x = position.x,
                y = position.y,
                z = position.z,
                sequence = positionSequence
            });
            client.SendMessage(
                string.Empty,
                Encoding.UTF8.GetBytes(payload),
                MistDeliveryMethod.Unreliable,
                roomId);
        }

        private void OnMistEvent(MistEvent item)
        {
            if (!string.Equals(item.RoomId, roomId, StringComparison.Ordinal))
            {
                return;
            }

            // Room events have no sender; connection loss identifies this node instead of a peer.
            // Handle both before filtering peer events by FromNodeId.
            switch (item.Type)
            {
                case MistEventType.RoomJoined:
                    roomJoined = true;
                    status = "joined " + item.RoomId;
                    return;
                case MistEventType.RoomJoinFailed:
                    roomJoined = false;
                    status = "join failed: " + Encoding.UTF8.GetString(item.Data);
                    HidePeers();
                    Debug.LogError($"CapsuleSync {item.RoomId}: {status}");
                    return;
                case MistEventType.RoomLeft:
                    roomJoined = false;
                    status = "left " + item.RoomId;
                    HidePeers();
                    return;
                case MistEventType.AllConnectionsLost:
                    status = "all connections lost in " + item.RoomId;
                    HidePeers();
                    return;
            }

            if (!roomJoined)
            {
                return;
            }

            if (string.IsNullOrEmpty(item.FromNodeId) ||
                string.Equals(item.FromNodeId, client.NodeId, StringComparison.Ordinal))
            {
                return;
            }

            switch (item.Type)
            {
                case MistEventType.PeerConnected:
                case MistEventType.AreaOfInterestEntered:
                    status = "joined " + item.RoomId;
                    ActivatePeer(item.FromNodeId);
                    break;

                case MistEventType.PeerDisconnected:
                case MistEventType.AreaOfInterestLeft:
                    DeactivatePeer(item.FromNodeId);
                    break;

                // The engine republishes the position it holds for every node in the area of
                // interest. It may trail the broadcast stream, so never use it for visible motion.
                case MistEventType.NodePositionUpdated:
                    ObserveEnginePosition(item);
                    break;

                // This sample's own sequenced broadcast, which makes the motion smooth.
                case MistEventType.Raw:
                    ApplyBroadcastPosition(item);
                    break;
            }
        }

        private static void OnMistLog(MistLogEvent item)
        {
            if (item.Level >= MistLogLevel.Warning)
            {
                Debug.LogWarning($"MistLib [{item.Level}] {item.Message}");
            }
        }

        private void ObserveEnginePosition(MistEvent item)
        {
            if (inactivePeers.Contains(item.FromNodeId))
            {
                return;
            }

            GetOrCreatePeer(item.FromNodeId).MarkSeen();
        }

        private void ApplyBroadcastPosition(MistEvent item)
        {
            if (inactivePeers.Contains(item.FromNodeId) ||
                !TryParsePosition(item.Data, out var position, out var sequence))
            {
                return;
            }

            GetOrCreatePeer(item.FromNodeId).SetBroadcastTarget(position, sequence);
        }

        private void ActivatePeer(string peerId)
        {
            inactivePeers.Remove(peerId);
            GetOrCreatePeer(peerId).MarkSeen();
        }

        private void DeactivatePeer(string peerId)
        {
            inactivePeers.Add(peerId);
            if (!peers.TryGetValue(peerId, out var capsule))
            {
                return;
            }

            capsule.Hide();
            Debug.Log($"CapsuleSync peer hidden: {peerId}");
        }

        private void HidePeers()
        {
            foreach (var peerId in peers.Keys)
            {
                DeactivatePeer(peerId);
            }
        }

        private SyncedCapsule GetOrCreatePeer(string peerId)
        {
            if (peers.TryGetValue(peerId, out var existing))
            {
                return existing;
            }

            var capsule = SyncedCapsule.Create(
                peerId,
                false,
                local.transform.position,
                RemoteColor,
                smoothing,
                root);
            peers.Add(peerId, capsule);
            Debug.Log($"CapsuleSync peer allocated: {peerId}");
            return capsule;
        }

        private void RemovePeer(string peerId)
        {
            if (!peers.TryGetValue(peerId, out var capsule))
            {
                return;
            }

            peers.Remove(peerId);
            if (capsule != null)
            {
                Destroy(capsule.gameObject);
            }
            Debug.Log($"CapsuleSync peer expired: {peerId}");
        }

        /// <summary>
        /// Hides a peer that stopped reporting, then destroys its pooled object if it remains
        /// hidden for another timeout interval.
        /// </summary>
        private void RemoveSilentPeers()
        {
            timedOut.Clear();
            foreach (var pair in peers)
            {
                var capsule = pair.Value;
                if (capsule.IsVisible && Time.time - capsule.LastSeenTime > peerTimeoutSeconds)
                {
                    capsule.Hide();
                }
                else if (!capsule.IsVisible &&
                         Time.time - capsule.HiddenSinceTime > peerTimeoutSeconds)
                {
                    timedOut.Add(pair.Key);
                }
            }

            foreach (var peerId in timedOut)
            {
                RemovePeer(peerId);
            }
        }

        private static bool TryParsePosition(byte[] data, out Vector3 position, out long sequence)
        {
            position = Vector3.zero;
            sequence = 0;
            if (data == null || data.Length == 0 || data[0] != (byte)'{')
            {
                return false;
            }

            PositionPayload payload;
            try
            {
                payload = JsonUtility.FromJson<PositionPayload>(Encoding.UTF8.GetString(data));
            }
            catch (ArgumentException)
            {
                // Another component in the same room is free to broadcast anything it likes.
                return false;
            }

            position = new Vector3(payload.x, payload.y, payload.z);
            sequence = payload.sequence;
            return !float.IsNaN(position.sqrMagnitude) && !float.IsInfinity(position.sqrMagnitude);
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private static Vector3 ReadMoveInput()
        {
            return new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        }
#else
        // A project whose Active Input Handling is "Input System Package (New)" only makes the
        // legacy Input class throw. IMGUI key events arrive under either backend, so the sample
        // reads them instead of taking a dependency on the Input System package.
        private readonly HashSet<KeyCode> heldKeys = new HashSet<KeyCode>();

        private Vector3 ReadMoveInput()
        {
            var right = heldKeys.Contains(KeyCode.D) || heldKeys.Contains(KeyCode.RightArrow);
            var left = heldKeys.Contains(KeyCode.A) || heldKeys.Contains(KeyCode.LeftArrow);
            var forward = heldKeys.Contains(KeyCode.W) || heldKeys.Contains(KeyCode.UpArrow);
            var back = heldKeys.Contains(KeyCode.S) || heldKeys.Contains(KeyCode.DownArrow);
            return new Vector3(
                (right ? 1f : 0f) - (left ? 1f : 0f),
                0f,
                (forward ? 1f : 0f) - (back ? 1f : 0f));
        }

        private void TrackKeyEvents()
        {
            var current = Event.current;
            if (current == null || current.keyCode == KeyCode.None)
            {
                return;
            }

            if (current.type == EventType.KeyDown)
            {
                heldKeys.Add(current.keyCode);
            }
            else if (current.type == EventType.KeyUp)
            {
                heldKeys.Remove(current.keyCode);
            }
        }
#endif

        private void BuildStage()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root, false);
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            ground.GetComponent<Renderer>().material.color = new Color(0.17f, 0.18f, 0.22f);

            // A few fixed markers so a capsule's motion reads against something that does not move.
            for (var x = -10; x <= 10; x += 5)
            {
                for (var z = -10; z <= 10; z += 5)
                {
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    marker.name = $"Marker {x},{z}";
                    marker.transform.SetParent(root, false);
                    marker.transform.position = new Vector3(x, 0.02f, z);
                    marker.transform.localScale = new Vector3(0.35f, 0.04f, 0.35f);
                    Destroy(marker.GetComponent<Collider>());
                    marker.GetComponent<Renderer>().material.color = new Color(0.34f, 0.36f, 0.42f);
                }
            }

            if (Camera.main == null)
            {
                var camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.SetParent(root, false);
                camera.transform.SetPositionAndRotation(new Vector3(0f, 14f, -13f), Quaternion.Euler(46f, 0f, 0f));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
            }

            if (FindAnyObjectByType<Light>() == null)
            {
                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.type = LightType.Directional;
                light.intensity = 1.1f;
            }
        }

        private void OnGUI()
        {
#if !ENABLE_LEGACY_INPUT_MANAGER
            TrackKeyEvents();
#endif

            tagStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                richText = false
            };

            hud.Clear();
            hud.AppendLine("MistLib capsule sync - broadcast authoritative - " + status);
            hud.AppendLine("node: " + (client != null && client.IsInitialized ? client.NodeId : "-"));
            hud.AppendLine("room: " + roomId + "   peers: " + peers.Count);
            hud.AppendLine();
            hud.AppendLine("WASD or arrow keys move the blue capsule.");
            if (peers.Count == 0)
            {
                hud.AppendLine("No peer yet. Run a second copy of this sample in the same room.");
            }
            foreach (var pair in peers)
            {
                var streamStatus = !pair.Value.IsVisible
                    ? "  hidden"
                    : pair.Value.HasBroadcastTarget ? string.Empty : "  waiting for broadcast";
                hud.AppendLine(
                    $"{Shorten(pair.Key)}  {Format(pair.Value.TargetPosition)}  " +
                    $"via {pair.Value.LastSource}  {Time.time - pair.Value.LastUpdateTime:0.0}s ago" +
                    streamStatus);
            }

            GUI.Label(new Rect(12f, 12f, 640f, 320f), hud.ToString());

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            if (local != null)
            {
                DrawTag(camera, local, "YOU  " + Shorten(local.NodeId) + "\n" + Format(local.transform.position));
            }
            foreach (var pair in peers)
            {
                if (pair.Value.IsVisible)
                {
                    DrawTag(camera, pair.Value, Shorten(pair.Key) + "\n" + Format(pair.Value.TargetPosition));
                }
            }
        }

        private void DrawTag(Camera camera, SyncedCapsule capsule, string text)
        {
            var screen = camera.WorldToScreenPoint(capsule.transform.position + Vector3.up * 1.4f);
            if (screen.z <= 0f)
            {
                return;
            }

            tagStyle.normal.textColor = capsule.IsLocal ? LocalColor : RemoteColor;
            GUI.Label(new Rect(screen.x - 100f, Screen.height - screen.y - 34f, 200f, 34f), text, tagStyle);
        }

        private static string Format(Vector3 position)
        {
            return $"({position.x:0.0}, {position.y:0.0}, {position.z:0.0})";
        }

        private static string Shorten(string value)
        {
            return value.Length <= 12 ? value : value.Substring(0, 12);
        }
    }
}
