# `ConnectionState.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/ConnectionManagement/ConnectionState/ConnectionState.cs` |
| **Namespace** | `Unity.ConnectionManagement` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 46 |
| **Base class** | `object` (plain C# class, not MonoBehaviour) |
| **Access** | `abstract class` (internal to assembly) |

---

## Purpose

Abstract base class defining the contract for all connection states. Provides default no-op implementations for all event handlers so concrete states only override what they need.

---

## Injected Fields

| Field | Type | Attribute | Access | Description |
|-------|------|-----------|--------|-------------|
| `m_ConnectionManager` | `ConnectionManager` | `[Inject]` | `protected` | Back-reference to the owning state machine. Used by all states to access `NetworkManager`, trigger `ChangeState()`, and read configuration. |
| `m_ConnectStatusPublisher` | `IPublisher<ConnectStatus>` | `[Inject]` | `protected` | Publishes connection status changes. Subscribed to by game UI to show status messages. |

**Injection timing:** These fields are set by `ConnectionManager.Start()` via `IObjectResolver.Inject(this)`. Until `Start()` runs, they are `null`.

---

## Virtual Method Table

All methods are `virtual` with empty/no-op default implementations:

```csharp
public abstract void Enter();
public abstract void Exit();
public virtual void OnClientConnected(ulong clientId) { }
public virtual void OnClientDisconnect(ulong clientId) { }
public virtual void OnServerStarted() { }
public virtual void OnServerStopped() { }
public virtual void OnTransportFailure() { }
public virtual void OnUserRequestedShutdown() { }
public virtual void StartClientIP(string playerName, string ipaddress, ushort port) { }
public virtual void StartClientSession(string playerName) { }
public virtual void StartHostIP(string playerName, string ipaddress, ushort port) { }
public virtual void StartHostSession(string playerName) { }
public virtual void ApprovalCheck(
    NetworkManager.ConnectionApprovalRequest request, 
    NetworkManager.ConnectionApprovalResponse response) { }
```

**Design note:** `Enter()` and `Exit()` are `abstract` (must be implemented), while all event handlers are `virtual` with no-op defaults. This means a state only handles the events it cares about — unhandled events are silently ignored.

---

## Override Summary by Concrete State

| Method | Offline | ClientConnecting | ClientConnected | ClientReconnecting | StartingHost | Hosting |
|--------|---------|-----------------|----------------|-------------------|-------------|---------|
| `Enter()` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `Exit()` | ✅ | ✅ (empty) | ✅ (empty) | ✅ | ✅ (empty) | ✅ |
| `OnClientConnected` | — | ✅ | — | ✅ | — | ✅ |
| `OnClientDisconnect` | — | ✅ | ✅ | ✅ | — | ✅ |
| `OnServerStarted` | — | — | — | — | ✅ | — |
| `OnServerStopped` | — | — | — | — | ✅ | ✅ |
| `OnTransportFailure` | — | ✅* | ✅* | ✅* | ✅* | ✅* |
| `OnUserRequestedShutdown` | — | ✅* | ✅* | ✅* | ✅* | ✅ |
| `StartClientIP` | ✅ | — | — | — | — | — |
| `StartClientSession` | ✅ | — | — | — | — | — |
| `StartHostIP` | ✅ | — | — | — | — | — |
| `StartHostSession` | ✅ | — | — | — | — | — |
| `ApprovalCheck` | — | — | — | — | ✅ | ✅ |

*✅\** = Inherited from `OnlineState` base class

---

## Design Pattern

This implements the **State Pattern** (GoF). Key characteristics:
- States are plain C# objects (not MonoBehaviours) — lightweight, no Unity overhead
- States are allocated once and reused — no GC pressure from state transitions
- State objects hold injected references but minimal mutable state — most runtime data lives in `SessionManager`, `NetworkManager`, or `MultiplayerServicesFacade`
