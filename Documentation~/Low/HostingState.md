# `HostingState.cs` — Technical Reference

[← Back to Low-Level Index](index.md) · [ConnectionState](ConnectionState.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/ConnectionManagement/ConnectionState/HostingState.cs` |
| **Namespace** | `Unity.ConnectionManagement` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 165 |
| **Base class** | `OnlineState` |
| **Access** | `internal class` |

---

## Purpose

Active hosting state. Handles incoming client connection approvals, tracks client connects/disconnects, and manages graceful shutdown with client notification.

---

## Injected Fields

| Field | Type | Attribute | Line | Description |
|-------|------|-----------|------|-------------|
| `m_MultiplayerServicesFacade` | `MultiplayerServicesFacade` | `[Inject]` | 18 | Used to begin session tracking and remove players from UGS sessions. |
| `m_ConnectionEventPublisher` | `IPublisher<ConnectionEventMessage>` | `[Inject]` | 20 | Publishes connect/disconnect events with player names. Different from `m_ConnectStatusPublisher` — this carries player identity. |

---

## Constants

| Constant | Type | Value | Line | Description |
|----------|------|-------|------|-------------|
| `k_MaxConnectPayload` | `int` | `1024` | 24 | Maximum bytes for connection payload. DOS mitigation. |

---

## Methods

### `Enter()` — Line 26

```csharp
public override void Enter()
{
    SceneLoaderWrapper.Instance.LoadScene("CharSelect", useNetworkSceneManager: true);

    if (m_MultiplayerServicesFacade.CurrentUnitySession != null)
    {
        m_MultiplayerServicesFacade.BeginTracking();
    }
}
```

**Step 1: Scene load** — Forces all connected clients + host to load "CharSelect" scene via `NetworkSceneManager`. This is a **design decision** — in Boss Room, the host always starts at character selection.

**Step 2: Session tracking** — If using UGS Sessions (Relay), starts monitoring session events (player join/leave, session delete, etc.).

**Hardcoded scene name:** `"CharSelect"` is hardcoded. Users must either rename their scene or modify this line.

### `Exit()` — Line 38

```csharp
public override void Exit()
{
    SessionManager<SessionPlayerData>.Instance.OnServerEnded();
}
```

Clears ALL player session data (both connected and disconnected). This is permanent cleanup — no reconnection is possible after the host exits.

### `OnClientConnected(ulong clientId)` — Line 43

```csharp
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
        var reason = JsonUtility.ToJson(ConnectStatus.GenericDisconnect);
        m_ConnectionManager.NetworkManager.DisconnectClient(clientId, reason);
    }
}
```

**Normal flow:** Client passed approval → data registered → publish success event with name.

**Error flow:** If no data found (shouldn't happen), force-disconnect the client. This is a safety net.

### `OnClientDisconnect(ulong clientId)` — Line 60

```csharp
public override void OnClientDisconnect(ulong clientId)
{
    if (clientId != m_ConnectionManager.NetworkManager.LocalClientId)
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
}
```

**Guard:** Ignores `LocalClientId` — the host disconnecting itself is handled separately.

**Data flow:**
1. Look up `playerId` from `clientId`
2. Get `SessionPlayerData` for the player name
3. Publish disconnect event with name
4. Mark as disconnected (data preserved for reconnection)

### `OnUserRequestedShutdown()` — Line 77

```csharp
public override void OnUserRequestedShutdown()
{
    var reason = JsonUtility.ToJson(ConnectStatus.HostEndedSession);
    for (var i = m_ConnectionManager.NetworkManager.ConnectedClientsIds.Count - 1; i >= 0; i--)
    {
        var id = m_ConnectionManager.NetworkManager.ConnectedClientsIds[i];
        if (id != m_ConnectionManager.NetworkManager.LocalClientId)
        {
            m_ConnectionManager.NetworkManager.DisconnectClient(id, reason);
        }
    }
    m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
}
```

**Overrides OnlineState:** Does NOT call `base.OnUserRequestedShutdown()`.

**Reverse iteration:** Iterates backwards because `DisconnectClient` modifies the `ConnectedClientsIds` collection. Forward iteration would skip elements or throw.

**Skip self:** The host doesn't disconnect itself — that's handled by the `ChangeState(Offline)` → `Enter()` → `NetworkManager.Shutdown()` chain.

**Client-side effect:** Each disconnected client receives `"HostEndedSession"` as `DisconnectReason`, which `ClientConnectedState.OnClientDisconnect` deserializes and sends to `OfflineState` (not reconnecting, since `HostEndedSession` is a valid explicit reason).

### `OnServerStopped()` — Line 91

```csharp
public override void OnServerStopped()
{
    m_ConnectStatusPublisher.Publish(ConnectStatus.GenericDisconnect);
    m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
}
```

Called when the server stops unexpectedly (not from user request). Publishes generic disconnect and goes offline.

### `ApprovalCheck(request, response)` — Line 111

The most complex method. Full client connection approval logic:

```
ApprovalCheck(request, response)
  │
  ├── [payload.Length > 1024]
  │     └── response.Approved = false; return     ← DOS protection
  │
  ├── Deserialize payload (UTF-8 → JSON → ConnectionPayload)
  ├── GetConnectStatus(connectionPayload)
  │     ├── ConnectedClientsIds.Count >= MaxConnectedPlayers → ServerFull
  │     ├── isDebug != Debug.isDebugBuild → IncompatibleBuildType  
  │     ├── IsDuplicateConnection(playerId) → LoggedInAgain
  │     └── else → Success
  │
  ├── [Success]
  │     ├── SessionManager.SetupConnectingPlayerSessionData(
  │     │     clientId, playerId, new SessionPlayerData(...))
  │     ├── response.Approved = true
  │     ├── response.CreatePlayerObject = true
  │     ├── response.Position = Vector3.zero
  │     └── response.Rotation = Quaternion.identity
  │
  └── [Not Success]
        ├── response.Approved = false
        ├── response.Reason = JsonUtility.ToJson(status)
        └── if (UGS session exists)
              └── RemovePlayerFromSessionAsync(playerId)
```

**Position:** All approved clients spawn at `Vector3.zero` with identity rotation. The game is expected to reposition them later (e.g., in character select).

**Session cleanup:** If a client is rejected but was added to the UGS session, they're removed via `RemovePlayerFromSessionAsync()`.

### `GetConnectStatus(ConnectionPayload)` — Line 148

```csharp
ConnectStatus GetConnectStatus(ConnectionPayload connectionPayload)
{
    if (m_ConnectionManager.NetworkManager.ConnectedClientsIds.Count >= 
        m_ConnectionManager.MaxConnectedPlayers)
    {
        return ConnectStatus.ServerFull;
    }

    if (connectionPayload.isDebug != Debug.isDebugBuild)
    {
        return ConnectStatus.IncompatibleBuildType;
    }

    return SessionManager<SessionPlayerData>.Instance
        .IsDuplicateConnection(connectionPayload.playerId) ?
        ConnectStatus.LoggedInAgain : ConnectStatus.Success;
}
```

**Check order matters:**
1. Server full (cheapest check — just a count comparison)
2. Build type mismatch (prevents debug/release mixing)
3. Duplicate connection (dictionary lookup)

---

## Security Notes

- **Payload size limit:** 1024 bytes max prevents large-buffer DOS attacks
- **Build type check:** Light protection against incompatible clients
- **No authentication:** The system trusts client-provided `playerId`. A malicious client could impersonate another player by reusing their GUID. The code comments explicitly acknowledge this limitation.
