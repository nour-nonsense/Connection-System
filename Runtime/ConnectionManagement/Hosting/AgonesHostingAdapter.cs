using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// NOTE: Requires the Agones Unity SDK package to be installed.
// Install via UPM: https://github.com/googleforgames/agones/tree/main/sdks/unity
// If the SDK is not installed, this file will not compile.
// The #if AGONES_SDK directive can be used to conditionally compile.

namespace Unity.ConnectionManagement.Hosting
{
    /// <summary>
    /// Bridges the Official Agones Unity SDK to <see cref="IHostingAdapter"/>.
    /// Handles sidecar connection, health pings, allocation watching, and graceful shutdown.
    /// </summary>
    public class AgonesHostingAdapter : IHostingAdapter
    {
#if AGONES_SDK
        Agones.AgonesSdk m_Agones;
#endif

        bool m_Disposed;

        public float HealthCheckIntervalSeconds => 5f;

        public event Action OnShutdownRequested;
        public event Action<string> OnAllocated;

        // ── Lifecycle ──────────────────────────────────────────────

        public async Task<bool> InitializeAsync()
        {
#if AGONES_SDK
            try
            {
                // The Agones SDK uses a GameObject — create it if needed
                var go = new GameObject("[AgonesSDK]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                m_Agones = go.AddComponent<Agones.AgonesSdk>();

                bool connected = await m_Agones.Connect();
                if (!connected)
                {
                    Debug.LogWarning("[AgonesAdapter] Failed to connect to Agones sidecar.");
                    UnityEngine.Object.Destroy(go);
                    m_Agones = null;
                    return false;
                }

                // Watch for state changes (allocation, shutdown)
                m_Agones.WatchGameServer(OnGameServerUpdated);

                Debug.Log("[AgonesAdapter] Connected to Agones sidecar.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AgonesAdapter] Agones initialization failed: {e.Message}");
                return false;
            }
#else
            Debug.LogWarning("[AgonesAdapter] AGONES_SDK is not defined. Agones SDK not installed.");
            await Task.CompletedTask;
            return false;
#endif
        }

        public async Task ReadyAsync()
        {
#if AGONES_SDK
            if (m_Agones != null)
            {
                bool ok = await m_Agones.Ready();
                Debug.Log(ok
                    ? "[AgonesAdapter] Marked as Ready."
                    : "[AgonesAdapter] Failed to mark Ready.");
            }
#else
            await Task.CompletedTask;
#endif
        }

        public async Task ShutdownAsync()
        {
#if AGONES_SDK
            if (m_Agones != null)
            {
                bool ok = await m_Agones.Shutdown();
                Debug.Log(ok
                    ? "[AgonesAdapter] Shutdown acknowledged."
                    : "[AgonesAdapter] Shutdown call failed.");
            }
#else
            await Task.CompletedTask;
#endif
        }

        // ── Health ─────────────────────────────────────────────────

        public async Task HealthCheckAsync()
        {
#if AGONES_SDK
            if (m_Agones != null)
            {
                await m_Agones.Health();
            }
#else
            await Task.CompletedTask;
#endif
        }

        // ── Metadata ───────────────────────────────────────────────

        public async Task<ServerAllocationData> GetAllocationDataAsync()
        {
#if AGONES_SDK
            if (m_Agones != null)
            {
                var gs = await m_Agones.GetGameServer();
                var labels = gs?.ObjectMeta?.LabelsMap ?? new Dictionary<string, string>();
                var annotations = gs?.ObjectMeta?.AnnotationsMap ?? new Dictionary<string, string>();

                return new ServerAllocationData
                {
                    GameSessionId = labels.GetValueOrDefault("agones.dev/session-id", ""),
                    MapName = labels.GetValueOrDefault("agones.dev/map", "CharSelect"),
                    MaxPlayers = int.TryParse(labels.GetValueOrDefault("agones.dev/max-players", "8"), out int mp) ? mp : 8,
                    Labels = labels
                };
            }
#endif
            await Task.CompletedTask;
            return new ServerAllocationData
            {
                GameSessionId = "",
                MapName = "CharSelect",
                MaxPlayers = 8,
                Labels = new Dictionary<string, string>()
            };
        }

        // ── Internal ───────────────────────────────────────────────

#if AGONES_SDK
        void OnGameServerUpdated(Agones.Model.GameServer gs)
        {
            if (gs == null) return;

            var state = gs.Status?.State;
            switch (state)
            {
                case "Allocated":
                    var sessionId = gs.ObjectMeta?.LabelsMap?.GetValueOrDefault("agones.dev/session-id", "");
                    Debug.Log($"[AgonesAdapter] Allocated! Session: {sessionId}");
                    OnAllocated?.Invoke(sessionId);
                    break;

                case "Shutdown":
                    Debug.Log("[AgonesAdapter] Shutdown requested by fleet.");
                    OnShutdownRequested?.Invoke();
                    break;
            }
        }
#endif

        // ── IDisposable ────────────────────────────────────────────

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;

#if AGONES_SDK
            if (m_Agones != null)
            {
                UnityEngine.Object.Destroy(m_Agones.gameObject);
                m_Agones = null;
            }
#endif
        }
    }
}
