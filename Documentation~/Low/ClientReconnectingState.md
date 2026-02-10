# `ClientReconnectingState.cs` — Technical Reference

[← Back to Low-Level Index](index.md) · [ClientConnectingState](ClientConnectingState.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/ConnectionManagement/ConnectionState/ClientReconnectingState.cs` |
| **Namespace** | `Unity.ConnectionManagement` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 141 |
| **Base class** | `ClientConnectingState` (inherits `ConnectClientAsync()`) |
| **Access** | `internal class` |

---

## Purpose

Automatic retry logic for disconnected clients. Manages a coroutine-based retry loop with configurable delays and maximum attempts. Inherits from `ClientConnectingState` to reuse the `ConnectClientAsync()` method.

---

## Injected Fields

| Field | Type | Attribute | Line | Description |
|-------|------|-----------|------|-------------|
| `m_ReconnectMessagePublisher` | `IPublisher<ReconnectMessage>` | `[Inject]` | 19 | Publishes reconnection progress for UI display (e.g., "Attempt 2/3"). |

---

## Instance Fields

| Field | Type | Access | Initial | Line | Description |
|-------|------|--------|---------|------|-------------|
| `m_ReconnectCoroutine` | `Coroutine` | `private` | `null` | 21 | Handle to the active coroutine. Used for cancellation. |
| `m_NbAttempts` | `int` | `private` | `0` | 22 | Current attempt counter. Compared against `ConnectionManager.NbReconnectAttempts`. |

---

## Constants

| Constant | Type | Value | Line | Description |
|----------|------|-------|------|-------------|
| `k_TimeBeforeFirstAttempt` | `float` | `1` | 24 | Seconds to wait before the very first reconnection attempt. Gives UGS time to update session state. |
| `k_TimeBetweenAttempts` | `float` | `5` | 25 | Seconds to wait between subsequent attempts. Fixed cooldown (not exponential backoff — see code comment referencing Wikipedia). |

---

## Methods

### `Enter()` — Line 27

```csharp
public override void Enter()
{
    m_NbAttempts = 0;
    m_ReconnectCoroutine = m_ConnectionManager.StartCoroutine(ReconnectCoroutine());
}
```

Resets counter and starts the first reconnect iteration. Uses `ConnectionManager` (MonoBehaviour) as the coroutine host since states are plain C# objects.

### `Exit()` — Line 33

```csharp
public override void Exit()
{
    if (m_ReconnectCoroutine != null)
    {
        m_ConnectionManager.StopCoroutine(m_ReconnectCoroutine);
        m_ReconnectCoroutine = null;
    }
    m_ReconnectMessagePublisher.Publish(
        new ReconnectMessage(m_ConnectionManager.NbReconnectAttempts, 
                             m_ConnectionManager.NbReconnectAttempts));
}
```

**Cancellation:** Stops any in-progress coroutine to prevent callbacks after state transition.

**Final message:** Publishes a `ReconnectMessage` with `CurrentAttempt == MaxAttempt` to signal "done reconnecting" to the UI.

### `OnClientConnected(ulong _)` — Line 43

```csharp
public override void OnClientConnected(ulong _)
{
    m_ConnectionManager.ChangeState(m_ConnectionManager.m_ClientConnected);
}
```

**Difference from parent:** Does NOT publish `ConnectStatus.Success`. The reconnection success is implicit — the UI already knows we were reconnecting.

### `OnClientDisconnect(ulong _)` — Line 48

The most complex method. Handles the branching logic after each failed attempt:

```
OnClientDisconnect()
  ├── [attempts < max]
  │     ├── [no reason] → start another ReconnectCoroutine()
  │     └── [with reason]
  │           ├── Publish(reason)
  │           ├── if fatal (UserRequestedDisconnect, HostEndedSession,
  │           │            ServerFull, IncompatibleBuildType)
  │           │     → ChangeState(Offline)
  │           └── else → start another ReconnectCoroutine()
  │
  └── [attempts >= max]
        ├── [no reason] → Publish(GenericDisconnect) → Offline
        └── [with reason] → Publish(reason) → Offline
```

**Fatal reasons** that abort reconnection immediately:
1. `UserRequestedDisconnect` — User chose to leave
2. `HostEndedSession` — Host shut down intentionally
3. `ServerFull` — No point retrying, server is full
4. `IncompatibleBuildType` — Build mismatch, retrying won't help

### `ReconnectCoroutine()` — Line 91

The core retry loop implemented as a Unity Coroutine:

```
ReconnectCoroutine()
  ├── if (m_NbAttempts > 0)
  │     └── yield WaitForSeconds(5)        ← k_TimeBetweenAttempts
  │
  ├── NetworkManager.Shutdown()
  ├── yield WaitWhile(ShutdownInProgress)  ← wait for clean shutdown
  │
  ├── Publish(ReconnectMessage)            ← "Attempt X/Y"
  │
  ├── if (m_NbAttempts == 0)
  │     └── yield WaitForSeconds(1)        ← k_TimeBeforeFirstAttempt
  │
  ├── m_NbAttempts++
  ├── var task = m_ConnectionMethod.SetupClientReconnectionAsync()
  ├── yield WaitUntil(task.IsCompleted)    ← bridge async to coroutine
  │
  ├── if (task succeeded)
  │     └── ConnectClientAsync()           ← inherited from ClientConnectingState
  │           └── Netcode StartClient()
  │                 ├── OnClientConnected → ClientConnected (success!)
  │                 └── OnClientDisconnect → back to top of this flow
  │
  └── if (task failed)
        ├── if (!shouldTryAgain)
        │     └── m_NbAttempts = max       ← force give up
        └── OnClientDisconnect(0)          ← trigger retry or give up logic
```

**Async-to-Coroutine bridge:** `yield return new WaitUntil(() => task.IsCompleted)` — converts the `Task` returned by `SetupClientReconnectionAsync()` into a coroutine-compatible wait. This runs on the main thread poll loop.

**Shutdown before retry:** The `NetworkManager.Shutdown()` call is necessary because `StartClient()` cannot be called while the NetworkManager is in a connected or connecting state.

---

## Timing Diagram

```
Time (seconds): 0   1       6       11      16
                │   │       │       │       │
                │   ├─ Try1 │       │       │
                │   │       ├─ Try2 │       │
                │   │       │       ├─ Try3 │
                │   │       │       │       └─ Give up → Offline
                │   │       │       │
          Enter │   1s      5s      5s
                │   wait    wait    wait
```

With `NbReconnectAttempts = 3`:
- t=0: Enter (disconnected)
- t=1: First attempt (1s initial delay)
- t=6: Second attempt (5s between)
- t=11: Third attempt (5s between)
- t=16: If third fails, go offline

---

## Memory / GC Notes

- `ReconnectCoroutine` enumerator is allocated on the heap — one allocation per coroutine start
- `WaitForSeconds` objects are allocated per yield — typically 2-3 per attempt
- These are acceptable since reconnections are infrequent events
