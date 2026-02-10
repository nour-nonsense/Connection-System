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
- Headless SceneLoaderWrapper (extensible, no UI dependencies)

## Prerequisites

> **⚠️ VContainer Required:** This package uses [VContainer](https://github.com/hadashiA/VContainer) for dependency injection. You **must** install it before installing this package.

### Install VContainer (choose one):

**Option A — Git URL (recommended):**
1. Open **Window > Package Manager**
2. Click **+** > **Add package from git URL...**
3. Enter:
   ```
   https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer
   ```

**Option B — Scoped Registry (auto-updates):**
Add this to your `Packages/manifest.json`:
```json
"scopedRegistries": [
  {
    "name": "OpenUPM",
    "url": "https://package.openupm.com",
    "scopes": [
      "jp.hadashikick.vcontainer"
    ]
  }
]
```
Then in Package Manager, switch to **My Registries** and install VContainer.

## Installation

### From Git URL
1. **Install VContainer first** (see Prerequisites above)
2. Open **Window > Package Manager**
3. Click **+** > **Add package from git URL...**
4. Enter:
   ```
   https://github.com/nour-nonsense/Connection-System.git
   ```

### From OpenUPM (after approval)
```bash
openupm add com.unity.connectionmanagement
```

### From Local Folder
1. Open **Window > Package Manager**
2. Click **+** > **Add package from disk...**
3. Navigate to this folder and select `package.json`

## Dependencies (auto-installed)
- Netcode for GameObjects 2.0+
- Unity Transport 2.0+
- Unity Services Core, Authentication, Multiplayer

## Quick Start

1. Install VContainer and this package (see above).
2. Add a `NetworkManager` and `ConnectionManager` to a GameObject in your scene.
3. Add a `SceneLoaderWrapper` component to a GameObject (or use a custom subclass).
4. Import the **Standard VContainer Installer** sample from Package Manager > Samples tab.
5. Copy the sample `ConnectionSystemInstaller` into your project's LifetimeScope.
6. Use `ConnectionManager.StartHostIp()` / `StartClientIp()` for direct IP connections.
7. Use `ConnectionManager.StartHostSession()` / `StartClientSession()` for Relay-based connections.
