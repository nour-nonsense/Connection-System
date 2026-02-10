using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Unity.ConnectionManagement.Hosting
{
    /// <summary>
    /// Platform-agnostic interface for dedicated server hosting platforms (Agones, Edgegap, Multiplay, etc.).
    /// Implementations bridge the hosting SDK to the ConnectionManager state machine.
    /// </summary>
    public interface IHostingAdapter : IDisposable
    {
        // ── Lifecycle ──────────────────────────────────────────────

        /// <summary>
        /// Attempt to connect to the hosting platform (e.g. Agones sidecar).
        /// Returns true if the platform is available, false to fall back to local hosting.
        /// </summary>
        Task<bool> InitializeAsync();

        /// <summary>
        /// Signal "ready for allocation" to the hosting platform.
        /// </summary>
        Task ReadyAsync();

        /// <summary>
        /// Gracefully shut down and notify the hosting platform.
        /// </summary>
        Task ShutdownAsync();

        // ── Health ─────────────────────────────────────────────────

        /// <summary>
        /// Send a health ping to the hosting platform.
        /// </summary>
        Task HealthCheckAsync();

        /// <summary>
        /// How often (in seconds) to call HealthCheckAsync.
        /// </summary>
        float HealthCheckIntervalSeconds { get; }

        // ── Metadata ───────────────────────────────────────────────

        /// <summary>
        /// Retrieve allocation metadata (map, max players, labels, etc.).
        /// </summary>
        Task<ServerAllocationData> GetAllocationDataAsync();

        // ── Events ─────────────────────────────────────────────────

        /// <summary>
        /// Raised when the hosting platform requests a graceful shutdown.
        /// </summary>
        event Action OnShutdownRequested;

        /// <summary>
        /// Raised when the server is allocated to a game session.
        /// </summary>
        event Action<string> OnAllocated;
    }

    /// <summary>
    /// Metadata received from the hosting platform upon allocation.
    /// </summary>
    public struct ServerAllocationData
    {
        public string GameSessionId;
        public string MapName;
        public int MaxPlayers;
        public Dictionary<string, string> Labels;
    }
}
