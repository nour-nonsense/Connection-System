# Connection Methods

[← Back to Index](index.md) · [Connection States](connection-states.md)

---

## Overview

A **ConnectionMethod** encapsulates all the transport-level setup needed before `NetworkManager.StartClient()` or `NetworkManager.StartHost()` is called. The abstract base class `ConnectionMethodBase` defines the contract; two implementations are provided.

---

## Class Hierarchy

```
ConnectionMethodBase (abstract)
├── ConnectionMethodIP        ← Direct IP/Port via UnityTransport
└── ConnectionMethodRelay     ← Unity Relay via Multiplayer Sessions
```

---

## `ConnectionMethodBase` (abstract)

### Constructor

```csharp
ConnectionMethodBase(ConnectionManager connectionManager, 
                     ProfileManager profileManager, 
                     string playerName)
```

### Abstract Methods

| Method | Purpose |
|--------|---------|
| `SetupHostConnection()` | Configure transport for hosting |
| `SetupClientConnection()` | Configure transport for joining |
| `SetupClientReconnectionAsync()` | Prepare for reconnection attempt |

### Protected Helpers

#### `SetConnectionPayload(playerId, playerName)`
Serializes a `ConnectionPayload` to JSON and sets it as `NetworkConfig.ConnectionData`. This payload is read during connection approval on the host.

```csharp
// Payload structure:
{
    "playerId": "guid-or-auth-id",
    "playerName": "Player1",
    "isDebug": true
}
```

#### `GetPlayerId()`
Returns a unique player identifier:
- If **UGS is initialized and signed in** → `AuthenticationService.Instance.PlayerId`
- Otherwise → `ClientPrefs.GetGuid() + ProfileManager.Profile` (offline fallback)

---

## `ConnectionMethodIP`

Simple direct connection using Unity Transport (UTP).

### Constructor

```csharp
ConnectionMethodIP(string ip, ushort port, 
                   ConnectionManager connectionManager, 
                   ProfileManager profileManager, 
                   string playerName)
```

### Behavior

| Method | What it does |
|--------|-------------|
| `SetupHostConnection()` | Sets payload + configures UTP with IP:Port |
| `SetupClientConnection()` | Sets payload + configures UTP with IP:Port |
| `SetupClientReconnectionAsync()` | Returns `(true, true)` — no extra setup needed |

### Usage

```csharp
// From your UI button handler:
connectionManager.StartHostIp("MyName", "127.0.0.1", 7777);
connectionManager.StartClientIp("MyName", "192.168.1.100", 7777);
```

---

## `ConnectionMethodRelay`

Uses **Unity Multiplayer Sessions SDK** with **Relay** for NAT-punchthrough-free connections.

### Constructor

```csharp
ConnectionMethodRelay(MultiplayerServicesFacade multiplayerServicesFacade,
                      ConnectionManager connectionManager,
                      ProfileManager profileManager,
                      string playerName)
```

### Behavior

| Method | What it does |
|--------|-------------|
| `SetupHostConnection()` | Sets payload (Relay allocation handled by Session SDK) |
| `SetupClientConnection()` | Sets payload (Relay join handled by Session SDK) |
| `SetupClientReconnectionAsync()` | Calls `MultiplayerServicesFacade.ReconnectToSessionAsync()` |

### Reconnection Details

When reconnecting via Relay:
1. Checks if `CurrentUnitySession` still exists
2. If not → returns `(false, false)` — stops all reconnection attempts
3. If yes → calls `ReconnectToSessionAsync()` to rejoin the session
4. Success → returns `(true, true)` → proceeds with `StartClient()`

### Usage

```csharp
// From your UI button handler:
connectionManager.StartHostSession("MyName");
connectionManager.StartClientSession("MyName");
```

---

## Adding a Custom Connection Method

To add a new transport or connection flow:

1. Create a new class extending `ConnectionMethodBase`:

```csharp
class ConnectionMethodSteam : ConnectionMethodBase
{
    public ConnectionMethodSteam(ConnectionManager cm, ProfileManager pm, string name)
        : base(cm, pm, name) { }

    public override void SetupHostConnection()
    {
        SetConnectionPayload(GetPlayerId(), m_PlayerName);
        // Configure Steam transport here
    }

    public override void SetupClientConnection()
    {
        SetConnectionPayload(GetPlayerId(), m_PlayerName);
        // Configure Steam transport here
    }

    public override Task<(bool success, bool shouldTryAgain)> SetupClientReconnectionAsync()
    {
        // Steam reconnection logic
        return Task.FromResult((true, true));
    }
}
```

2. Add a new `StartClient*` / `StartHost*` method to `OfflineState` (or use the existing ones with your new method).
