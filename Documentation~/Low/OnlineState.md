# `OnlineState.cs` — Technical Reference

[← Back to Low-Level Index](index.md) · [ConnectionState](ConnectionState.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/ConnectionManagement/ConnectionState/OnlineState.cs` |
| **Namespace** | `Unity.ConnectionManagement` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 22 |
| **Base class** | `ConnectionState` |
| **Access** | `abstract class` (internal) |

---

## Purpose

Intermediate abstract base for all states where the network is active (client connecting, connected, reconnecting, hosting, starting host). Provides **default implementations** for two events that behave identically across all online states.

---

## Overridden Methods

### `OnUserRequestedShutdown()` — Line 8

```csharp
public override void OnUserRequestedShutdown()
{
    m_ConnectStatusPublisher.Publish(ConnectStatus.UserRequestedDisconnect);
    m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
}
```

**Behavior:** Publishes `UserRequestedDisconnect` then transitions to `OfflineState`. This is the default for all online states. Only `HostingState` overrides this (to disconnect all clients first before going offline).

**Call chain:**
```
User clicks "Disconnect" → ConnectionManager.RequestShutdown()
    → m_CurrentState.OnUserRequestedShutdown()
        → Publish(UserRequestedDisconnect)  ← subscribers notified (UI)
        → ChangeState(m_Offline)
            → OnlineState.Exit()            ← current state cleanup
            → OfflineState.Enter()          ← shutdown + load MainMenu
```

### `OnTransportFailure()` — Line 15

```csharp
public override void OnTransportFailure()
{
    m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
}
```

**Behavior:** Silently transitions to `OfflineState`. No status is published — the transport failure itself is the notification. No state overrides this.

**When triggered:** Called by NetworkManager when the underlying transport (UTP) encounters an unrecoverable error (socket closed, binding failed, etc.).

---

## Inheritance Note

`HostingState` overrides `OnUserRequestedShutdown()` to add client-disconnection logic before calling `ChangeState(m_Offline)`. It does **not** call `base.OnUserRequestedShutdown()` — the `Publish(UserRequestedDisconnect)` is intentionally omitted because the host sends `HostEndedSession` to each client individually.
