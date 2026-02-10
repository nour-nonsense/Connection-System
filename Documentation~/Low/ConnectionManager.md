# `ConnectionManager.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/ConnectionManagement/ConnectionManager.cs` |
| **Namespace** | `Unity.ConnectionManagement` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 183 |
| **Base class** | `MonoBehaviour` |
| **Access** | `public` |

---

## Purpose

Central orchestrator for the connection lifecycle. Owns all state instances, wires up `NetworkManager` callbacks, and delegates every event to the current `ConnectionState`.

---

## Field Inventory

### Serialized Fields (Inspector-visible)

| Field | Type | Default | Line | Description |
|-------|------|---------|------|-------------|
| `m_NetworkManager` | `NetworkManager` | — | ~18 | Direct reference to the scene's NetworkManager. Assigned via Inspector. |
| `m_MaxConnectedPlayers` | `int` | `8` | ~21 | Maximum concurrent connected clients. Used in `HostingState.GetConnectStatus()`. |
| `m_NbReconnectAttempts` | `int` | `2` | ~24 | Number of automatic reconnection attempts before giving up. Read by `ClientReconnectingState`. |

### Public Properties

| Property | Type | Accessor | Line | Description |
|----------|------|----------|------|-------------|
| `NetworkManager` | `NetworkManager` | `get` | ~26 | Exposes `m_NetworkManager` read-only. |
| `MaxConnectedPlayers` | `int` | `get` | ~27 | Exposes `m_MaxConnectedPlayers`. |
| `NbReconnectAttempts` | `int` | `get` | ~28 | Exposes `m_NbReconnectAttempts`. |

### Internal State Instances

All state objects are allocated in the field initializers (not in `Awake`/`Start`). They are `internal` so states can reference siblings for transitions.

| Field | Type | Line | Description |
|-------|------|------|-------------|
| `m_Offline` | `OfflineState` | ~30 | `new OfflineState()` |
| `m_ClientConnecting` | `ClientConnectingState` | ~31 | `new ClientConnectingState()` |
| `m_ClientConnected` | `ClientConnectedState` | ~32 | `new ClientConnectedState()` |
| `m_ClientReconnecting` | `ClientReconnectingState` | ~33 | `new ClientReconnectingState()` |
| `m_StartingHost` | `StartingHostState` | ~34 | `new StartingHostState()` |
| `m_Hosting` | `HostingState` | ~35 | `new HostingState()` |

### Private Fields

| Field | Type | Line | Description |
|-------|------|------|-------------|
| `m_CurrentState` | `ConnectionState` | ~37 | Pointer to the currently active state. Initially `null`, set to `m_Offline` in `Start()`. |

### Injected Fields

| Field | Type | Attribute | Line | Description |
|-------|------|-----------|------|-------------|
| `m_Resolver` | `IObjectResolver` | `[Inject]` | ~40 | VContainer resolver. Used to inject dependencies into state objects. |

---

## Lifecycle Methods

### `Start()` — Line ~43

**Execution order:** Called by Unity on the first frame after the MonoBehaviour is enabled.

**Call chain:**
```
Start()
  ├── m_Resolver.Inject(m_Offline)
  ├── m_Resolver.Inject(m_ClientConnecting)
  ├── m_Resolver.Inject(m_ClientConnected)
  ├── m_Resolver.Inject(m_ClientReconnecting)
  ├── m_Resolver.Inject(m_StartingHost)
  ├── m_Resolver.Inject(m_Hosting)
  ├── m_CurrentState = m_Offline
  └── RegisterNetworkCallbacks()
```

**Critical detail:** All 6 state objects are injected here. If `m_Resolver` is null (VContainer not set up), injection fails with `NullReferenceException`. The states' `[Inject]` fields will remain null, causing runtime crashes later.

### `OnDestroy()` — Line ~65

**Call chain:**
```
OnDestroy()
  └── UnregisterNetworkCallbacks()
```

Unsubscribes from all NetworkManager events to prevent callbacks on destroyed objects.

---

## Network Callback Registration

### `RegisterNetworkCallbacks()` — Line ~70

Subscribes to `NetworkManager` events:

