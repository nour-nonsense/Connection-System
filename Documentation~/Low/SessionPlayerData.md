# `SessionPlayerData.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/ConnectionManagement/SessionPlayerData.cs` |
| **Namespace** | `Unity.ConnectionManagement` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 40 |
| **Type** | `struct` (value type) |
| **Implements** | `ISessionPlayerData` |

---

## Purpose

Holds all per-player data that persists across a session, including reconnections. Stored in `SessionManager<SessionPlayerData>` keyed by player ID.

---

## Memory Layout

```csharp
public struct SessionPlayerData : ISessionPlayerData
{
    public string PlayerName;             // 8 bytes (reference)
    public int PlayerNumber;              // 4 bytes
    public NetworkGuid AvatarNetworkGuid; // 16 bytes (2 × ulong)
    public int CurrentHitPoints;          // 4 bytes
    public bool IsConnected;              // 1 byte (+ padding)
    public ulong ClientID;               // 8 bytes
    public bool HasCharacterSpawned;      // 1 byte (+ padding)
}
// Total: ~48 bytes (with alignment padding)
```

**Value type:** As a `struct`, copies are made when read from `SessionManager`. Changes to a local copy do NOT affect the stored data. You must call `SetPlayerData()` to persist changes.

---

## Constructor

```csharp
public SessionPlayerData(ulong clientId, string name, NetworkGuid guid, 
                          int currentHitPoints = 0, bool isConnected = false, 
                          bool hasCharacterSpawned = false)
```

**Defaults:**
- `currentHitPoints` = 0
- `isConnected` = false
- `hasCharacterSpawned` = false

---

## ISessionPlayerData Implementation

```csharp
bool ISessionPlayerData.IsConnected
{
    get => IsConnected;
    set => IsConnected = value;
}

ulong ISessionPlayerData.ClientID
{
    get => ClientID;
    set => ClientID = value;
}

void ISessionPlayerData.Reinitialize()
{
    HasCharacterSpawned = false;
    // Note: PlayerName, PlayerNumber, AvatarNetworkGuid, CurrentHitPoints 
    // are NOT reset. They persist across game sessions on the same server.
}
```

**`Reinitialize()`** is called by `SessionManager.OnSessionEnded()` → `ReinitializePlayersData()`. It only resets `HasCharacterSpawned`, preserving identity data for the next game round.

---

## Usage Points

| Location | Usage |
|----------|-------|
| `StartingHostState.ApprovalCheck()` | Creates initial data for host |
| `HostingState.ApprovalCheck()` | Creates initial data for connecting clients |
| `HostingState.OnClientConnected()` | Reads `PlayerName` for event |
| `HostingState.OnClientDisconnect()` | Reads `PlayerName` for event |
| `SessionManager.DisconnectClient()` | Sets `IsConnected = false` |
| `SessionManager.SetupConnectingPlayerSessionData()` | Stores data, handles reconnection |

---

## Reconnection Behavior

When a player reconnects, `SessionManager.SetupConnectingPlayerSessionData()` detects the existing (disconnected) data and:
1. Restores the old `SessionPlayerData` (preserving position, HP, avatar, etc.)
2. Updates `ClientID` to the new Netcode client ID
3. Sets `IsConnected = true`

This means the player resumes with their previous game state instead of starting fresh.
