# `ClientConnectedState.cs` — Technical Reference

[← Back to Low-Level Index](index.md) · [ConnectionState](ConnectionState.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/ConnectionManagement/ConnectionState/ClientConnectedState.cs` |
| **Namespace** | `Unity.ConnectionManagement` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 44 |
| **Base class** | `OnlineState` |
| **Access** | `internal class` |

---

## Purpose

Represents a fully connected client. Minimal logic — its main responsibility is deciding whether to reconnect or go offline when disconnected.

---

## Injected Fields

| Field | Type | Attribute | Access | Line | Description |
|-------|------|-----------|--------|------|-------------|
| `m_MultiplayerServicesFacade` | `MultiplayerServicesFacade` | `[Inject]` | `protected` | 13 | Used to begin session tracking on entry. Protected for potential subclass access. |

---

## Methods

### `Enter()` — Line 16

```csharp
public override void Enter()
{
    if (m_MultiplayerServicesFacade.CurrentUnitySession != null)
    {
        m_MultiplayerServicesFacade.BeginTracking();
    }
}
```

**Conditional tracking:** Only starts session event tracking if connected via a UGS Session (Relay). For direct IP connections, `CurrentUnitySession` is null, so tracking is skipped.

### `Exit()` — Line 24

Empty. No cleanup needed.

### `OnClientDisconnect(ulong _)` — Line 26

```csharp
public override void OnClientDisconnect(ulong _)
{
    var disconnectReason = m_ConnectionManager.NetworkManager.DisconnectReason;
    if (string.IsNullOrEmpty(disconnectReason) ||
        disconnectReason == "Disconnected due to host shutting down.")
    {
        m_ConnectStatusPublisher.Publish(ConnectStatus.Reconnecting);
        m_ConnectionManager.ChangeState(m_ConnectionManager.m_ClientReconnecting);
    }
    else
    {
        var connectStatus = JsonUtility.FromJson<ConnectStatus>(disconnectReason);
        m_ConnectStatusPublisher.Publish(connectStatus);
        m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
    }
}
```

**Decision logic:**

| Disconnect Reason | Behavior | Target State |
|-------------------|----------|-------------|
| Empty / null | Assume transient → try reconnect | `ClientReconnectingState` |
| `"Disconnected due to host shutting down."` | Netcode default message → try reconnect | `ClientReconnectingState` |
| Any JSON-encoded `ConnectStatus` | Intentional disconnect → go offline | `OfflineState` |

**Critical string:** `"Disconnected due to host shutting down."` is a Netcode-internal string. If Unity changes this message in a future Netcode version, this comparison will break and the client will go offline instead of attempting reconnection.

**JSON deserialization:** `JsonUtility.FromJson<ConnectStatus>(disconnectReason)` — the server sends `JsonUtility.ToJson(ConnectStatus.HostEndedSession)` etc., so this round-trip works because `ConnectStatus` is an enum serialized as JSON.

---

## Data Flow on Disconnect

```
NetworkManager detects disconnect
  → ConnectionManager.OnClientDisconnectCallback(clientId)
    → ClientConnectedState.OnClientDisconnect(clientId)
      ├── [no reason] → Publish(Reconnecting) → ChangeState(ClientReconnecting)
      │                                            → ClientReconnectingState.Enter()
      │                                              → StartCoroutine(ReconnectCoroutine())
      └── [with reason] → Publish(reason) → ChangeState(Offline)
                                              → OfflineState.Enter()
                                                → EndTracking() + Shutdown()
```

---

## Inherited Methods (from OnlineState)

| Method | Behavior |
|--------|----------|
| `OnUserRequestedShutdown()` | Publish `UserRequestedDisconnect` → Offline |
| `OnTransportFailure()` | → Offline (no publish) |
