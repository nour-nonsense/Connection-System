# Unity Connection Management

A connection management system for Unity Multiplayer projects using Netcode for GameObjects.

## Features
- State-machine-based connection lifecycle management
- Direct IP connection support
- Unity Relay connection support
- Automatic reconnection with configurable attempts
- Connection approval with payload validation
- Session management integration (Unity Gaming Services)
- PubSub messaging infrastructure

## Installation

### From Local Folder
1. Open **Window > Package Manager** in Unity
2. Click the **+** button > **Add package from disk...**
3. Navigate to this folder and select `package.json`

### From Git URL
1. Open **Window > Package Manager**
2. Click **+** > **Add package from git URL...**
3. Enter the git URL pointing to this folder

## Dependencies
- Netcode for GameObjects 2.0+
- Unity Transport 2.0+
- Unity Services Core, Authentication, Multiplayer
- VContainer (dependency injection)
- Unity Multiplayer Samples Utilities

## Quick Start

1. Add a `NetworkManager` and `ConnectionManager` to a GameObject in your scene.
2. Configure `MaxConnectedPlayers` and `NbReconnectAttempts` on the `ConnectionManager`.
3. Use `ConnectionManager.StartHostIp()` / `StartClientIp()` for direct IP connections.
4. Use `ConnectionManager.StartHostSession()` / `StartClientSession()` for Relay-based connections.
