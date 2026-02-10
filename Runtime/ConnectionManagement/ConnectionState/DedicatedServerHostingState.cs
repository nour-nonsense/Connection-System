using System;
using System.Collections;
using Unity.ConnectionManagement.Hosting;
using Unity.ConnectionManagement.Infrastructure;
using Unity.ConnectionManagement.Sessions;
using Unity.ConnectionManagement.Utils;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace Unity.ConnectionManagement
{
    /// <summary>
    /// Connection state for a dedicated server managed by a hosting platform (Agones, Edgegap, etc.).
    /// Extends the standard hosting behavior with health checks, allocation handling, and graceful shutdown.
    /// Falls back to <see cref="HostingState"/> behavior for approval checks and player management.
    /// </summary>
    class DedicatedServerHostingState : OnlineState
    {
        [Inject]
        IPublisher<ConnectionEventMessage> m_ConnectionEventPublisher;

        IHostingAdapter m_HostingAdapter;

        /// <summary>
        /// Set by ConnectionManager after resolving the optional hosting adapter.
        /// Guaranteed non-null when this state is entered (priority check passed).
        /// </summary>
        internal void SetHostingAdapter(IHostingAdapter adapter) => m_HostingAdapter = adapter;

        const int k_MaxConnectPayload = 1024;

        Coroutine m_HealthCheckCoroutine;

        public override void Enter()
        {
            Debug.Log("[DedicatedServer] Entering DedicatedServerHostingState.");

            // Subscribe to hosting platform events
            m_HostingAdapter.OnShutdownRequested += HandleShutdownRequested;
            m_HostingAdapter.OnAllocated += HandleAllocated;

            // Signal ready and start health checks
            SignalReadyAndStartHealth();
        }

        public override void Exit()
        {
            if (m_HostingAdapter != null)
            {
                m_HostingAdapter.OnShutdownRequested -= HandleShutdownRequested;
                m_HostingAdapter.OnAllocated -= HandleAllocated;
            }

            if (m_HealthCheckCoroutine != null)
            {
                m_ConnectionManager.StopCoroutine(m_HealthCheckCoroutine);
                m_HealthCheckCoroutine = null;
            }

            SessionManager<SessionPlayerData>.Instance.OnServerEnded();
        }

        // ── Lifecycle ──────────────────────────────────────────────

        async void SignalReadyAndStartHealth()
        {
            try
            {
                await m_HostingAdapter.ReadyAsync();
                Debug.Log("[DedicatedServer] Marked Ready on hosting platform.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DedicatedServer] Failed to signal Ready: {e.Message}");
            }

            m_HealthCheckCoroutine = m_ConnectionManager.StartCoroutine(HealthCheckLoop());
        }

        IEnumerator HealthCheckLoop()
        {
            var interval = m_HostingAdapter.HealthCheckIntervalSeconds;
            while (true)
            {
                yield return new WaitForSecondsRealtime(interval);
                try
                {
                    m_HostingAdapter.HealthCheckAsync(); // fire-and-forget
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DedicatedServer] Health check failed: {e.Message}");
                }
            }
        }

        // ── Event Handlers ─────────────────────────────────────────

        async void HandleAllocated(string gameSessionId)
        {
            Debug.Log($"[DedicatedServer] Allocated to session: {gameSessionId}");

            try
            {
                var allocationData = await m_HostingAdapter.GetAllocationDataAsync();

                if (allocationData.MaxPlayers > 0)
                {
                    m_ConnectionManager.MaxConnectedPlayers = allocationData.MaxPlayers;
                }

                var sceneName = string.IsNullOrEmpty(allocationData.MapName) ? "CharSelect" : allocationData.MapName;
                SceneLoaderWrapper.Instance.LoadScene(sceneName, useNetworkSceneManager: true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DedicatedServer] Failed to process allocation: {e.Message}");
                // Fallback to default scene
                SceneLoaderWrapper.Instance.LoadScene("CharSelect", useNetworkSceneManager: true);
            }
        }

        void HandleShutdownRequested()
        {
            Debug.Log("[DedicatedServer] Shutdown requested by hosting platform.");
            GracefulShutdown();
        }

        async void GracefulShutdown()
        {
            // Disconnect all clients with a reason
            var reason = UnityEngine.JsonUtility.ToJson(ConnectStatus.HostEndedSession);
            for (var i = m_ConnectionManager.NetworkManager.ConnectedClientsIds.Count - 1; i >= 0; i--)
            {
                var id = m_ConnectionManager.NetworkManager.ConnectedClientsIds[i];
                m_ConnectionManager.NetworkManager.DisconnectClient(id, reason);
            }

            try
            {
                await m_HostingAdapter.ShutdownAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DedicatedServer] Shutdown signal failed: {e.Message}");
            }

            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);

            // Quit the application after transitioning to Offline
            Debug.Log("[DedicatedServer] Shutting down application.");
            Application.Quit();
        }

        // ── Player Management (mirrors HostingState) ───────────────

        public override void OnClientConnected(ulong clientId)
        {
            var playerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(clientId);
            if (playerData != null)
            {
                m_ConnectionEventPublisher.Publish(new ConnectionEventMessage()
                {
                    ConnectStatus = ConnectStatus.Success,
                    PlayerName = playerData.Value.PlayerName
                });
            }
            else
            {
                Debug.LogError($"No player data associated with client {clientId}");
                var reason = UnityEngine.JsonUtility.ToJson(ConnectStatus.GenericDisconnect);
                m_ConnectionManager.NetworkManager.DisconnectClient(clientId, reason);
            }
        }

        public override void OnClientDisconnect(ulong clientId)
        {
            var playerId = SessionManager<SessionPlayerData>.Instance.GetPlayerId(clientId);
            if (playerId != null)
            {
                var sessionData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(playerId);
                if (sessionData.HasValue)
                {
                    m_ConnectionEventPublisher.Publish(new ConnectionEventMessage()
                    {
                        ConnectStatus = ConnectStatus.GenericDisconnect,
                        PlayerName = sessionData.Value.PlayerName
                    });
                }
                SessionManager<SessionPlayerData>.Instance.DisconnectClient(clientId);
            }
        }

        public override void OnUserRequestedShutdown()
        {
            GracefulShutdown();
        }

        public override void OnServerStopped()
        {
            m_ConnectStatusPublisher.Publish(ConnectStatus.GenericDisconnect);
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }

        /// <summary>
        /// Approval check — same logic as HostingState but without MultiplayerServicesFacade
        /// (dedicated servers don't use UGS sessions for player management).
        /// </summary>
        public override void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var connectionData = request.Payload;
            var clientId = request.ClientNetworkId;

            if (connectionData.Length > k_MaxConnectPayload)
            {
                response.Approved = false;
                return;
            }

            var payload = System.Text.Encoding.UTF8.GetString(connectionData);
            var connectionPayload = UnityEngine.JsonUtility.FromJson<ConnectionPayload>(payload);
            var gameReturnStatus = GetConnectStatus(connectionPayload);

            if (gameReturnStatus == ConnectStatus.Success)
            {
                SessionManager<SessionPlayerData>.Instance.SetupConnectingPlayerSessionData(clientId, connectionPayload.playerId,
                    new SessionPlayerData(clientId, connectionPayload.playerName, new NetworkGuid(), 0, true));

                response.Approved = true;
                response.CreatePlayerObject = true;
                response.Position = Vector3.zero;
                response.Rotation = Quaternion.identity;
                return;
            }

            response.Approved = false;
            response.Reason = UnityEngine.JsonUtility.ToJson(gameReturnStatus);
        }

        ConnectStatus GetConnectStatus(ConnectionPayload connectionPayload)
        {
            if (m_ConnectionManager.NetworkManager.ConnectedClientsIds.Count >= m_ConnectionManager.MaxConnectedPlayers)
            {
                return ConnectStatus.ServerFull;
            }

            if (connectionPayload.isDebug != Debug.isDebugBuild)
            {
                return ConnectStatus.IncompatibleBuildType;
            }

            return SessionManager<SessionPlayerData>.Instance.IsDuplicateConnection(connectionPayload.playerId)
                ? ConnectStatus.LoggedInAgain
                : ConnectStatus.Success;
        }
    }
}
