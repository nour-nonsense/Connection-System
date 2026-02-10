# `MultiplayerServicesFacade.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/UnityServices/Sessions/MultiplayerServicesFacade.cs` |
| **Namespace** | `Unity.ConnectionManagement.UnityServices.Sessions` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 466 |
| **Implements** | `IDisposable`, `IStartable` (VContainer lifecycle interface) |
| **Access** | `public class` |

---

## Purpose

High-level abstraction over the Unity Multiplayer Sessions SDK. Provides rate-limited session operations (create, join, query, reconnect, leave, delete) and manages session event subscriptions.

---

## Injected Fields

| Field | Type | Attribute | Line | Description |
|-------|------|-----------|------|-------------|
| `m_ParentScope` | `LifetimeScope` | `[Inject]` | 17 | VContainer scope. Used to create child scope for `MultiplayerServicesInterface`. |
| `m_UpdateRunner` | `UpdateRunner` | `[Inject]` | 19 | Not directly used in current implementation but injected for potential future use. |
| `m_LocalSession` | `LocalSession` | `[Inject]` | 21 | Local mirror of remote session data. Updated via `ApplyRemoteData()`. |
| `m_LocalUser` | `LocalSessionUser` | `[Inject]` | 23 | Current player's session data. Used for host checks and service calls. |
| `m_UnityServiceErrorMessagePub` | `IPublisher<UnityServiceErrorMessage>` | `[Inject]` | 25 | Error broadcast channel. |
| `m_SessionListFetchedPub` | `IPublisher<SessionListFetchedMessage>` | `[Inject]` | 27 | Session list broadcast channel. |

---

## Private Fields

| Field | Type | Line | Description |
|-------|------|------|-------------|
| `m_ServiceScope` | `LifetimeScope` | 30 | Child VContainer scope owning `MultiplayerServicesInterface`. Disposed on `Dispose()`. |
| `m_MultiplayerServicesInterface` | `MultiplayerServicesInterface` | 31 | Thin wrapper around `MultiplayerService.Instance`. |
| `m_RateLimitQuery` | `RateLimitCooldown` | 33 | 1s cooldown for session queries. |
| `m_RateLimitJoin` | `RateLimitCooldown` | 34 | 1s cooldown for join operations. |
| `m_RateLimitQuickJoin` | `RateLimitCooldown` | 35 | 1s cooldown for quick-join. |
| `m_RateLimitHost` | `RateLimitCooldown` | 36 | 3s cooldown for session creation. |
| `m_IsTracking` | `bool` | 40 | Guards against double-subscribe/unsubscribe. |

### Properties

| Property | Type | Access | Line |
|----------|------|--------|------|
| `CurrentUnitySession` | `ISession` | `public get; private set` | 38 |

---

## Lifecycle

### `Start()` — Line 42 (IStartable)

Called automatically by VContainer after all injections are complete:

```csharp
public void Start()
{
    m_ServiceScope = m_ParentScope.CreateChild(builder =>
    {
        builder.Register<MultiplayerServicesInterface>(Lifetime.Singleton);
    });

    m_MultiplayerServicesInterface = m_ServiceScope.Container
        .Resolve<MultiplayerServicesInterface>();

    m_RateLimitQuery = new RateLimitCooldown(1f);
    m_RateLimitJoin = new RateLimitCooldown(1f);
    m_RateLimitQuickJoin = new RateLimitCooldown(1f);
    m_RateLimitHost = new RateLimitCooldown(3f);
}
```

**Child scope:** Creates a separate VContainer scope for `MultiplayerServicesInterface`. This isolates the SDK wrapper's lifetime from the parent scope.

### `Dispose()` — Line 58

```csharp
public void Dispose()
{
    EndTracking();
    m_ServiceScope?.Dispose();
}
```

---

## Session Operations

### `TryCreateSessionAsync(name, maxPlayers, isPrivate)` — Line 113

```
TryCreateSessionAsync()
  ├── Check m_RateLimitHost.CanCall → if false, return (false, null)
  ├── m_MultiplayerServicesInterface.CreateSession(...)
  │     → MultiplayerService.Instance.CreateSessionAsync(options.WithRelayNetwork())
  ├── return (true, session)
  └── catch → PublishError(e) → return (false, null)
```

