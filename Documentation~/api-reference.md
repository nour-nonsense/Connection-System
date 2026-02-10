# API Reference

[← Back to Index](index.md)

---

## Namespaces

| Namespace | Layer |
|-----------|-------|
| `Unity.ConnectionManagement` | Core — state machine, connection methods, data |
| `Unity.ConnectionManagement.Infrastructure` | PubSub, UpdateRunner, NetworkGuid |
| `Unity.ConnectionManagement.Sessions` | SessionManager, ISessionPlayerData |
| `Unity.ConnectionManagement.UnityServices` | Error messages, RateLimitCooldown |
| `Unity.ConnectionManagement.UnityServices.Sessions` | Multiplayer facade, local session, session messages |
| `Unity.ConnectionManagement.Utils` | ClientPrefs, ProfileManager, SceneLoaderWrapper, NetworkNameState |

---

## Core (`Unity.ConnectionManagement`)

### `ConnectionManager` : MonoBehaviour

| Member | Type | Access | Description |
|--------|------|--------|-------------|
| `NetworkManager` | `NetworkManager` | `public` | Reference to Netcode NetworkManager |
| `MaxConnectedPlayers` | `int` | `public` | Max allowed concurrent players |
| `NbReconnectAttempts` | `int` | `public` | Number of auto-reconnection attempts (default: 2) |
| `m_Offline` | `OfflineState` | `internal` | Singleton state instance |
| `m_ClientConnecting` | `ClientConnectingState` | `internal` | Singleton state instance |
| `m_ClientConnected` | `ClientConnectedState` | `internal` | Singleton state instance |
| `m_ClientReconnecting` | `ClientReconnectingState` | `internal` | Singleton state instance |
| `m_StartingHost` | `StartingHostState` | `internal` | Singleton state instance |
| `m_StartingServer` | `StartingServerState` | `internal` | Singleton state instance |
| `m_Hosting` | `HostingState` | `internal` | Singleton state instance |
| `ChangeState(ConnectionState)` | `void` | `internal` | Transition to a new state |
| `StartClientIp(name, ip, port)` | `void` | `public` | Begin IP client connection |
| `StartClientSession(name)` | `void` | `public` | Begin Session/Relay client connection |
| `StartHostIp(name, ip, port)` | `void` | `public` | Begin IP host |
| `StartHostSession(name)` | `void` | `public` | Begin Session/Relay host |
| `StartServerIp(ip, port)` | `void` | `public` | Begin Dedicated Server (Headless) |
| `RequestShutdown()` | `void` | `public` | Request graceful shutdown |

### `ConnectionState` (abstract)

| Virtual Method | Signature |
|---------------|-----------|
| `Enter()` | `void` |
| `Exit()` | `void` |
| `OnClientConnected(ulong clientId)` | `void` |
| `OnClientDisconnect(ulong clientId)` | `void` |
| `OnServerStarted()` | `void` |
| `OnServerStopped()` | `void` |
| `OnTransportFailure()` | `void` |
| `OnUserRequestedShutdown()` | `void` |
| `StartClientIP(name, ip, port)` | `void` |
| `StartClientSession(name)` | `void` |
| `StartHostIP(name, ip, port)` | `void` |
| `StartHostSession(name)` | `void` |
| `StartServerIP(ip, port)` | `void` |
| `ApprovalCheck(request, response)` | `void` |

### `ConnectionMethodBase` (abstract)

| Method | Signature | Description |
|--------|-----------|-------------|
| `SetupHostConnection()` | `void` | Configure transport for hosting |
| `SetupClientConnection()` | `void` | Configure transport for client |
| `SetupClientReconnectionAsync()` | `Task<(bool, bool)>` | Returns (success, shouldTryAgain) |
| `SetConnectionPayload(id, name)` | `void` | Serialize and set connection data |
| `GetPlayerId()` | `string` | Get persistent player identifier |

### `ConnectionMethodIP` : ConnectionMethodBase

| Constructor Parameter | Type | Description |
|----------------------|------|-------------|
| `ip` | `string` | Host IP address |
| `port` | `ushort` | Host port |
| `connectionManager` | `ConnectionManager` | Manager reference |
| `profileManager` | `ProfileManager` | Profile reference |
| `playerName` | `string` | Player display name |

