# Connection States

[← Back to Index](index.md) · [Architecture](architecture.md)

---

## Overview

The connection lifecycle is modeled as a **finite state machine** with 7 states. The abstract base class `ConnectionState` defines the interface; each concrete state overrides only the methods relevant to its phase.

### Class Hierarchy

```
ConnectionState (abstract)
├── OfflineState
└── OnlineState (abstract)
    ├── ClientConnectingState
    │   └── ClientReconnectingState (inherits ConnectingState)
    ├── ClientConnectedState
    ├── StartingHostState
    └── HostingState
```

---

## Base Classes

### `ConnectionState` (abstract)

The root of all states. Injected with:
- `ConnectionManager m_ConnectionManager` — reference back to the state machine owner
- `IPublisher<ConnectStatus> m_ConnectStatusPublisher` — broadcasts status changes to subscribers

**Virtual methods** (all no-op by default):

| Method | When Called |
|--------|------------|
| `Enter()` | State is entered |
| `Exit()` | State is exited |
| `OnClientConnected(ulong clientId)` | Client connected event from NetworkManager |
| `OnClientDisconnect(ulong clientId)` | Client disconnected event from NetworkManager |
| `OnServerStarted()` | Server started callback |
| `StartClientIP(playerName, ip, port)` | User wants to connect via IP |
| `StartClientSession(playerName)` | User wants to connect via Relay/Session |
| `StartHostIP(playerName, ip, port)` | User wants to host via IP |
| `StartHostSession(playerName)` | User wants to host via Relay/Session |
| `OnUserRequestedShutdown()` | User explicitly requests disconnect |
| `ApprovalCheck(request, response)` | Connection approval callback |
| `OnTransportFailure()` | Transport layer failure |
| `OnServerStopped()` | Server stopped callback |

### `OnlineState` (abstract)

Extends `ConnectionState` with default behavior for all "online" states:
- `OnUserRequestedShutdown()` → publishes `UserRequestedDisconnect` → transitions to `OfflineState`
- `OnTransportFailure()` → transitions to `OfflineState`

---

## Concrete States

### 1. `OfflineState`

> The default/starting state. The NetworkManager is shut down.

**Enter:**
- Calls `MultiplayerServicesFacade.EndTracking()` to clean up any session tracking
- Calls `NetworkManager.Shutdown()`
- If not already on MainMenu scene, loads it via `SceneLoaderWrapper`

**Available transitions:**
| Trigger | Target State |
|---------|-------------|
| `StartClientIP()` | `ClientConnectingState` (with `ConnectionMethodIP`) |
| `StartClientSession()` | `ClientConnectingState` (with `ConnectionMethodRelay`) |
| `StartHostIP()` | `StartingHostState` (with `ConnectionMethodIP`) |
| `StartHostSession()` | `StartingHostState` (with `ConnectionMethodRelay`) |

---

### 2. `ClientConnectingState`

> Client is actively trying to connect to a host.

**Enter:**
- Fires `ConnectClientAsync()` which calls `ConnectionMethod.SetupClientConnection()` then `NetworkManager.StartClient()`

**Transitions:**
| Event | Action | Target |
|-------|--------|--------|
| `OnClientConnected` | Publish `Success` | `ClientConnectedState` |
| `OnClientDisconnect` | Publish disconnect reason | `OfflineState` |

---

### 3. `ClientConnectedState`

> Client is connected and playing.

**Enter:**
- If a Unity Session exists, begins tracking session events via `MultiplayerServicesFacade.BeginTracking()`

**Transitions:**
| Event | Action | Target |
|-------|--------|--------|
| Disconnect (no reason or host shutdown) | Publish `Reconnecting` | `ClientReconnectingState` |
| Disconnect (with explicit reason) | Publish reason | `OfflineState` |

---

### 4. `ClientReconnectingState`

> Client lost connection and is automatically retrying.

**Inherits:** `ClientConnectingState` (reuses `ConnectClientAsync()`)

**Enter:**
- Resets attempt counter
- Starts `ReconnectCoroutine()`

**Reconnection logic:**
1. Wait `k_TimeBetweenAttempts` (5 sec) between attempts (except first)
2. Shutdown NetworkManager, wait until shutdown complete
3. Publish `ReconnectMessage` with current/max attempts
4. Wait `k_TimeBeforeFirstAttempt` (1 sec) on first attempt (lets services update)
5. Call `ConnectionMethod.SetupClientReconnectionAsync()`
6. If success → `ConnectClientAsync()` (from parent class)
7. If failure → `OnClientDisconnect(0)` to trigger next attempt or give up

**Fatal reasons that skip reconnection:**
- `UserRequestedDisconnect`
- `HostEndedSession`
- `ServerFull`
- `IncompatibleBuildType`

**Max attempts:** Configurable via `ConnectionManager.NbReconnectAttempts` (default: 2)

---

### 5. `StartingHostState`

> Host is starting up. Runs connection approval for itself.

**Enter:**
- Calls `ConnectionMethod.SetupHostConnection()` then `NetworkManager.StartHost()`

**Self-approval:**
- When `ApprovalCheck` fires for LocalClientId, approves itself and registers session data

**Transitions:**
| Event | Action | Target |
|-------|--------|--------|
| `OnServerStarted` | Publish `Success` | `HostingState` |
| `OnServerStopped` | Publish `StartHostFailed` | `OfflineState` |

---

### 6. `HostingState`

> Host is running and accepting clients.

**Enter:**
- Loads "CharSelect" scene via `SceneLoaderWrapper` (network-managed)
- If Unity Session exists, begins tracking

**Connection Approval (`ApprovalCheck`):**
1. Reject if payload > 1024 bytes (DOS protection)
2. Parse `ConnectionPayload` JSON
3. Check `GetConnectStatus()`:
   - `ServerFull` if `ConnectedClientsIds.Count >= MaxConnectedPlayers`
   - `IncompatibleBuildType` if debug build mismatch
   - `LoggedInAgain` if duplicate connection
   - `Success` otherwise
4. If approved: register `SessionPlayerData`, create player object
5. If rejected: send disconnect reason, remove from session

**Client events:**
- `OnClientConnected` → publishes `ConnectionEventMessage` with player name
- `OnClientDisconnect` → publishes disconnect, calls `SessionManager.DisconnectClient()`

**Shutdown (`OnUserRequestedShutdown`):**
- Disconnects all clients with `HostEndedSession` reason
- Transitions to `OfflineState`

---

## State Transition Summary

```
┌─────────────────────────────────────────────────────────┐
│                     OfflineState                        │
│  (Default. NetworkManager shut down. MainMenu loaded.)  │
└──────┬────────────────────────────────┬─────────────────┘
       │ StartClient*()                 │ StartHost*()
       ▼                                ▼
┌──────────────────┐            ┌──────────────────┐
│ ClientConnecting │            │  StartingHost    │
│ (StartClient)    │            │  (StartHost)     │
└───────┬──────────┘            └───────┬──────────┘
        │ success                       │ success
        ▼                               ▼
┌──────────────────┐            ┌──────────────────┐
│ ClientConnected  │            │    Hosting        │
│ (Playing)        │            │ (Accepting joins) │
└───────┬──────────┘            └──────────────────┘
        │ disconnect
        ▼
┌──────────────────┐
│ClientReconnecting│
│ (Auto-retry)     │
└──────────────────┘
```
