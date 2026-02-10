# Message Structs — Technical Reference

[← Back to Low-Level Index](index.md)

---

This document covers all message structs used with the PubSub system.

---

## `ConnectStatus` (enum)

| File | `Runtime/ConnectionManagement/ConnectionManager.cs` (nested type) |
|------|------------------------------------------------------------------|
| **Namespace** | `Unity.ConnectionManagement` |

```csharp
public enum ConnectStatus
{
    Undefined,
    Success,
    ServerFull,
    LoggedInAgain,
    UserRequestedDisconnect,
    GenericDisconnect,
    Reconnecting,
    IncompatibleBuildType,
    HostEndedSession,
    StartHostFailed,
    StartClientFailed
}
```

**Network serialization:** Serialized to JSON via `JsonUtility.ToJson()` and sent as `DisconnectReason` string through Netcode. Deserialized back via `JsonUtility.FromJson<ConnectStatus>()`. The JSON representation is the integer value, e.g., `3` for `LoggedInAgain`.

### Usage Summary

| Value | Published By | Triggers |
|-------|-------------|----------|
| `Success` | `ClientConnectingState.OnClientConnected()`, `StartingHostState.OnServerStarted()` | UI: hide loading screen |
| `ServerFull` | `HostingState.GetConnectStatus()` → sent as disconnect reason | Client: show "server full" |
| `LoggedInAgain` | `HostingState.GetConnectStatus()` → sent as disconnect reason | Client: show "already connected" |
| `UserRequestedDisconnect` | `OnlineState.OnUserRequestedShutdown()` | UI: return to menu |
| `GenericDisconnect` | `HostingState.OnServerStopped()`, various fallbacks | UI: show generic error |
| `Reconnecting` | `ClientConnectedState.OnClientDisconnect()` | UI: show reconnection progress |
| `IncompatibleBuildType` | `HostingState.GetConnectStatus()` → sent as disconnect reason | Client: show build mismatch |
| `HostEndedSession` | `HostingState.OnUserRequestedShutdown()` → sent per-client | Client: show "host left" |
| `StartHostFailed` | `StartingHostState.StartHostFailed()` | UI: show host failure |
| `StartClientFailed` | `ClientConnectingState.StartingClientFailed()` | UI: show client failure |

---

## `ConnectionEventMessage` (struct)

| File | `Runtime/ConnectionManagement/ConnectionManager.cs` (nested type) |
|------|------------------------------------------------------------------|
| **Namespace** | `Unity.ConnectionManagement` |

```csharp
public struct ConnectionEventMessage
{
    public ConnectStatus ConnectStatus;  // 4 bytes (enum → int)
    public string PlayerName;            // 8 bytes (reference)
}
```

**Purpose:** Carries player-identity-aware events. Published by `HostingState` when a specific player connects or disconnects.

**Distinction from `ConnectStatus`:** `ConnectStatus` is a simple status code. `ConnectionEventMessage` adds `PlayerName` for display in notifications like "Player X connected".

---

## `ReconnectMessage` (struct)

| File | `Runtime/ConnectionManagement/ConnectionManager.cs` (nested type) |
|------|------------------------------------------------------------------|
| **Namespace** | `Unity.ConnectionManagement` |

```csharp
public struct ReconnectMessage
{
    public int CurrentAttempt;  // 4 bytes
    public int MaxAttempt;      // 4 bytes
}
```

**Purpose:** Progress indicator for reconnection. Published by `ClientReconnectingState`:
- During each attempt: `(currentAttempt, maxAttempts)` — e.g., (1, 3)
- On exit: `(maxAttempts, maxAttempts)` — signals "done" regardless of success/failure

---

## `UnityServiceErrorMessage` (struct)

| File | `Runtime/UnityServices/Infrastructure/Messages/UnityServiceErrorMessage.cs` |
|------|-----------------------------------------------------------------------------|
| **Namespace** | `Unity.ConnectionManagement.UnityServices` |

```csharp
public struct UnityServiceErrorMessage
{
    public string Title;     // 8 bytes (reference)
    public string Message;   // 8 bytes (reference)
}
```

**Purpose:** Error display from UGS operations. Published by `MultiplayerServicesFacade.PublishError()` when session operations fail.

**Title values used:**
- `"Authentication Error"` — Sign-in failure
- `"Session Error"` — Session operation failure

---

## `SessionListFetchedMessage` (struct)

| File | `Runtime/UnityServices/Infrastructure/Messages/SessionListFetchedMessage.cs` |
|------|------------------------------------------------------------------------------|
| **Namespace** | `Unity.ConnectionManagement.UnityServices` |

```csharp
public struct SessionListFetchedMessage
{
    public IList<ISessionInfo> Sessions;  // 8 bytes (reference)
}
```

**Purpose:** Published when a session query completes. The `Sessions` list contains metadata for each available session.

**Not currently published by the package.** This channel is defined but the `QueryAllSessions` result is returned directly via `Task<>`, not published via PubSub. The channel exists for consumer code that prefers reactive patterns.

---

## Memory Notes

All message structs are passed by value through `Publish()`. Since most contain reference types (`string`, `IList`), the actual memory overhead of copying is minimal (just pointer copies, ~8-16 bytes per message).