| Event | Handler Method |
|-------|---------------|
| `OnClientConnectedCallback` | `OnClientConnectedCallback(ulong)` |
| `OnClientDisconnectCallback` | `OnClientDisconnectCallback(ulong)` |
| `OnServerStarted` | `OnServerStarted()` |
| `OnServerStopped` | `OnServerStopped(bool)` |
| `OnTransportFailure` | `OnTransportFailure()` |
| `ConnectionApprovalCallback` | `ApprovalCheck(request, response)` |

### `UnregisterNetworkCallbacks()` — Line ~85

Mirror of `RegisterNetworkCallbacks()` — removes all subscriptions.

---

## Event Delegation Methods

Each callback simply delegates to `m_CurrentState`:

| Method | Delegates To | Notes |
|--------|-------------|-------|
| `OnClientConnectedCallback(ulong clientId)` | `m_CurrentState.OnClientConnected(clientId)` | |
| `OnClientDisconnectCallback(ulong clientId)` | `m_CurrentState.OnClientDisconnect(clientId)` | |
| `OnServerStarted()` | `m_CurrentState.OnServerStarted()` | |
| `OnServerStopped(bool _)` | `m_CurrentState.OnServerStopped()` | The `bool` parameter (wasHosted) is ignored |
| `OnTransportFailure()` | `m_CurrentState.OnTransportFailure()` | |
| `ApprovalCheck(req, resp)` | `m_CurrentState.ApprovalCheck(req, resp)` | Synchronous; must complete before returning |

---

## State Transition

### `ChangeState(ConnectionState nextState)` — Line ~115

```csharp
internal void ChangeState(ConnectionState nextState)
{
    Debug.Log($"{name}: Changed state from {m_CurrentState.GetType().Name} to {nextState.GetType().Name}.");
    
    if (m_CurrentState != null)
    {
        m_CurrentState.Exit();
    }
    m_CurrentState = nextState;
    m_CurrentState.Enter();
}
```

**Important behaviors:**
1. **Logging** — Every transition is logged with old and new state names.
2. **Exit before Enter** — The old state's `Exit()` is called before the new state's `Enter()`.
3. **Synchronous** — Transition is immediate. No frame delay.
4. **Re-entrant risk** — If `Enter()` triggers another `ChangeState()` call, the original `ChangeState()` has already updated `m_CurrentState`, so callbacks during the nested `Enter()` go to the correct (newest) state.

---

## Public API Methods

These are called by your game's UI or systems:

### `StartClientIp(string playerName, string ip, ushort port)` — Line ~130

```
Delegates to: m_CurrentState.StartClientIP(playerName, ip, port)
```

Only `OfflineState` implements this; all other states ignore it (no-op from base class).

### `StartClientSession(string playerName)` — Line ~135

```
Delegates to: m_CurrentState.StartClientSession(playerName)
```

### `StartHostIp(string playerName, string ip, ushort port)` — Line ~140

```
Delegates to: m_CurrentState.StartHostIP(playerName, ip, port)
```

### `StartHostSession(string playerName)` — Line ~145

```
Delegates to: m_CurrentState.StartHostSession(playerName)
```

### `RequestShutdown()` — Line ~150

```
Delegates to: m_CurrentState.OnUserRequestedShutdown()
```

---

## Memory Layout

```
ConnectionManager (MonoBehaviour)
├── m_NetworkManager       → 8 bytes (reference)
├── m_MaxConnectedPlayers  → 4 bytes (int)
├── m_NbReconnectAttempts  → 4 bytes (int)
├── m_Offline              → 8 bytes (reference to heap object)
├── m_ClientConnecting     → 8 bytes
├── m_ClientConnected      → 8 bytes
├── m_ClientReconnecting   → 8 bytes
├── m_StartingHost         → 8 bytes
├── m_Hosting              → 8 bytes
├── m_CurrentState         → 8 bytes (pointer to one of the above)
└── m_Resolver             → 8 bytes (reference)
Total instance fields: ~80 bytes + MonoBehaviour overhead
```

---

## Thread Safety

**Not thread-safe.** All methods must be called from the main Unity thread. NetworkManager callbacks are guaranteed to fire on the main thread by Netcode. No locks or synchronization primitives are used.
