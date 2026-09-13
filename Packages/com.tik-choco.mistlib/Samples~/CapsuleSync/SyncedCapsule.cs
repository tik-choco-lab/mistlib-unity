using UnityEngine;

namespace TikChoco.MistLib.Samples
{
    /// <summary>
    /// One capsule in the room. The local capsule follows input directly; a remote capsule
    /// interpolates toward the last position MistLib reported for its node, so the gap between
    /// two sparse updates is visible as movement instead of a jump.
    /// </summary>
    public sealed class SyncedCapsule : MonoBehaviour
    {
        private float smoothing = 10f;
        private bool hasBroadcastTarget;
        private bool hasBroadcastSequence;
        private long lastBroadcastSequence;
        private Renderer capsuleRenderer;

        public string NodeId { get; private set; }
        public bool IsLocal { get; private set; }
        public bool HasBroadcastTarget => hasBroadcastTarget;
        public bool IsVisible => IsLocal || (capsuleRenderer != null && capsuleRenderer.enabled);

        /// <summary>The last position received for this node. The local capsule is always at it.</summary>
        public Vector3 TargetPosition { get; private set; }

        /// <summary>Which MistLib path delivered <see cref="TargetPosition"/>, for the on-screen HUD.</summary>
        public string LastSource { get; private set; } = "-";

        public float LastUpdateTime { get; private set; }
        public float LastSeenTime { get; private set; }
        public float HiddenSinceTime { get; private set; }

        public static SyncedCapsule Create(
            string nodeId,
            bool isLocal,
            Vector3 position,
            Color color,
            float smoothing,
            Transform parent)
        {
            var instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            instance.name = (isLocal ? "Local " : "Remote ") + nodeId;
            instance.transform.SetParent(parent, false);
            instance.transform.position = position;

            // The sample moves capsules by assignment, so nothing needs a collider.
            Destroy(instance.GetComponent<Collider>());
            var capsuleRenderer = instance.GetComponent<Renderer>();
            capsuleRenderer.material.color = color;

            var capsule = instance.AddComponent<SyncedCapsule>();
            capsule.NodeId = nodeId;
            capsule.IsLocal = isLocal;
            capsule.TargetPosition = position;
            capsule.LastSource = isLocal ? "input" : "-";
            capsule.LastUpdateTime = Time.time;
            capsule.LastSeenTime = capsule.LastUpdateTime;
            capsule.HiddenSinceTime = isLocal ? 0f : Time.time;
            capsule.smoothing = smoothing;
            capsule.capsuleRenderer = capsuleRenderer;
            capsuleRenderer.enabled = isLocal;
            return capsule;
        }

        /// <summary>Records a lower-rate engine update as peer activity without moving it.</summary>
        public void MarkSeen()
        {
            LastSeenTime = Time.time;
        }

        /// <summary>Applies a newer position from this sample's high-rate broadcast stream.</summary>
        public void SetBroadcastTarget(Vector3 position, long sequence)
        {
            // Sequence zero is accepted for compatibility with an older copy of the sample. Once
            // a sequenced update arrives, unsequenced and reordered older updates are discarded.
            if (sequence > 0)
            {
                if (hasBroadcastSequence && sequence <= lastBroadcastSequence)
                {
                    return;
                }

                hasBroadcastSequence = true;
                lastBroadcastSequence = sequence;
            }
            else if (hasBroadcastSequence)
            {
                return;
            }

            var wasVisible = IsVisible;
            hasBroadcastTarget = true;
            SetTarget(position, "broadcast");
            if (!wasVisible)
            {
                // A pooled peer may still be sitting at the position it had before leaving the
                // AOI. Place it at the first fresh target before making it visible again.
                transform.position = position;
                capsuleRenderer.enabled = true;
                HiddenSinceTime = 0f;
            }
        }

        /// <summary>Hides this peer while retaining its transform and sequence state for reuse.</summary>
        public void Hide()
        {
            if (IsLocal || !IsVisible)
            {
                return;
            }

            capsuleRenderer.enabled = false;
            HiddenSinceTime = Time.time;
        }

        private void SetTarget(Vector3 position, string source)
        {
            TargetPosition = position;
            LastSource = source;
            LastUpdateTime = Time.time;
            LastSeenTime = LastUpdateTime;
        }

        /// <summary>Moves the local capsule, which is the position this process publishes.</summary>
        public void MoveTo(Vector3 position)
        {
            TargetPosition = position;
            transform.position = position;
            LastUpdateTime = Time.time;
            LastSeenTime = LastUpdateTime;
        }

        private void Update()
        {
            if (IsLocal || !IsVisible)
            {
                return;
            }

            var step = 1f - Mathf.Exp(-Mathf.Max(smoothing, 0.01f) * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, TargetPosition, step);
        }
    }
}