### `ConnectionMethodRelay` : ConnectionMethodBase

| Constructor Parameter | Type | Description |
|----------------------|------|-------------|
| `multiplayerServicesFacade` | `MultiplayerServicesFacade` | Services facade |
| `connectionManager` | `ConnectionManager` | Manager reference |
| `profileManager` | `ProfileManager` | Profile reference |
| `playerName` | `string` | Player display name |

### `ConnectStatus` (enum)

| Value | Description |
|-------|-------------|
| `Undefined` | Default / unset |
| `Success` | Connection succeeded |
| `ServerFull` | Server at max capacity |
| `LoggedInAgain` | Duplicate connection detected |
| `UserRequestedDisconnect` | Player chose to disconnect |
| `GenericDisconnect` | Disconnected for unknown reason |
| `Reconnecting` | Auto-reconnection in progress |
| `IncompatibleBuildType` | Debug/Release mismatch |
| `HostEndedSession` | Host shut down the session |
| `StartHostFailed` | Host startup failed |
| `StartClientFailed` | Client startup failed |

### `ConnectionPayload` (struct)

| Field | Type | Description |
|-------|------|-------------|
| `playerId` | `string` | Unique player identifier |
| `playerName` | `string` | Display name |
| `isDebug` | `bool` | Debug build flag |

### `ConnectionEventMessage` (struct)

| Field | Type | Description |
|-------|------|-------------|
| `ConnectStatus` | `ConnectStatus` | Status enum |
| `PlayerName` | `string` | Affected player's name |

### `ReconnectMessage` (struct)

| Field | Type | Description |
|-------|------|-------------|
| `CurrentAttempt` | `int` | Current attempt number |
| `MaxAttempt` | `int` | Maximum attempts |

### `SessionPlayerData` : ISessionPlayerData

| Field | Type | Description |
|-------|------|-------------|
| `PlayerName` | `string` | Display name |
| `PlayerNumber` | `int` | Assigned player number |
| `AvatarNetworkGuid` | `NetworkGuid` | Avatar identifier |
| `CurrentHitPoints` | `int` | Current HP |
| `IsConnected` | `bool` | Connection status |
| `ClientID` | `ulong` | Netcode client ID |
| `HasCharacterSpawned` | `bool` | Whether character object exists |

---

## Infrastructure (`Unity.ConnectionManagement.Infrastructure`)

### `IPublisher<T>`

| Method | Signature |
|--------|-----------|
| `Publish(T message)` | `void` |

### `ISubscriber<T>`

| Method | Signature |
|--------|-----------|
| `Subscribe(Action<T> handler)` | `IDisposable` |
| `Unsubscribe(Action<T> handler)` | `void` |

### `IMessageChannel<T>` : IPublisher\<T\>, ISubscriber\<T\>, IDisposable

| Property | Type |
|----------|------|
| `IsDisposed` | `bool` |

### `IBufferedMessageChannel<T>` : IMessageChannel\<T\>

| Property | Type |
|----------|------|
| `HasBufferedMessage` | `bool` |
| `BufferedMessage` | `T` |

### `NetworkGuid` : INetworkSerializeByMemcpy

| Field | Type |
|-------|------|
| `FirstHalf` | `ulong` |
| `SecondHalf` | `ulong` |

### `NetworkGuidExtensions`

| Method | Signature |
|--------|-----------|
| `ToNetworkGuid(this Guid)` | `NetworkGuid` |
| `ToGuid(this NetworkGuid)` | `Guid` |

### `UpdateRunner` : MonoBehaviour

| Method | Signature | Description |
|--------|-----------|-------------|
| `Subscribe(Action<float>, float)` | `void` | Register for periodic updates |
| `Unsubscribe(Action<float>)` | `void` | Remove subscription |

---

## Sessions (`Unity.ConnectionManagement.Sessions`)

### `ISessionPlayerData`

| Property | Type | Description |
|----------|------|-------------|
| `IsConnected` | `bool` | Get/Set |
| `ClientID` | `ulong` | Get/Set |
| `Reinitialize()` | `void` | Reset for new game |

