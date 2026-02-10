# `ClientConnectingState.cs` — Technical Reference

[← Back to Low-Level Index](index.md) · [ConnectionState](ConnectionState.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/ConnectionManagement/ConnectionState/ClientConnectingState.cs` |
| **Namespace** | `Unity.ConnectionManagement` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 82 |
| **Base class** | `OnlineState` |
| **Access** | `internal class` |

---

## Purpose

Handles the "client is trying to connect" phase. Configures the transport via a `ConnectionMethodBase` then calls `NetworkManager.StartClient()`. Success → `ClientConnectedState`. Failure → `OfflineState`.

---

## Instance Fields

| Field | Type | Access | Line | Description |
|-------|------|--------|------|-------------|
| `m_ConnectionMethod` | `ConnectionMethodBase` | `protected` | 13 | The connection strategy. Set via `Configure()`. Protected because `ClientReconnectingState` inherits and accesses it. |

---

## Methods

### `Configure(ConnectionMethodBase)` — Line 15

```csharp
public ClientConnectingState Configure(ConnectionMethodBase baseConnectionMethod)
{
    m_ConnectionMethod = baseConnectionMethod;
    return this;
}
```

**Returns `this`** for potential fluent chaining (not currently used). Called by `OfflineState` before transitioning.

### `Enter()` — Line 21

```csharp
public override void Enter()
{
#pragma warning disable 4014
    ConnectClientAsync();
#pragma warning restore 4014
}
```

**Note:** The `#pragma warning disable 4014` suppresses "call is not awaited" warning. `ConnectClientAsync()` is `internal` (not `async Task`) — it's a synchronous method that may throw. The pragma is arguably misleading. The actual execution is synchronous for IP connections.

### `Exit()` — Line 28

```csharp
public override void Exit() { }
```

No cleanup needed.

### `OnClientConnected(ulong _)` — Line 30

```csharp
public override void OnClientConnected(ulong _)
{
    m_ConnectStatusPublisher.Publish(ConnectStatus.Success);
    m_ConnectionManager.ChangeState(m_ConnectionManager.m_ClientConnected);
}
```

**Parameter ignored:** `clientId` is discarded because the connecting client knows its own ID. Publishes `Success` then transitions.

### `OnClientDisconnect(ulong _)` — Line 36

```csharp
public override void OnClientDisconnect(ulong _)
{
    StartingClientFailed();
}
```

Delegates to the private error handler.

### `StartingClientFailed()` — Line 42

```csharp
void StartingClientFailed()
{
    var disconnectReason = m_ConnectionManager.NetworkManager.DisconnectReason;
    if (string.IsNullOrEmpty(disconnectReason))
    {
        m_ConnectStatusPublisher.Publish(ConnectStatus.StartClientFailed);
    }
    else
    {
        var connectStatus = JsonUtility.FromJson<ConnectStatus>(disconnectReason);
        m_ConnectStatusPublisher.Publish(connectStatus);
    }
    m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
}
```

**Disconnect reason parsing:**
1. If no reason → generic `StartClientFailed`
2. If reason exists → deserialize JSON to `ConnectStatus` enum (server sends `JsonUtility.ToJson(status)`)
3. Either way → go offline

### `ConnectClientAsync()` — Line 58

```csharp
internal void ConnectClientAsync()
{
    try
    {
        m_ConnectionMethod.SetupClientConnection();
        
        if (m_ConnectionMethod is ConnectionMethodIP)
        {
            if (!m_ConnectionManager.NetworkManager.StartClient())
            {
                throw new Exception("NetworkManager StartClient failed");
            }
        }
    }
    catch (Exception e)
    {
        Debug.LogError("Error connecting client, see following exception");
        Debug.LogException(e);
        StartingClientFailed();
        throw;
    }
}
```

**Step-by-step:**
1. `SetupClientConnection()` — Configures transport (sets IP:Port or Relay data) and sets connection payload
2. **Type check** — Only calls `StartClient()` for `ConnectionMethodIP`. For Relay, the Session SDK handles the actual connection start.
3. **Error handling** — If `StartClient()` returns false or any exception occurs, calls `StartingClientFailed()` then re-throws

**Important:** `ConnectClientAsync()` is `internal` (not `private`) because `ClientReconnectingState` inherits this class and calls this method directly during reconnection.

---

## Call Chain — Successful IP Connection

```
OfflineState.StartClientIP()
  → ClientConnectingState.Configure(methodIP)
  → ChangeState(ClientConnecting)
    → ClientConnectingState.Enter()
      → ConnectClientAsync()
        → ConnectionMethodIP.SetupClientConnection()
          → SetConnectionPayload(playerId, name)   ← serializes to JSON
          → UnityTransport.SetConnectionData(ip, port)
        → NetworkManager.StartClient()              ← initiates connection
          ... (async network handshake) ...
        → NetworkManager fires OnClientConnected
      → ClientConnectingState.OnClientConnected()
        → Publish(Success)
        → ChangeState(ClientConnected)
```

---

## Threading

All synchronous on the main thread. `ConnectClientAsync()` name is misleading — there's no `await`. The actual async part is NetworkManager's internal connection process, which fires callbacks later.
