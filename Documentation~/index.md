# Unity Connection Management — Documentation

> **Package:** `com.unity.connectionmanagement` · **Version:** 1.0.5  
> **Unity:** 2022.3+ · **Netcode:** 2.0+

## What is This Package?

Unity Connection Management is a **production-ready, headless (no UI)** connection system for multiplayer games built with **Netcode for GameObjects**. It provides a complete state‑machine‑driven connection lifecycle — from going online as a host or client, to automatic reconnection, connection approval, session management, and graceful shutdown.

Originally extracted from **Unity's official Boss Room sample**, it is designed to be dropped into any project as a reusable package.

---

## Documentation Map

| Document | Description |
|----------|-------------|
| [Architecture Overview](architecture.md) | High-level design, state machine diagram, dependency graph |
| [Connection States](connection-states.md) | Deep-dive into every state and its transitions |
| [Connection Methods](connection-methods.md) | IP vs Relay connection setup |
| [Session Management](sessions-and-services.md) | Unity Gaming Services integration, local session tracking |
| [Infrastructure](infrastructure.md) | PubSub messaging, UpdateRunner, NetworkGuid |
| [Utilities](utilities.md) | ClientPrefs, ProfileManager, SceneLoaderWrapper, NetworkNameState |
| [API Reference](api-reference.md) | Full class/struct/interface listing with signatures |
| [Integration Guide](integration-guide.md) | Step-by-step guide to wire this into your project |
| [Troubleshooting](troubleshooting.md) | Common errors and solutions |

---

## Quick Links

- **Start here →** [Integration Guide](integration-guide.md)
- **I want to understand the design →** [Architecture Overview](architecture.md)
- **I need to customize a state →** [Connection States](connection-states.md)
- **I'm getting errors →** [Troubleshooting](troubleshooting.md)

---

## Module Overview

```
Connection System/
├── Runtime/
│   ├── ConnectionManagement/          ← Core state machine
│   │   ├── ConnectionManager.cs       ← Entry point (MonoBehaviour)
│   │   ├── ConnectionMethod.cs        ← IP & Relay setup
│   │   ├── SessionPlayerData.cs       ← Per-player session data
│   │   └── ConnectionState/           ← 7 state classes
│   │       ├── ConnectionState.cs     ← Abstract base
│   │       ├── OnlineState.cs         ← Abstract online base
│   │       ├── OfflineState.cs
│   │       ├── ClientConnectingState.cs
│   │       ├── ClientConnectedState.cs
│   │       ├── ClientReconnectingState.cs
│   │       ├── StartingHostState.cs
│   │       └── HostingState.cs
│   ├── Infrastructure/                ← Shared utilities
│   │   ├── PubSub/IMessageChannel.cs  ← Publish/Subscribe system
│   │   ├── UpdateRunner.cs            ← Periodic update loop
│   │   └── NetworkGuid.cs             ← Network-safe GUID
│   ├── UnityServices/                 ← UGS integration
│   │   ├── Infrastructure/
│   │   │   ├── Messages/UnityServiceErrorMessage.cs
│   │   │   └── RateLimitCooldown.cs
│   │   └── Sessions/
│   │       ├── MultiplayerServicesFacade.cs
│   │       ├── MultiplayerServicesInterface.cs
│   │       ├── SessionManager.cs
│   │       ├── LocalSession.cs
│   │       ├── LocalSessionUser.cs
│   │       └── Messages/SessionListFetchedMessage.cs
│   └── Utils/                         ← Helper classes
│       ├── ClientPrefs.cs
│       ├── ProfileManager.cs
│       ├── SceneLoaderWrapper.cs
│       └── NetworkNameState.cs
├── Samples~/
│   └── StandardInstaller/
│       └── ConnectionSystemInstaller.cs
├── Documentation~/                    ← You are here
├── package.json
├── README.md
├── CHANGELOG.md
└── LICENSE.md
```
