# `StartingHostState.cs` — Technical Reference

[← Back to Low-Level Index](index.md) · [ConnectionState](ConnectionState.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/ConnectionManagement/ConnectionState/StartingHostState.cs` |
| **Namespace** | `Unity.ConnectionManagement` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 96 |
| **Base class** | `OnlineState` |
| **Access** | `internal class` |

---

## Purpose

Transient state during host startup. Handles the self-approval check (host must approve its own connection) and transitions to `HostingState` on success.

---

## Injected Fields

| Field | Type | Attribute | Line | Description |
|-------|------|-----------|------|-------------|
| `m_MultiplayerServicesFacade` | `MultiplayerServicesFacade` | `[Inject]` | 17 | Not used directly in this state but injected for potential subclass use. |
| `m_LocalSession` | `LocalSession` | `[Inject]` | 20 | Not used directly in this state's methods. |

---

## Instance Fields

| Field | Type | Access | Line | Description |
|-------|------|--------|------|-------------|
| `m_ConnectionMethod` | `ConnectionMethodBase` | `private` | 21 | The connection strategy. Set via `Configure()`. |

---

## Methods

### `Configure(ConnectionMethodBase)` — Line 23

```csharp
public StartingHostState Configure(ConnectionMethodBase baseConnectionMethod)
{
    m_ConnectionMethod = baseConnectionMethod;
    return this;
}
```

Called by `OfflineState.StartHostIP()` / `StartHostSession()` before transition.

### `Enter()` — Line 29

```csharp
public override void Enter()
{
    StartHost();
}
```

### `Exit()` — Line 34

Empty.

### `OnServerStarted()` — Line 36

```csharp
public override void OnServerStarted()
{
    m_ConnectStatusPublisher.Publish(ConnectStatus.Success);
    m_ConnectionManager.ChangeState(m_ConnectionManager.m_Hosting);
}
```

This callback fires **after** the self-approval check succeeds and the server is fully started.

### `ApprovalCheck(request, response)` — Line 42

```csharp
public override void ApprovalCheck(
    NetworkManager.ConnectionApprovalRequest request,
    NetworkManager.ConnectionApprovalResponse response)
{
    var connectionData = request.Payload;
    var clientId = request.ClientNetworkId;

    if (clientId == m_ConnectionManager.NetworkManager.LocalClientId)
    {
        var payload = System.Text.Encoding.UTF8.GetString(connectionData);
        var connectionPayload = JsonUtility.FromJson<ConnectionPayload>(payload);

        SessionManager<SessionPlayerData>.Instance.SetupConnectingPlayerSessionData(
            clientId, connectionPayload.playerId,
            new SessionPlayerData(clientId, connectionPayload.playerName, 
                                  new NetworkGuid(), 0, true));

        response.Approved = true;
        response.CreatePlayerObject = true;
    }
}
```

**Key behavior:**
1. **Self-only** — Only processes the `LocalClientId`. During `StartHost()`, the host's own connection triggers approval before any clients can connect.
2. **Session registration** — Registers the host's own player data in `SessionManager`. Creates initial `SessionPlayerData` with:
   - `clientId` = local client ID
   - `playerName` = from payload
   - `AvatarNetworkGuid` = empty `NetworkGuid()` (set later)
   - `CurrentHitPoints` = `0`
   - `HasCharacterSpawned` = `true`
3. **Player object** — `CreatePlayerObject = true` tells Netcode to instantiate the player prefab.
4. **No Position set** — Unlike `HostingState.ApprovalCheck`, no position/rotation is set because the host handles its own spawning.

### `OnServerStopped()` — Line 62

```csharp
public override void OnServerStopped()
{
    StartHostFailed();
}
```

If the server stops before `OnServerStarted` fires, it means host startup failed.

### `StartHost()` — Line 67

```csharp
void StartHost()
{
    try
    {
        m_ConnectionMethod.SetupHostConnection();

        if (m_ConnectionMethod is ConnectionMethodIP)
        {
            if (!m_ConnectionManager.NetworkManager.StartHost())
            {
                StartHostFailed();
            }
        }
    }
    catch (Exception)
    {
        StartHostFailed();
        throw;
    }
}
```

**Same pattern as `ConnectClientAsync()`:**
1. Setup connection (configures transport, sets payload)
2. Type-check for IP → call `StartHost()`
3. For Relay, the Session SDK handles the actual start

**`StartHost()` vs `StartClient()` + `StartServer()`:** `NetworkManager.StartHost()` is equivalent to starting both client and server. It triggers:
- `ConnectionApprovalCallback` (self-approval)
- `OnServerStarted` callback
- `OnClientConnected` callback

### `StartHostFailed()` — Line 89

```csharp
void StartHostFailed()
{
    m_ConnectStatusPublisher.Publish(ConnectStatus.StartHostFailed);
    m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
}
```

---

## Call Chain — Successful IP Host Start

```
OfflineState.StartHostIP()
  → StartingHostState.Configure(methodIP)
  → ChangeState(StartingHost)
    → StartingHostState.Enter()
      → StartHost()
        → ConnectionMethodIP.SetupHostConnection()
        → NetworkManager.StartHost()             ← starts server + client
          → ApprovalCheck(self)                   ← approve the host itself
            → SessionManager.SetupConnectingPlayerSessionData()
            → response.Approved = true
          → OnServerStarted()                     ← server is ready
            → Publish(Success)
            → ChangeState(Hosting)
              → HostingState.Enter()
                → SceneLoaderWrapper.LoadScene("CharSelect", network: true)
```
