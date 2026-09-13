using System;

namespace TikChoco.MistLib
{
    [Serializable]
    public sealed class MistClientOptions
    {
        /// <summary>
        /// Configuration that keeps every engine default: Nostr signaling, the engine's relay list,
        /// discovery kinds, timings, and STUN servers. This is what an empty
        /// <see cref="ConfigJson"/> sends, so the package never restates those values itself.
        /// </summary>
        public const string EngineDefaultsJson = "{}";

        /// <summary>Leave empty for a generated unique ID. The engine has no default for this one.</summary>
        public string NodeId;

        /// <summary>Legacy WebSocket signaling endpoint. Mutually exclusive with <see cref="ConfigJson"/>.</summary>
        public string SignalingUrl;

        /// <summary>
        /// MistLib configuration JSON. Every key is optional, and an omitted key keeps the engine
        /// default, so an empty value here is valid and sends <see cref="EngineDefaultsJson"/>.
        /// Call <see cref="MistClient.GetConfigJson"/> after initialization to read back what the
        /// engine actually resolved.
        /// </summary>
        public string ConfigJson;

        public string InitialRoom;

        internal void Validate()
        {
            if (!string.IsNullOrWhiteSpace(SignalingUrl) && !string.IsNullOrWhiteSpace(ConfigJson))
            {
                throw new ArgumentException(
                    "Set at most one of SignalingUrl or ConfigJson. Leave both empty to run on the " +
                    "engine defaults.");
            }
        }

        internal bool UsesSignalingUrl => !string.IsNullOrWhiteSpace(SignalingUrl);

        internal string ResolveNodeId()
        {
            return string.IsNullOrWhiteSpace(NodeId) ? "unity-" + Guid.NewGuid().ToString("N") : NodeId.Trim();
        }

        internal string ResolveConfigJson()
        {
            return string.IsNullOrWhiteSpace(ConfigJson) ? EngineDefaultsJson : ConfigJson;
        }
    }
}
