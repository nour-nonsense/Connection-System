using System;
using Unity.ConnectionManagement.Hosting;
using Unity.ConnectionManagement.Infrastructure;
using Unity.ConnectionManagement.Sessions;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using VContainer;

namespace Unity.ConnectionManagement
{
    /// <summary>
    /// Connection state corresponding to a dedicated server starting up. Starts the server (no local client)
    /// when entering the state. If successful, transitions to the Hosting state, if not, transitions back to Offline.
    /// </summary>
    class StartingServerState : OnlineState
    {
        [Inject(Optional = true)]
        IHostingAdapter m_HostingAdapter;

        ConnectionMethodBase m_ConnectionMethod;

        public StartingServerState Configure(ConnectionMethodBase baseConnectionMethod)
        {
            m_ConnectionMethod = baseConnectionMethod;
            return this;
        }

        public override void Enter()
        {
            StartServer();
        }

        public override void Exit() { }

        /// <summary>
        /// Agones Priority / Local Fallback:
        /// 1. If IHostingAdapter is available and connects → DedicatedServerHostingState
        /// 2. Otherwise → standard HostingState
        /// </summary>
        public override async void OnServerStarted()
        {
            // 1. Try Agones (or any hosting adapter)
            if (m_HostingAdapter != null)
            {
                try
                {
                    bool connected = await m_HostingAdapter.InitializeAsync();
                    if (connected)
                    {
                        Debug.Log("[Server] Hosting platform connected. Entering DedicatedServerHostingState.");
                        m_ConnectStatusPublisher.Publish(ConnectStatus.Success);
                        m_ConnectionManager.ChangeState(m_ConnectionManager.m_DedicatedServerHosting);
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Server] Hosting adapter initialization failed: {e.Message}. Falling back to local.");
                }
            }

            // 2. Fallback to standard local hosting
            Debug.Log("[Server] No hosting platform detected. Using standard HostingState.");
            m_ConnectStatusPublisher.Publish(ConnectStatus.Success);
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Hosting);
        }

        public override void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var connectionData = request.Payload;
            var clientId = request.ClientNetworkId;

            var payload = System.Text.Encoding.UTF8.GetString(connectionData);
            var connectionPayload = JsonUtility.FromJson<ConnectionPayload>(payload);

            SessionManager<SessionPlayerData>.Instance.SetupConnectingPlayerSessionData(clientId, connectionPayload.playerId,
                new SessionPlayerData(clientId, connectionPayload.playerName, new NetworkGuid(), 0, true));

            response.Approved = true;
            response.CreatePlayerObject = true;
        }

        public override void OnServerStopped()
        {
            StartServerFailed();
        }

        void StartServer()
        {
            try
            {
                m_ConnectionMethod?.SetupHostConnection();

                if (!m_ConnectionManager.NetworkManager.StartServer())
                {
                    StartServerFailed();
                }
            }
            catch (Exception)
            {
                StartServerFailed();
                throw;
            }
        }

        void StartServerFailed()
        {
            m_ConnectStatusPublisher.Publish(ConnectStatus.StartHostFailed);
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }
    }
}
