    # Utilities

[← Back to Index](index.md) · [Architecture](architecture.md)

---

## Overview

The Utils module provides helper classes for client-side preferences, player profiles, scene loading, and network name synchronization.

**Namespace:** `Unity.ConnectionManagement.Utils`

---

## `ClientPrefs`

A static wrapper around Unity's `PlayerPrefs` for storing local client settings.

### API

| Method | Key | Default | Purpose |
|--------|-----|---------|---------|
| `GetMasterVolume()` / `SetMasterVolume(float)` | `"MasterVolume"` | 0.5 | Master audio volume |
| `GetMusicVolume()` / `SetMusicVolume(float)` | `"MusicVolume"` | 0.8 | Music volume |
| `GetGuid()` | `"client_guid"` | Auto-generated | Persistent client install identifier |
| `GetAvailableProfiles()` / `SetAvailableProfiles(string)` | `"AvailableProfiles"` | `""` | Comma-separated profile list |

### `GetGuid()` Behavior

1. Checks if `client_guid` exists in PlayerPrefs
2. If yes → returns it
3. If no → generates a new `System.Guid`, saves it, returns it
4. This GUID persists across app restarts and identifies the install uniquely

---

## `ProfileManager`

Manages multiple authentication profiles for the same device. Useful for:
- Testing multiplayer locally with multiple editor instances ("ParrelSync" or Virtual Projects)
- Allowing multiple accounts on the same machine

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Profile` | `string` | Current active profile. Fires `onProfileChanged` when set |
| `AvailableProfiles` | `ReadOnlyCollection<string>` | List of saved profiles |

### Methods

| Method | Purpose |
|--------|---------|
| `CreateProfile(string)` | Add a new profile to the list |
| `DeleteProfile(string)` | Remove a profile from the list |

### Profile Resolution (`GetProfile()`)

The profile is resolved in this priority order:
1. **Command-line argument** → `-AuthProfile <profileId>` (for CI/testing)
2. **Editor** → MD5 hash of `Application.dataPath` (unique per project clone, max 30 chars for UGS auth)
3. **Build** → Empty string (default profile)

### Registration

```csharp
builder.Register<ProfileManager>(Lifetime.Singleton);
```

---

## `SceneLoaderWrapper`

A headless `NetworkBehaviour` singleton that wraps Unity's scene loading APIs. Handles both regular `SceneManager` and Netcode's `NetworkSceneManager`.

### Key Features

- **Singleton pattern** — `SceneLoaderWrapper.Instance`
- **DontDestroyOnLoad** — persists across scene loads
- **Extensible** — all methods are `virtual`, override for custom behavior (e.g., loading screens)
- **Headless** — no UI dependencies; override `OnSceneLoaded` and `OnSceneEvent` to add loading screen behavior

### API

```csharp
// Load a scene (network-managed, for host):
SceneLoaderWrapper.Instance.LoadScene("GameScene", useNetworkSceneManager: true);

// Load a scene (local only, e.g., main menu):
SceneLoaderWrapper.Instance.LoadScene("MainMenu", useNetworkSceneManager: false);
```

### LoadScene Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `sceneName` | `string` | Scene name or path |
| `useNetworkSceneManager` | `bool` | `true` = use NGO scene management, `false` = use SceneManager |
| `loadSceneMode` | `LoadSceneMode` | Default: `Single` (unloads current scenes) |

### Network Scene Management

When `useNetworkSceneManager: true`:
- Only works if `IsSpawned` and `IsServer`
- Calls `NetworkManager.SceneManager.LoadScene()`
- All connected clients will automatically load the scene

### Lifecycle

```
Awake() → Singleton setup + DontDestroyOnLoad
Start() → Subscribe to SceneManager.sceneLoaded + NetworkManager events
OnNetworkingSessionStarted() → Subscribe to NetworkSceneManager events
OnNetworkingSessionEnded() → Unsubscribe from NetworkSceneManager events
OnDestroy() → Unsubscribe from all events
```

### Extending for Loading Screens

```csharp
public class MySceneLoader : SceneLoaderWrapper
{
    [SerializeField] LoadingScreen m_LoadingScreen;

    protected override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        m_LoadingScreen.Hide();
    }

    protected override void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType == SceneEventType.Load)
            m_LoadingScreen.Show(sceneEvent.SceneName);
        else if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
            m_LoadingScreen.Hide();
    }
}
```

### Registration

```csharp
// Add as a component to a persistent GameObject, OR:
builder.RegisterComponentInHierarchy<SceneLoaderWrapper>();
```

---

## `NetworkNameState`

A simple `NetworkBehaviour` that synchronizes a player's display name across the network.

### Usage

```csharp
// On the server (owner):
networkNameState.Name.Value = "Player1";

// On any client (read):
string name = networkNameState.Name.Value.ToString();
```

### `FixedPlayerName`

A network-serializable wrapper around `FixedString32Bytes` (max 32 bytes / ~29 UTF-8 characters).

```csharp
public struct FixedPlayerName : INetworkSerializable
{
    // Implicit conversions:
    FixedPlayerName name = "PlayerName";  // string → FixedPlayerName
    string str = name;                     // FixedPlayerName → string
}
```

### Why Fixed-Size?

Netcode's `NetworkVariable` requires fixed-size types for efficient delta serialization. `FixedString32Bytes` avoids heap allocations and GC pressure during network sync.
