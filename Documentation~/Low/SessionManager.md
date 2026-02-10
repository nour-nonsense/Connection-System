# `SessionManager.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/UnityServices/Sessions/SessionManager.cs` |
| **Namespace** | `Unity.ConnectionManagement.Sessions` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 278 |
| **Type** | `public class SessionManager<T> where T : struct, ISessionPlayerData` |
| **Pattern** | Singleton (private constructor + static `Instance`) |

---

## Purpose

Server-side registry mapping player IDs (persistent, client-generated) to session data and Netcode client IDs. Handles the dual-key lookup (`clientId ↔ playerId`) and manages reconnection by preserving disconnected player data.

---

## Internal Data Structures

### `m_ClientData` — `Dictionary<string, T>`

Maps persistent player ID → player data struct. This is the **primary data store**.

```
Key: "auth-player-id-123" or "guid+profile"
Value: SessionPlayerData { PlayerName, IsConnected, ClientID, ... }
```

### `m_ClientIDToPlayerId` — `Dictionary<ulong, string>`

Maps Netcode client ID → persistent player ID. This is a **reverse index** for fast lookup.

```
Key: 12345 (Netcode-assigned, changes per connection)
Value: "auth-player-id-123"
```

### `m_HasSessionStarted` — `bool`

Controls disconnection behavior:
- `false` → disconnected player data is **removed** (lobby/char-select phase)
- `true` → disconnected player data is **preserved** (gameplay phase, allows reconnection)

---

## Singleton Pattern

```csharp
static SessionManager<T> s_Instance;

public static SessionManager<T> Instance
{
    get
    {
        if (s_Instance == null)
        {
            s_Instance = new SessionManager<T>();
        }
        return s_Instance;
    }
}

SessionManager() // private constructor
{
    m_ClientData = new Dictionary<string, T>();
    m_ClientIDToPlayerId = new Dictionary<ulong, string>();
}
```

**Never destroyed:** The singleton persists for the entire application lifetime. `OnServerEnded()` clears the data but doesn't null the instance.

---

## Method Internals

### `SetupConnectingPlayerSessionData(clientId, playerId, sessionPlayerData)` — Line 110

Most complex method. Handles three scenarios:

```
SetupConnectingPlayerSessionData(clientId, playerId, data)
  │
  ├── [Duplicate connected player with same playerId]
  │     └── LogError + return (reject)
  │
  ├── [Existing disconnected player with same playerId]
  │     └── RECONNECTION:
  │           sessionPlayerData = m_ClientData[playerId]   ← restore old data
  │           sessionPlayerData.ClientID = clientId         ← update ID
  │           sessionPlayerData.IsConnected = true          ← mark connected
  │
  └── [New player]
        └── Use provided sessionPlayerData as-is
  
  Finally:
    m_ClientIDToPlayerId[clientId] = playerId
    m_ClientData[playerId] = sessionPlayerData
```

**Reconnection key insight:** The `sessionPlayerData` parameter is **overwritten** with the old data when reconnecting. This means the caller's initial data is discarded, and the player resumes with their previous state.

### `DisconnectClient(clientId)` — Line 63

Behavior depends on `m_HasSessionStarted`:

| Session State | What Happens |
|---------------|-------------|
| **Not started** | Remove from both dictionaries. Player must re-register on next connect. |
| **Started** | Keep data in `m_ClientData`, just set `IsConnected = false`. The `m_ClientIDToPlayerId` mapping is preserved so `GetPlayerData(clientId)` still works until the player reconnects with a new `clientId`. |

**Struct copy pattern:**
```csharp
var clientData = m_ClientData[playerId]; // copy (struct)
clientData.IsConnected = false;           // modify copy
m_ClientData[playerId] = clientData;      // write back
```
This is necessary because `T` is a struct — dictionary indexer returns a copy, not a reference.

### `GetPlayerData(ulong clientId)` — Line 167

Two-step lookup:
```
clientId → GetPlayerId(clientId) → playerId → GetPlayerData(playerId)
```
Returns `T?` (nullable struct). Returns `null` if either lookup fails.

### `GetPlayerData(string playerId)` — Line 185

Direct dictionary lookup. Returns `T?`.

### `IsDuplicateConnection(string playerId)` — Line 99

```csharp
return m_ClientData.ContainsKey(playerId) && m_ClientData[playerId].IsConnected;
```

Returns `true` only if the player exists AND is currently connected. A disconnected player is NOT a duplicate (they're a candidate for reconnection).

### `OnSessionStarted()` — Line 216

Sets `m_HasSessionStarted = true`. From this point, `DisconnectClient()` preserves data.

### `OnSessionEnded()` — Line 224

```csharp
public void OnSessionEnded()
{
    ClearDisconnectedPlayersData();  // remove all disconnected entries
    ReinitializePlayersData();       // call Reinitialize() on remaining
    m_HasSessionStarted = false;     // back to "not started" mode
}
```

### `OnServerEnded()` — Line 234

Nuclear option: clears both dictionaries and resets `m_HasSessionStarted`.

### `ClearDisconnectedPlayersData()` — Line 252

```csharp
void ClearDisconnectedPlayersData()
{
    List<ulong> idsToClear = new List<ulong>();
    foreach (var id in m_ClientIDToPlayerId.Keys)
    {
        var data = GetPlayerData(id);
        if (data is { IsConnected: false })
        {
            idsToClear.Add(id);
        }
    }

    foreach (var id in idsToClear)
    {
        string playerId = m_ClientIDToPlayerId[id];
        // ... remove from both dictionaries
    }
}
```

**Two-pass pattern:** Collects IDs first, then removes. This avoids `InvalidOperationException` from modifying a dictionary during enumeration.

### `ReinitializePlayersData()` — Line 241

Calls `T.Reinitialize()` on each connected player's data. For `SessionPlayerData`, this resets `HasCharacterSpawned = false`.

---

## Thread Safety

**Not thread-safe.** The singleton is accessed only from the main thread (via `ConnectionState` event handlers). No locks or concurrent collections used.

---

## Potential Issues

1. **Static singleton lifetime** — Data survives scene loads. If the server ends without calling `OnServerEnded()`, stale data persists.
2. **ClientID key collision** — If Netcode reuses a `clientId` for a different player (unlikely but possible after many connect/disconnect cycles), the `m_ClientIDToPlayerId` mapping may become incorrect.
3. **No player data eviction** — Disconnected player data is never removed during an active session (`m_HasSessionStarted == true`). A server running for a very long time with many transient players could accumulate unbounded data.
