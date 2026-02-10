using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.ConnectionManagement.Utils
{
    /// <summary>
    /// Manages scene loading by wrapping around Unity's SceneManager and Netcode's NetworkSceneManager.
    /// This is a headless (no UI) implementation. If you need a loading screen, extend this class
    /// and override the virtual methods.
    /// </summary>
    public class SceneLoaderWrapper : NetworkBehaviour
    {
        bool IsNetworkSceneManagementEnabled => NetworkManager != null && NetworkManager.SceneManager != null && NetworkManager.NetworkConfig.EnableSceneManagement;

        bool m_IsInitialized;

        public static SceneLoaderWrapper Instance { get; protected set; }

        public virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this);
        }

        public virtual void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            NetworkManager.OnServerStarted += OnNetworkingSessionStarted;
            NetworkManager.OnClientStarted += OnNetworkingSessionStarted;
            NetworkManager.OnServerStopped += OnNetworkingSessionEnded;
            NetworkManager.OnClientStopped += OnNetworkingSessionEnded;
        }

        void OnNetworkingSessionStarted()
        {
            if (!m_IsInitialized)
            {
                if (IsNetworkSceneManagementEnabled)
                {
                    NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
                }
                m_IsInitialized = true;
            }
        }

        void OnNetworkingSessionEnded(bool unused)
        {
            if (m_IsInitialized)
            {
                if (IsNetworkSceneManagementEnabled)
                {
                    NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
                }
                m_IsInitialized = false;
            }
        }

        public override void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (NetworkManager != null)
            {
                NetworkManager.OnServerStarted -= OnNetworkingSessionStarted;
                NetworkManager.OnClientStarted -= OnNetworkingSessionStarted;
                NetworkManager.OnServerStopped -= OnNetworkingSessionEnded;
                NetworkManager.OnClientStopped -= OnNetworkingSessionEnded;
            }
            base.OnDestroy();
        }

        /// <summary>
        /// Loads a scene asynchronously using the specified loadSceneMode, with NetworkSceneManager if on a listening
        /// server with SceneManagement enabled, or SceneManager otherwise.
        /// </summary>
        /// <param name="sceneName">Name or path of the Scene to load.</param>
        /// <param name="useNetworkSceneManager">If true, uses NetworkSceneManager, else uses SceneManager</param>
        /// <param name="loadSceneMode">If LoadSceneMode.Single then all current Scenes will be unloaded before loading.</param>
        public virtual void LoadScene(string sceneName, bool useNetworkSceneManager, LoadSceneMode loadSceneMode = LoadSceneMode.Single)
        {
            if (useNetworkSceneManager)
            {
                if (IsSpawned && IsNetworkSceneManagementEnabled && !NetworkManager.ShutdownInProgress)
                {
                    if (NetworkManager.IsServer)
                    {
                        NetworkManager.SceneManager.LoadScene(sceneName, loadSceneMode);
                    }
                }
            }
            else
            {
                SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
            }
        }

        /// <summary>
        /// Called when a scene finishes loading. Override to add custom behavior (e.g., loading screen).
        /// </summary>
        protected virtual void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            // No-op in headless version. Override to add loading screen behavior.
        }

        /// <summary>
        /// Called when a network scene event occurs. Override to add custom behavior (e.g., loading screen).
        /// </summary>
        protected virtual void OnSceneEvent(SceneEvent sceneEvent)
        {
            // No-op in headless version. Override to add loading screen / synchronization behavior.
        }
    }
}
