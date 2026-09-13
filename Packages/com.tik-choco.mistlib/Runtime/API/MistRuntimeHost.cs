using System;
using UnityEngine;

namespace TikChoco.MistLib
{
    [DisallowMultipleComponent]
    public sealed class MistRuntimeHost : MonoBehaviour
    {
        [SerializeField] private bool initializeOnAwake;
        [Tooltip("Leave empty for a generated unique ID.")]
        [SerializeField] private string nodeId;
        [Tooltip("Legacy WebSocket signaling. Leave empty unless you need it instead of the config JSON.")]
        [SerializeField] private string signalingUrl;
        [Tooltip("Leave empty to run on the engine defaults. Only the keys you set are overridden.")]
        [SerializeField, TextArea(4, 16)] private string configJson;
        [SerializeField] private string initialRoom;

        public MistClient Client { get; private set; }

        private void Awake()
        {
            if (!initializeOnAwake)
            {
                return;
            }

            Initialize(new MistClientOptions
            {
                NodeId = nodeId,
                SignalingUrl = signalingUrl,
                ConfigJson = configJson,
                InitialRoom = initialRoom
            });
        }

        /// <summary>
        /// Copies the configuration the engine resolved into the Inspector field, so a deployment can
        /// start from the real defaults instead of a copy of them maintained here.
        /// </summary>
        [ContextMenu("Copy Effective Config JSON")]
        private void CopyEffectiveConfigJson()
        {
            if (Client == null || !Client.IsInitialized)
            {
                Debug.LogWarning("MistRuntimeHost is not initialized; enter Play Mode first.");
                return;
            }

            configJson = Client.GetConfigJson();
        }

        private void Update()
        {
            if (Client != null && Client.IsInitialized)
            {
                Client.PumpCallbacks();
            }
        }

        private void OnDestroy()
        {
            Client?.Dispose();
            Client = null;
        }

        public void Initialize(MistClientOptions options)
        {
            if (Client != null)
            {
                throw new InvalidOperationException("MistRuntimeHost is already initialized.");
            }

            var client = new MistClient();
            try
            {
                client.Initialize(options);
                Client = client;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }
    }
}
