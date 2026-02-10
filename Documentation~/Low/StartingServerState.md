# `StartingServerState.cs` — Technical Reference

[← Back to Low-Level Index](index.md) · [OnlineState](OnlineState.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/ConnectionManagement/ConnectionState/StartingServerState.cs` |
| **Namespace** | `Unity.ConnectionManagement` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Base class** | `OnlineState` |
| **Access** | `internal class` |

---

## Purpose

Handles the startup process for a **Dedicated Server**.
Unlike `StartingHostState`, this state:
1.  Calls `NetworkManager.StartServer()` (not StartHost).
2.  Does **not** start a local client.
3.  Does **not** need self-approval logic (since there is no local player).

---

## Method Internals

### `Enter()`

Simply calls `StartServer()`.

### `StartServer()`

1.  Calls `m_ConnectionMethod.SetupHostConnection()` to configure transport (port/IP).
2.  Calls `NetworkManager.StartServer()`.
3.  Catches exceptions and calls `StartServerFailed()` if anything explodes.

### `OnServerStarted()`

Triggered by `NetworkManager` callback when the server successfully binds to the port.
- Publishes `ConnectStatus.Success`.
- Transitions to `HostingState`.

### `ApprovalCheck()`

Called when **Clients** try to connect to this server.
- Decodes the JSON payload from the client.
- Sets up `SessionPlayerData` in `SessionManager`.
- Returns `response.Approved = true`.
- returns `response.CreatePlayerObject = true`.

**Difference from StartingHostState:**
`StartingHostState` has special logic to approve *itself* (Host-Client). `StartingServerState` only handles *remote* clients.

### `OnServerStopped()`

If the server stops during startup (binding error, etc.), calls `StartServerFailed()`.

---

## State Transitions

| To State | Condition |
| :--- | :--- |
| `HostingState` | Server started successfully (`OnServerStarted` callback). |
| `OfflineState` | Server failed to start (Exception or `StartServer` returns false). |
