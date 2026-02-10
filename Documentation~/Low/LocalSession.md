# `LocalSession.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/UnityServices/Sessions/LocalSession.cs` |
| **Namespace** | `Unity.ConnectionManagement.UnityServices.Sessions` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 223 |
| **Access** | `public class` |

---

## Purpose

Client-side mirror of a remote UGS session. Maintains a local copy of session metadata and player list. Fires events when data changes so game UI can update without polling the remote API.

---

## Properties

| Property | Type | Access | Backing | Description |
|----------|------|--------|---------|-------------|
| `SessionID` | `string` | `get; private set` | Field | UGS session identifier |
| `SessionCode` | `string` | `get; private set` | Field | Human-readable join code |
| `RelayJoinCode` | `string` | `get; private set` | Field | Relay-specific join code |
| `MaxPlayerCount` | `int` | `get; private set` | Field | Session capacity |
| `IsPrivate` | `bool` | `get; private set` | Field | Whether session is publicly queryable |
| `IsLocked` | `bool` | `get; private set` | Field | Whether session accepts new players |
| `AvailableSlots` | `int` | computed | `MaxPlayerCount - sessionUsers.Count` | Remaining capacity |
| `sessionUsers` | `Dictionary<string, LocalSessionUser>` | `public` | Field | Player ID → local user mapping |

---

## Events

| Event | Signature | Fired When |
|-------|-----------|-----------|
| `changed` | `event Action<LocalSession>` | Any property changes. Fires at end of `CopyDataFrom()` and `ApplyRemoteData()`. |

---

## Key Methods

### `CopyDataFrom(LocalSession session)` — Line 61

Copies all properties from another `LocalSession` instance. Used when receiving session data from another source. Fires `changed` event.

### `ApplyRemoteData(ISession session)` — Line 72

```csharp
public void ApplyRemoteData(ISession session)
{
    SessionID = session.Id;
    SessionCode = session.Code;
    RelayJoinCode = session.RelayServer?.JoinCode;
    MaxPlayerCount = session.MaxPlayers;
    IsPrivate = session.IsPrivate;
    IsLocked = session.IsLocked;

    // Sync player list
    var remotePlayerIds = new HashSet<string>();
    foreach (var player in session.Players)
    {
        var id = player.Id;
        remotePlayerIds.Add(id);
        
        if (sessionUsers.ContainsKey(id))
        {
            sessionUsers[id].CopyDataFrom(player);
        }
        else
        {
            var newUser = new LocalSessionUser(/*...*/);
            newUser.CopyDataFrom(player);
            sessionUsers.Add(id, newUser);
        }
    }

    // Remove players not in remote session
    var toRemove = sessionUsers.Keys
        .Where(k => !remotePlayerIds.Contains(k))
        .ToList();
    foreach (var id in toRemove)
    {
        sessionUsers.Remove(id);
    }

    OnChanged();
}
```

**Three-phase sync:**
1. **Update existing** — For players already in the local list, copy new data
2. **Add new** — For players in remote but not local, create `LocalSessionUser`
3. **Remove stale** — For players in local but not remote, remove them

**LINQ allocation:** `Where().ToList()` allocates a `List<string>` each sync. Called on every `OnSessionChanged` event from UGS. For sessions with many players, this could be optimized with a reusable buffer.

### `Reset()` — Line 120

Clears all data and the player dictionary. Called when ending tracking or on session delete.

---

## Thread Safety

Not thread-safe. All access is from the main thread via `MultiplayerServicesFacade` event handlers.
