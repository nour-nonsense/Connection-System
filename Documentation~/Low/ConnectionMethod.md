# `ConnectionMethod.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/ConnectionManagement/ConnectionMethod.cs` |
| **Namespace** | `Unity.ConnectionManagement` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 168 |
| **Contains** | 3 classes: `ConnectionMethodBase`, `ConnectionMethodIP`, `ConnectionMethodRelay` |

---

## `ConnectionMethodBase` (abstract)

### Access

`public abstract class` — exposed for external subclassing.

### Constructor

```csharp
ConnectionMethodBase(ConnectionManager connectionManager, 
                     ProfileManager profileManager, 
                     string playerName)
```

All three parameters stored as `protected readonly` fields.

### Protected Fields

| Field | Type | Access | Description |
|-------|------|--------|-------------|
| `m_ConnectionManager` | `ConnectionManager` | `protected readonly` | Reference to the state machine owner |
| `m_ProfileManager` | `ProfileManager` | `protected readonly` | For resolving player ID via profile |
| `m_PlayerName` | `string` | `protected readonly` | Display name to include in connection payload |

### Abstract Methods

```csharp
public abstract void SetupHostConnection();
public abstract void SetupClientConnection();
public abstract Task<(bool success, bool shouldTryAgain)> SetupClientReconnectionAsync();
```

### `SetConnectionPayload(string playerId, string playerName)` — Protected

```csharp
protected void SetConnectionPayload(string playerId, string playerName)
{
    var payload = JsonUtility.ToJson(new ConnectionPayload()
    {
        playerId = playerId,
        playerName = playerName,
        isDebug = Debug.isDebugBuild
    });

    var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
    m_ConnectionManager.NetworkManager.NetworkConfig.ConnectionData = payloadBytes;
}
```

**Serialization chain:**
1. `ConnectionPayload` struct → `JsonUtility.ToJson` → JSON string
2. JSON string → `Encoding.UTF8.GetBytes` → `byte[]`
3. `byte[]` → assigned to `NetworkConfig.ConnectionData`

**Deserialization** happens in `HostingState.ApprovalCheck()` (reverse process).

**Payload size:** For a typical player name (20 chars) and GUID (36 chars), the JSON payload is ~100 bytes. Well under the 1024 byte limit.

### `GetPlayerId()` — Protected

```csharp
protected string GetPlayerId()
{
    if (Unity.Services.Core.UnityServices.State != ServicesInitializationState.Initialized)
    {
        return ClientPrefs.GetGuid() + m_ProfileManager.Profile;
    }

    return AuthenticationService.Instance.IsSignedIn 
        ? AuthenticationService.Instance.PlayerId 
        : ClientPrefs.GetGuid() + m_ProfileManager.Profile;
}
```

**Resolution order:**
1. If UGS not initialized → offline fallback: `ClientPrefs.GetGuid() + Profile`
2. If UGS initialized but not signed in → offline fallback
3. If UGS initialized AND signed in → `AuthenticationService.Instance.PlayerId`

**Offline ID format:** `"550e8400-e29b-41d4-a716-446655440000default"` (GUID + profile name). The profile suffix ensures unique IDs when running multiple editor instances of the same project.

---

## `ConnectionMethodIP`

### Access

`internal class` — not visible outside the assembly.

### Additional Fields

| Field | Type | Access | Description |
|-------|------|--------|-------------|
| `m_Ipaddress` | `string` | `readonly` | Target IP address |
| `m_Port` | `ushort` | `readonly` | Target port |

### `SetupHostConnection()` — Implementation

```csharp
public override void SetupHostConnection()
{
    SetConnectionPayload(GetPlayerId(), m_PlayerName);
    var utp = (UnityTransport)m_ConnectionManager.NetworkManager.NetworkConfig.NetworkTransport;
    utp.SetConnectionData(m_Ipaddress, m_Port);
}
```

**Cast:** Assumes transport is `UnityTransport`. Will throw `InvalidCastException` if using a different transport.

**`SetConnectionData`:** Configures both listen address (for server) and connection address (for client) simultaneously. For the host, this sets the port to listen on.

### `SetupClientConnection()` — Implementation

Identical to `SetupHostConnection()`. Same payload setup, same transport configuration.

### `SetupClientReconnectionAsync()` — Implementation

```csharp
public override Task<(bool success, bool shouldTryAgain)> SetupClientReconnectionAsync()
{
    return Task.FromResult((true, true));
}
```

**No-op:** IP connections need no special reconnection setup. Returns synchronous completed task.

---

## `ConnectionMethodRelay`

### Access

`internal class` — not visible outside the assembly.

### Additional Fields

| Field | Type | Access | Description |
|-------|------|--------|-------------|
| `m_MultiplayerServicesFacade` | `MultiplayerServicesFacade` | `readonly` | Facade for session management and reconnection |

### `SetupHostConnection()` — Implementation

```csharp
public override void SetupHostConnection()
{
    SetConnectionPayload(GetPlayerId(), m_PlayerName);
}
```

**Relay difference:** Only sets the payload. The actual Relay allocation and transport configuration is handled by the Session SDK when `MultiplayerService.Instance.CreateSessionAsync()` is called with `.WithRelayNetwork()`.

### `SetupClientConnection()` — Implementation

Same as host — payload only. Session SDK handles Relay join.

### `SetupClientReconnectionAsync()` — Implementation

```csharp
public override async Task<(bool success, bool shouldTryAgain)> SetupClientReconnectionAsync()
{
    if (m_MultiplayerServicesFacade.CurrentUnitySession == null)
    {
        return (false, false); // Can't reconnect without a session
    }

    var reconnected = await m_MultiplayerServicesFacade.ReconnectToSessionAsync();
    if (reconnected != null)
    {
        m_MultiplayerServicesFacade.SetRemoteSession(reconnected);
        return (true, true);
    }
    
    return (false, false); // Session gone, stop trying
}
```

**Return values:**
| `success` | `shouldTryAgain` | Meaning |
|-----------|-----------------|---------|
| `true` | `true` | Reconnected to session, proceed with `StartClient()` |
| `false` | `true` | This attempt failed but the session exists, try again |
| `false` | `false` | Session is gone, stop all attempts immediately |

**`shouldTryAgain = false`** causes `ClientReconnectingState` to set `m_NbAttempts = max`, which prevents any further attempts.

---

## Memory / GC Notes

- `ConnectionMethodBase` instances are allocated on each `StartClient*` / `StartHost*` call from `OfflineState`
- They persist only while the connection state is active
- `ConnectionPayload` → JSON → bytes allocation happens once per connection (not per frame)
- `Task.FromResult` (in `ConnectionMethodIP`) returns a cached completed task — no allocation
