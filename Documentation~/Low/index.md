# Low-Level Technical Documentation

[← Back to Main Documentation](../index.md)

---

This folder contains **per-file, implementation-level** documentation for every source file in the Connection Management package. Each document covers:

- **File location, namespace, and assembly**
- **Complete field/property inventory** with types, access modifiers, and memory layout
- **Method-by-method internals** with call chains, control flow, and edge cases
- **Threading/timing considerations**
- **Internal data flow** between fields and methods
- **Serialization details** where applicable

---

## File Index

### Core — ConnectionManagement

| Document | Source File | Lines |
|----------|-----------|-------|
| [ConnectionManager](ConnectionManager.md) | `Runtime/ConnectionManagement/ConnectionManager.cs` | 183 |
| [ConnectionState](ConnectionState.md) | `Runtime/ConnectionManagement/ConnectionState/ConnectionState.cs` | 46 |
| [OnlineState](OnlineState.md) | `Runtime/ConnectionManagement/ConnectionState/OnlineState.cs` | 22 |
| [OfflineState](OfflineState.md) | `Runtime/ConnectionManagement/ConnectionState/OfflineState.cs` | 66 |
| [ClientConnectingState](ClientConnectingState.md) | `Runtime/ConnectionManagement/ConnectionState/ClientConnectingState.cs` | 82 |
| [ClientConnectedState](ClientConnectedState.md) | `Runtime/ConnectionManagement/ConnectionState/ClientConnectedState.cs` | 44 |
| [ClientReconnectingState](ClientReconnectingState.md) | `Runtime/ConnectionManagement/ConnectionState/ClientReconnectingState.cs` | 141 |
| [StartingHostState](StartingHostState.md) | `Runtime/ConnectionManagement/ConnectionState/StartingHostState.cs` | 96 |
| [StartingServerState](StartingServerState.md) | `Runtime/ConnectionManagement/ConnectionState/StartingServerState.cs` | ~80 |
| [HostingState](HostingState.md) | `Runtime/ConnectionManagement/ConnectionState/HostingState.cs` | 165 |
| [ConnectionMethod](ConnectionMethod.md) | `Runtime/ConnectionManagement/ConnectionMethod.cs` | 168 |
| [SessionPlayerData](SessionPlayerData.md) | `Runtime/ConnectionManagement/SessionPlayerData.cs` | 40 |

### Infrastructure

| Document | Source File | Lines |
|----------|-----------|-------|
| [PubSub](PubSub.md) | `Runtime/Infrastructure/PubSub/IMessageChannel.cs` | 27 |
| [UpdateRunner](UpdateRunner.md) | `Runtime/Infrastructure/UpdateRunner.cs` | 102 |
| [NetworkGuid](NetworkGuid.md) | `Runtime/Infrastructure/NetworkGuid.cs` | 31 |

### Unity Services

| Document | Source File | Lines |
|----------|-----------|-------|
| [MultiplayerServicesFacade](MultiplayerServicesFacade.md) | `Runtime/UnityServices/Sessions/MultiplayerServicesFacade.cs` | 466 |
| [MultiplayerServicesInterface](MultiplayerServicesInterface.md) | `Runtime/UnityServices/Sessions/MultiplayerServicesInterface.cs` | 107 |
| [SessionManager](SessionManager.md) | `Runtime/UnityServices/Sessions/SessionManager.cs` | 278 |
| [LocalSession](LocalSession.md) | `Runtime/UnityServices/Sessions/LocalSession.cs` | 223 |
| [LocalSessionUser](LocalSessionUser.md) | `Runtime/UnityServices/Sessions/LocalSessionUser.cs` | 128 |
| [Messages](Messages.md) | Multiple message structs | — |
| [RateLimitCooldown](RateLimitCooldown.md) | `Runtime/UnityServices/Infrastructure/RateLimitCooldown.cs` | 26 |

### Utilities

| Document | Source File | Lines |
|----------|-----------|-------|
| [SceneLoaderWrapper](SceneLoaderWrapper.md) | `Runtime/Utils/SceneLoaderWrapper.cs` | 209 |
| [ClientPrefs](ClientPrefs.md) | `Runtime/Utils/ClientPrefs.cs` | 70 |
| [ProfileManager](ProfileManager.md) | `Runtime/Utils/ProfileManager.cs` | 121 |
| [NetworkNameState](NetworkNameState.md) | `Runtime/Utils/NetworkNameState.cs` | 37 |

### Samples

| Document | Source File | Lines |
|----------|-----------|-------|
| [ConnectionSystemInstaller](ConnectionSystemInstaller.md) | `Samples~/StandardInstaller/ConnectionSystemInstaller.cs` | 61 |
