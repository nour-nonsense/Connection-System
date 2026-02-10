# `OfflineState.cs` — Technical Reference

[← Back to Low-Level Index](index.md) · [ConnectionState](ConnectionState.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/ConnectionManagement/ConnectionState/OfflineState.cs` |
| **Namespace** | `Unity.ConnectionManagement` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 66 |
| **Base class** | `ConnectionState` (directly, not OnlineState) |
| **Access** | `internal class` |

---

## Purpose

The default/initial state. Represents a fully disconnected state where the `NetworkManager` is shut down and the player is on the main menu.

---

## Injected Fields

| Field | Type | Attribute | Line | Description |
|-------|------|-----------|------|-------------|
| `m_MultiplayerServicesFacade` | `MultiplayerServicesFacade` | `[Inject]` | ~12 | Used to call `EndTracking()` when entering offline. |
| `m_ProfileManager` | `ProfileManager` | `[Inject]` | ~14 | Passed to `ConnectionMethod` constructors for player ID resolution. |

---

## Method Internals

### `Enter()` — Line ~17

```csharp
public override void Enter()
{
    m_MultiplayerServicesFacade.EndTracking();
    m_ConnectionManager.NetworkManager.Shutdown();
    if (SceneLoaderWrapper.Instance != null)
    {
        if (!SceneManager.GetActiveScene().name.Equals("MainMenu"))
        {
            SceneLoaderWrapper.Instance.LoadScene("MainMenu", useNetworkSceneManager: false);
        }
    }
}
```

**Step-by-step:**
1. `EndTracking()` — Stops session event subscriptions. If host, deletes session. If client, leaves session.
2. `Shutdown()` — Stops NetworkManager. Disconnects all peers. Releases sockets.
3. **Conditional scene load** — Only loads MainMenu if not already on it. Uses `SceneManager` (not network) because there's no active network session.

**Edge case:** If `SceneLoaderWrapper.Instance` is null (not initialized yet), scene loading is skipped silently.

### `Exit()` — Line ~30

```csharp
public override void Exit() { }
```

No cleanup needed when leaving offline state.

### `StartClientIP(playerName, ip, port)` — Line ~33

```csharp
public override void StartClientIP(string playerName, string ipaddress, ushort port)
{
    var connectionMethod = new ConnectionMethodIP(
        ipaddress, port,
        m_ConnectionManager,
        m_ProfileManager,
        playerName);
    m_ConnectionManager.m_ClientConnecting.Configure(connectionMethod);
    m_ConnectionManager.ChangeState(m_ConnectionManager.m_ClientConnecting);
}
```

**Call chain:**
```
StartClientIP()
  ├── new ConnectionMethodIP(ip, port, cm, pm, name)   ← allocates method on heap
  ├── ClientConnectingState.Configure(method)           ← stores method reference
  └── ChangeState(m_ClientConnecting)
        ├── OfflineState.Exit()                         ← no-op
        └── ClientConnectingState.Enter()               ← starts connection
```

**GC note:** A new `ConnectionMethodIP` is allocated on each connection attempt. This is acceptable since connections are infrequent.

### `StartClientSession(playerName)` — Line ~43

Same pattern as `StartClientIP` but creates `ConnectionMethodRelay` instead. Requires `MultiplayerServicesFacade` injection.

### `StartHostIP(playerName, ip, port)` — Line ~52

```csharp
public override void StartHostIP(string playerName, string ipaddress, ushort port)
{
    var connectionMethod = new ConnectionMethodIP(
        ipaddress, port,
        m_ConnectionManager,
        m_ProfileManager,
        playerName);
    m_ConnectionManager.m_StartingHost.Configure(connectionMethod);
    m_ConnectionManager.ChangeState(m_ConnectionManager.m_StartingHost);
}
```

Same pattern but configures and transitions to `StartingHostState`.

### `StartHostSession(playerName)` — Line ~62

### `StartServerIP(ip, port)` — Line ~70

```csharp
public override void StartServerIP(string ipaddress, int port)
{
    var connectionMethod = new ConnectionMethodIP(
        ipaddress, (ushort)port,
        m_ConnectionManager,
        m_ProfileManager,
        "DedicatedServer");
    m_ConnectionManager.ChangeState(m_ConnectionManager.m_StartingServer.Configure(connectionMethod));
}
```

Transitions to `StartingServerState`. The player name is hardcoded to "DedicatedServer" since headless instances don't have user profiles.

### `StartHostSession(playerName)` — Line ~62

Same pattern with `ConnectionMethodRelay` → `StartingHostState`.

---

## State Diagram (from this state)

```
                   ┌─────────────────────┐
                   │     OfflineState     │
                   │                     │
StartClientIP()───►│──► ClientConnecting │
StartClientSession()►│                   │
                   │                     │
StartHostIP()─────►│──► StartingHost     │
StartHostSession()─►│                   │
                   └─────────────────────┘
```

---

## Important Notes

1. **Not an OnlineState** — `OfflineState` extends `ConnectionState` directly, NOT `OnlineState`. Therefore `OnUserRequestedShutdown()` and `OnTransportFailure()` are inherited as no-ops from the base class.
2. **Scene name coupling** — The string `"MainMenu"` is hardcoded. Users must either name their scene "MainMenu" or modify this state.
3. **No Start*() guards** — If `StartClientIP` is called from any other state, it's a no-op (base class default). Only `OfflineState` handles these methods.
