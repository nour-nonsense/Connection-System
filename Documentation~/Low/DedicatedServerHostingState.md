# DedicatedServerHostingState

`DedicatedServerHostingState` is a specialized state for dedicated servers running in a managed environment (e.g., Kubernetes via Agones, or Edgegap). It replaces the standard `HostingState` for dedicated server builds when a hosting adapter is detected.

## Purpose

This state bridges the gap between the game logic (`ConnectionManager`) and the infrastructure platform. It handles:
- **Lifecycle Management**: Signaling `Ready` to the platform, handling `Allocated` events, and processing `Shutdown` requests.
- **Health Checks**: Periodically pinging the sidecar or agent to report server health.
- **Allocation Data**: Reading metadata (e.g., map name, max players) from the allocation payload to configure the session.
- **Graceful Shutdown**: Ensuring clients are disconnected cleanly before the process terminates.

## Workflow

1.  **Enter**: 
    - Subscribes to `OnAllocated` and `OnShutdownRequested` events from the `IHostingAdapter`.
    - Calls `ReadyAsync()` on the adapter to signal availability.
    - Starts a coroutine to call `HealthCheckAsync()` periodically (default: every 5 seconds).

2.  **Allocation**:
    - When `OnAllocated` fires, the state fetches allocation data via `GetAllocationDataAsync()`.
    - It updates `MaxConnectedPlayers` based on the allocation configuration.
    - It loads the scene specified by `MapName` (defaults to "CharSelect" if unspecified).

3.  **Shutdown**:
    - If the platform requests shutdown (e.g., scale-down event), the state disconnects all clients with a `HostEndedSession` reason.
    - It invokes `ShutdownAsync()` on the adapter.
    - Transitions to `OfflineState` and forces `Application.Quit()`.

## Dependencies

- **IHostingAdapter**: The interface used to communicate with the hosting platform. If no adapter is present, this state is never entered.
- **SessionManager**: Used for player data tracking (same as `HostingState`).

## Comparison with HostingState

| Feature | HostingState (Client-Host) | DedicatedServerHostingState |
| :--- | :--- | :--- |
| **Owner** | Player (Host) | Cloud / Kubernetes |
| **Lifecycle** | User-controlled | Orchestrator-controlled |
| **Scene Loading** | Hardcoded / UI-driven | Driven by Allocation Metadata |
| **Health Checks** | None | active ping loop |
| **Approval** | Logic + UGS Session | Logic only (no UGS Session) |