### `SessionManager<T>` where T : struct, ISessionPlayerData

| Member | Signature | Description |
|--------|-----------|-------------|
| `Instance` | `SessionManager<T>` | Singleton accessor |
| `SetupConnectingPlayerSessionData(ulong, string, T)` | `void` | Register player |
| `GetPlayerData(ulong)` | `T?` | Get by client ID |
| `GetPlayerData(string)` | `T?` | Get by player ID |
| `SetPlayerData(ulong, T)` | `void` | Update player data |
| `GetPlayerId(ulong)` | `string` | Map client → player ID |
| `DisconnectClient(ulong)` | `void` | Mark disconnected |
| `IsDuplicateConnection(string)` | `bool` | Check duplicate |
| `OnSessionStarted()` | `void` | Preserve disconnected data |
| `OnSessionEnded()` | `void` | Clean up + reinitialize |
| `OnServerEnded()` | `void` | Clear all |

---

## Unity Services (`Unity.ConnectionManagement.UnityServices.*`)

### `MultiplayerServicesFacade` : IDisposable, IStartable

See [Sessions & Services](sessions-and-services.md) for full API.

### `MultiplayerServicesInterface`

See [Sessions & Services](sessions-and-services.md) for full API.

### `LocalSession`

| Property | Type | Description |
|----------|------|-------------|
| `SessionID` | `string` | Session identifier |
| `SessionCode` | `string` | Join code |
| `RelayJoinCode` | `string` | Relay join code |
| `sessionUsers` | `Dictionary<string, LocalSessionUser>` | Players in session |
| `changed` | `event Action<LocalSession>` | Change notification |

### `LocalSessionUser`

| Property | Type | Description |
|----------|------|-------------|
| `IsHost` | `bool` | Host flag |
| `DisplayName` | `string` | Player name |
| `ID` | `string` | Player identifier |
| `changed` | `event Action<LocalSessionUser>` | Change notification |

### `RateLimitCooldown`

| Member | Type | Description |
|--------|------|-------------|
| `CooldownTimeLength` | `float` | Configured cooldown |
| `CanCall` | `bool` | Whether a call is allowed |
| `PutOnCooldown()` | `void` | Start cooldown timer |

---

## Utils (`Unity.ConnectionManagement.Utils`)

### `ClientPrefs` (static)

| Method | Returns | Description |
|--------|---------|-------------|
| `GetMasterVolume()` | `float` | Master volume (0–1) |
| `SetMasterVolume(float)` | `void` | Save master volume |
| `GetMusicVolume()` | `float` | Music volume (0–1) |
| `SetMusicVolume(float)` | `void` | Save music volume |
| `GetGuid()` | `string` | Persistent client GUID |
| `GetAvailableProfiles()` | `string` | Comma-separated profiles |
| `SetAvailableProfiles(string)` | `void` | Save profiles |

### `ProfileManager`

| Member | Type | Description |
|--------|------|-------------|
| `Profile` | `string` | Current profile |
| `AvailableProfiles` | `ReadOnlyCollection<string>` | All profiles |
| `onProfileChanged` | `event Action` | Profile changed |
| `CreateProfile(string)` | `void` | Add profile |
| `DeleteProfile(string)` | `void` | Remove profile |

### `SceneLoaderWrapper` : NetworkBehaviour

| Member | Type | Description |
|--------|------|-------------|
| `Instance` | `SceneLoaderWrapper` | Singleton |
| `LoadScene(name, useNetwork, mode)` | `void` | Load a scene |
| `OnSceneLoaded(Scene, LoadSceneMode)` | `virtual void` | Override for loading screen |
| `OnSceneEvent(SceneEvent)` | `virtual void` | Override for network scene events |

### `NetworkNameState` : NetworkBehaviour

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `NetworkVariable<FixedPlayerName>` | Synced player name |

### `FixedPlayerName` : INetworkSerializable

| Conversion | Direction |
|-----------|-----------|
| `implicit operator string` | FixedPlayerName → string |
| `implicit operator FixedPlayerName` | string → FixedPlayerName |
