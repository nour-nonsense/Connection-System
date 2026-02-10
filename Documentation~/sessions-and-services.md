# Session Management & Unity Services

[← Back to Index](index.md) · [Architecture](architecture.md)

---

## Overview

This layer abstracts **Unity Gaming Services (UGS)** — specifically the Multiplayer Sessions SDK. It provides session creation, joining, querying, and lifecycle management through a clean facade pattern.

---

## Component Map

```
MultiplayerServicesFacade (high-level API)
    ├── MultiplayerServicesInterface (raw SDK wrapper)
    ├── LocalSession (local state mirror)
    ├── LocalSessionUser (per-user state)
    ├── RateLimitCooldown (API throttling)
    └── Messages
        ├── UnityServiceErrorMessage
        └── SessionListFetchedMessage
```

---

## `MultiplayerServicesFacade`

**Namespace:** `Unity.ConnectionManagement.UnityServices.Sessions`  
**Implements:** `IDisposable`, `IStartable` (VContainer lifecycle)

The main entry point for all session operations. It wraps `MultiplayerServicesInterface` and adds:
- **Rate limiting** (prevents API abuse)
- **Session event tracking** (player join/leave, state changes)
- **Error handling** (publishes errors via PubSub)
- **Session lifecycle** (create, join, leave, delete, reconnect)

### Injected Dependencies

| Dependency | Purpose |
|-----------|---------|
| `LifetimeScope m_ParentScope` | Creates child scope for service interface |
| `UpdateRunner m_UpdateRunner` | Periodic update loop for polling |
| `LocalSession m_LocalSession` | Local mirror of remote session data |
| `LocalSessionUser m_LocalUser` | Current player's session data |
| `IPublisher<UnityServiceErrorMessage>` | Error broadcasting |
| `IPublisher<SessionListFetchedMessage>` | Session list broadcasting |

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `CurrentUnitySession` | `ISession` | The currently joined session (null if none) |

### Public Methods

#### Session Creation & Joining

```csharp
// Create a new session with Relay
Task<(bool Success, ISession Session)> TryCreateSessionAsync(
    string sessionName, int maxPlayers, bool isPrivate)

// Join by session code
Task<(bool Success, ISession Session)> TryJoinSessionByCodeAsync(string sessionCode)

// Join by session name/ID
Task<(bool Success, ISession Session)> TryJoinSessionByNameAsync(string sessionName)

// Quick join (finds first available or creates new)
Task<(bool Success, ISession Session)> TryQuickJoinSessionAsync()

// Reconnect to a previously joined session
Task<ISession> ReconnectToSessionAsync()
```

#### Session Lifecycle

```csharp
// Start tracking session events (call after joining)
void BeginTracking()

// Stop tracking and leave/delete session
void EndTracking()

// Set the current session (after joining)
void SetRemoteSession(ISession session)

// Remove a player (host only)
async void RemovePlayerFromSessionAsync(string uasId)
```

#### Session Querying

```csharp
// Fetch and publish list of available sessions
Task RetrieveAndPublishSessionListAsync()
```

### Session Events Tracked

When `BeginTracking()` is called, the facade subscribes to:

| Event | Handler |
|-------|---------|
| `Changed` | Syncs local data, checks if host left |
| `StateChanged` | Logs state transitions |
| `Deleted` | Resets session, ends tracking |
| `PlayerJoined` | Logs join |
| `PlayerHasLeft` | Logs leave |
| `RemovedFromSession` | Resets session, ends tracking |
| `PlayerPropertiesChanged` | Logs change |
| `SessionPropertiesChanged` | Logs change |

### Rate Limiting

All API calls are rate-limited to comply with UGS quotas:

| Operation | Cooldown |
|-----------|----------|
| Query sessions | 1 second |
| Join session | 1 second |
| Quick join | 1 second |
| Create/Host | 3 seconds |

---

## `MultiplayerServicesInterface`

**Purpose:** Thin wrapper around `MultiplayerService.Instance` API calls.

| Method | UGS API Called |
|--------|---------------|
| `CreateSession()` | `CreateSessionAsync()` with `.WithRelayNetwork()` |
| `JoinSessionByCode()` | `JoinSessionByCodeAsync()` |
| `JoinSessionById()` | `JoinSessionByIdAsync()` |
| `QuickJoinSession()` | `MatchmakeSessionAsync()` |
| `QuerySessions()` | `QuerySessionsAsync()` |
| `QueryAllSessions()` | `QuerySessionsAsync()` with filters & sorting |
| `ReconnectToSession()` | `ReconnectToSessionAsync()` |

**Defaults:**
- Max players: 8
- Max sessions shown: 16
- Filter: only sessions with available slots
- Sort: newest first

---

## `SessionManager<T>`

**Namespace:** `Unity.ConnectionManagement.Sessions`  
**Pattern:** Singleton (`SessionManager<T>.Instance`)

Manages per-player session data on the **server side**. Maps player IDs to `ISessionPlayerData` structs.

### Key Concepts

- **`playerId`** — Persistent unique identifier (survives reconnects)
- **`clientId`** — Netcode-assigned ID (changes on reconnect)
- Associates `clientId → playerId → T` for data lookup

### Public Methods

| Method | Purpose |
|--------|---------|
| `SetupConnectingPlayerSessionData(clientId, playerId, data)` | Register new or reconnecting player |
| `GetPlayerData(clientId)` / `GetPlayerData(playerId)` | Retrieve player data |
| `SetPlayerData(clientId, data)` | Update player data |
| `GetPlayerId(clientId)` | Look up player ID from client ID |
| `DisconnectClient(clientId)` | Mark player as disconnected (keep data for reconnection) |
| `IsDuplicateConnection(playerId)` | Check if player is already connected |
| `OnSessionStarted()` | Mark session as started (preserves data of disconnected players) |
| `OnSessionEnded()` | Clear disconnected players, reinitialize remaining |
| `OnServerEnded()` | Clear all data |

### Reconnection Behavior

| Session State | Player Disconnects | Outcome |
|---------------|-------------------|---------|
| Not started | Player leaves | Data removed immediately |
| Started | Player disconnects | Data **preserved** for reconnection |
| Started | Player reconnects | Old data restored with new `clientId` |

---

## `LocalSession`

Local mirror of a UGS Session. Tracks:
- `SessionID`, `SessionCode`, `RelayJoinCode`, `SessionName`, `Private`, `MaxPlayerCount`
- Dictionary of `LocalSessionUser` objects

Fires `changed` event whenever any data changes (used for UI updates).

---

## `LocalSessionUser`

Local representation of a player in a session. Tracks:
- `IsHost` — whether this player is the session host
- `DisplayName` — player display name
- `ID` — unique player identifier

Fires `changed` event when any property changes, with `UserMembers` flags indicating which fields changed.

---

## Messages

### `UnityServiceErrorMessage`

Published when a UGS API call fails.

```csharp
public struct UnityServiceErrorMessage
{
    public string Title;
    public string Message;
    public Service AffectedService;    // Authentication or Session
    public Exception OriginalException;
}
```

### `SessionListFetchedMessage`

Published when session list query completes.

```csharp
public struct SessionListFetchedMessage
{
    public readonly IList<ISessionInfo> LocalSessions;
}
```

---

## `RateLimitCooldown`

Simple time-based throttle. Each operation has its own cooldown instance.

```csharp
var cooldown = new RateLimitCooldown(3f); // 3 second cooldown

if (cooldown.CanCall)
{
    // Make API call
    cooldown.PutOnCooldown();
}
```
