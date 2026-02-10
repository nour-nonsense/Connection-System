# `MultiplayerServicesInterface.cs` — Technical Reference

[← Back to Low-Level Index](index.md) · [MultiplayerServicesFacade](MultiplayerServicesFacade.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/UnityServices/Sessions/MultiplayerServicesInterface.cs` |
| **Namespace** | `Unity.ConnectionManagement.UnityServices.Sessions` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 107 |
| **Access** | `public class` |

---

## Purpose

Thin wrapper layer around `MultiplayerService.Instance` (Unity's Multiplayer SDK). Isolates the raw SDK calls behind a testable, injectable interface. Provides default filter and sort options.

---

## Constants

| Constant | Type | Value | Line | Description |
|----------|------|-------|------|-------------|
| `k_MaxSessionsToShow` | `int` | `16` | 14 | Max sessions returned by `QueryAllSessions()`. |
| `k_MaxPlayers` | `int` | `8` | 15 | Default max players for `QuickJoinSession()` auto-created sessions. |

---

## Constructor Fields

| Field | Type | Line | Description |
|-------|------|------|-------------|
| `m_FilterOptions` | `List<FilterOption>` | 17 | Filters for session queries. Default: `AvailableSlots > 0` (only open sessions). |
| `m_SortOptions` | `List<SortOption>` | 18 | Sort order for queries. Default: newest first (`CreationTime Descending`). |

---

## Methods — SDK Call Mapping

### `CreateSession(name, maxPlayers, isPrivate, playerProps, sessionProps)` — Line 35

```csharp
var sessionOptions = new SessionOptions
{
    Name = sessionName,
    MaxPlayers = maxPlayers,
    IsPrivate = isPrivate,
    IsLocked = false,
    PlayerProperties = playerProperties,
    SessionProperties = sessionProperties
}.WithRelayNetwork();  // ← Key: enables Relay transport

return await MultiplayerService.Instance.CreateSessionAsync(sessionOptions);
```

**`.WithRelayNetwork()`** — Configures the session to use Unity Relay for networking. This allocates a Relay server and configures the transport automatically.

**`IsLocked = false`** — Sessions are never locked. Players can always join (until full).

### `JoinSessionByCode(code, userData)` — Line 50

```csharp
var joinSessionOptions = new JoinSessionOptions
{
    PlayerProperties = localUserData
};
return await MultiplayerService.Instance.JoinSessionByCodeAsync(sessionCode, joinSessionOptions);
```

### `JoinSessionById(id, userData)` — Line 59

Same pattern as `JoinSessionByCode` but uses `JoinSessionByIdAsync()`.

### `QuickJoinSession(userData)` — Line 68

```csharp
var quickJoinOptions = new QuickJoinOptions
{
    Filters = m_FilterOptions,    // AvailableSlots > 0
    CreateSession = true          // ← auto-create if no match found
};

var sessionOptions = new SessionOptions
{
    MaxPlayers = k_MaxPlayers,    // 8
    PlayerProperties = localUserData
}.WithRelayNetwork();

return await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoinOptions, sessionOptions);
```

**`CreateSession = true`** — If no matching session is found, automatically creates one using `sessionOptions`. This means `QuickJoin` always succeeds (unless rate-limited or service is down).

### `QuerySessions()` — Line 85

```csharp
return await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions());
```

**No filters or sorting** — Returns raw results. Used for general queries.

### `ReconnectToSession(sessionId)` — Line 90

```csharp
return await MultiplayerService.Instance.ReconnectToSessionAsync(sessionId);
```

### `QueryAllSessions()` — Line 95

```csharp
var querySessionOptions = new QuerySessionsOptions
{
    Count = k_MaxSessionsToShow,      // 16
    FilterOptions = m_FilterOptions,  // AvailableSlots > 0
    SortOptions = m_SortOptions       // Newest first
};
return await MultiplayerService.Instance.QuerySessionsAsync(querySessionOptions);
```

**This method is not called anywhere in the package.** It's provided for consumer code to list available sessions (e.g., for a server browser UI).

---

## Design Notes

- **No error handling** — All exceptions propagate to `MultiplayerServicesFacade` which handles them via `PublishError()`.
- **No caching** — Every call hits the UGS backend. Rate limiting is handled by the facade layer.
- **Stateless** — No instance state beyond the filter/sort options set in the constructor.