### `TryJoinSessionByCodeAsync(code)` — Line 141

Rate-limited (1s). Validates code is not empty. Calls `JoinSessionByCode()`.

### `TryJoinSessionByNameAsync(name)` — Line 173

Rate-limited (1s). Validates name is not empty. Calls `JoinSessionById()`.

### `TryQuickJoinSessionAsync()` — Line 205

Rate-limited (1s). Calls `QuickJoinSession()` which internally uses `MatchmakeSessionAsync()` with `CreateSession = true` fallback.

### `ReconnectToSessionAsync()` — Line 357

```csharp
public async Task<ISession> ReconnectToSessionAsync()
{
    try
    {
        return await m_MultiplayerServicesInterface
            .ReconnectToSession(m_LocalSession.SessionID);
    }
    catch (Exception e)
    {
        PublishError(e, checkIfDeleted: true);
    }
    return null;
}
```

`checkIfDeleted: true` suppresses the error if the session was already deleted (common in reconnection scenarios).

---

## Session Event Tracking

### `BeginTracking()` / `EndTracking()`

**Guard:** `m_IsTracking` flag prevents double-subscribe.

### Subscribed Events (via `SubscribeToJoinedSession`)

| Event | Handler | Behavior |
|-------|---------|----------|
| `Changed` | `OnSessionChanged()` | Syncs local data. If client, checks if host left → error + end tracking |
| `StateChanged` | `OnSessionStateChanged()` | Logs state (Connected / Disconnected / Deleted) |
| `Deleted` | `OnSessionDeleted()` | Resets session + ends tracking |
| `PlayerJoined` | `OnPlayerJoined()` | Logs player ID |
| `PlayerHasLeft` | `OnPlayerHasLeft()` | Logs player ID |
| `RemovedFromSession` | `OnRemovedFromSession()` | Resets session + ends tracking |
| `PlayerPropertiesChanged` | `OnPlayerPropertiesChanged()` | Logs |
| `SessionPropertiesChanged` | `OnSessionPropertiesChanged()` | Logs |

### `OnSessionChanged()` — Line 259

```
OnSessionChanged()
  ├── m_LocalSession.ApplyRemoteData(CurrentUnitySession)  ← sync
  │
  └── if (!m_LocalUser.IsHost)
        ├── for each sessionUser: check if any IsHost
        │     └── if host found → return (all ok)
        └── if no host found
              → PublishError("Host left the session")
              → EndTracking()
```

**Host-left detection:** The client checks all session users for one with `IsHost = true`. If none exists, the host has left (even if the session still exists), and the client should prepare for disconnection.

### `EndTracking()` — Line 89

```
EndTracking()
  ├── m_IsTracking = false
  ├── UnsubscribeFromJoinedSession()
  └── if (CurrentUnitySession != null)
        ├── if (m_LocalUser.IsHost)
        │     └── DeleteSessionAsync()
        └── else
              └── LeaveSessionAsync()
```

**Host vs client cleanup:** Hosts delete the session (removing it for all players). Clients just leave.

---

## Error Handling

### `PublishError(Exception, bool checkIfDeleted)` — Line 432

```
PublishError(e, checkIfDeleted)
  │
  ├── if NOT AggregateException → publish generic error
  │
  ├── if AggregateException.InnerException is NOT SessionException → publish generic
  │
  ├── if (checkIfDeleted && SessionNotFound && !IsHost) → return silently
  │     └── Session was already deleted, expected during reconnection
  │
  ├── if (SessionError.RateLimitExceeded) → PutOnCooldown(m_RateLimitJoin)
  │     └── SDK-level rate limit hit, enforce additional cooldown
  │
  └── else → publish error with reason: "e.Message (e.InnerException.Message)"
```

**SessionException wrapping:** Unity SDK wraps errors in `AggregateException` → `SessionException`. The method unwraps these layers to get the actual error type.

---

## Thread Safety

Methods that start with `Try*` are `async Task` — they start on the main thread, `await` SDK calls (which may run on a thread pool), and resume on the main thread (Unity's `SynchronizationContext`).

`LeaveSessionAsync()` and `DeleteSessionAsync()` are `async void` — fire-and-forget. Exceptions are caught internally and published via PubSub.
