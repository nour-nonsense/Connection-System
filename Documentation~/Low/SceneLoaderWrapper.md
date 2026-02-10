# `SceneLoaderWrapper.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/Utils/SceneLoaderWrapper.cs` |
| **Namespace** | `Unity.ConnectionManagement.Utils` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 209 |
| **Base class** | `NetworkBehaviour` |
| **Pattern** | Singleton (`Instance`) |

---

## Purpose

Abstraction layer over Unity's `SceneManager` and Netcode's `NetworkSceneManager`. Provides a single `LoadScene()` API that works for both networked and non-networked scene transitions. Designed to be subclassed for loading screen integration.

---

## Singleton

```csharp
public static SceneLoaderWrapper Instance { get; private set; }

public override void OnNetworkSpawn()
{
    Instance = this;
    if (IsServer)
    {
        NetworkManager.SceneManager.OnSceneEvent += BaseOnSceneEvent;
    }
}

public override void OnNetworkDespawn()
{
    if (IsServer)
    {
        NetworkManager.SceneManager.OnSceneEvent -= BaseOnSceneEvent;
    }
    Instance = null;
}
```

**Lifecycle:** `Instance` is set on `OnNetworkSpawn()` (after `NetworkManager.StartHost/Client`), not on `Awake()`. This means `Instance` is null until the network is running. Non-networked scene loads (like `OfflineState.Enter()`) must null-check `Instance`.

**Server-only events:** `OnSceneEvent` is only subscribed on the server. Clients don't receive raw scene events — they get the loaded scene via network synchronization.

---

## Fields

| Field | Type | Access | Line | Description |
|-------|------|--------|------|-------------|
| `m_IsNetworkSceneManagementEnabled` | `bool` | `private` | 18 | Set to `true` when `NetworkSceneManager` is available |
| `m_LoadingScene` | `bool` | `private` | 19 | Prevents overlapping scene loads |

---

## Methods

### `LoadScene(string sceneName, bool useNetworkSceneManager, LoadSceneMode loadSceneMode)` — Line 45

```csharp
public void LoadScene(string sceneName, 
                       bool useNetworkSceneManager = false, 
                       LoadSceneMode loadSceneMode = LoadSceneMode.Single)
{
    if (useNetworkSceneManager)
    {
        if (IsSpawned && m_IsNetworkSceneManagementEnabled && !NetworkManager.ShutdownInProgress)
        {
            if (NetworkManager.IsServer)
            {
                if (m_LoadingScene)
                {
                    Debug.LogWarning("Scene loading already in progress!");
                    return;
                }
                m_LoadingScene = true;
                NetworkManager.SceneManager.LoadScene(sceneName, loadSceneMode);
            }
        }
    }
    else
    {
        var loadOperation = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
        if (loadOperation != null)
        {
            // Note: no callback hookup in base implementation
        }
    }
}
```

**Two paths:**

| `useNetworkSceneManager` | Behavior |
|--------------------------|----------|
| `true` | Server-only. Uses `NetworkManager.SceneManager.LoadScene()`. Synchronized to all clients. Has overlap guard. |
| `false` | Local only. Uses `SceneManager.LoadSceneAsync()`. No synchronization. No guard. |

**Guard conditions for network load:**
1. `IsSpawned` — NetworkObject must be spawned
2. `m_IsNetworkSceneManagementEnabled` — NetworkSceneManager available
3. `!ShutdownInProgress` — Not shutting down
4. `IsServer` — Only the server can initiate network scene loads
5. `!m_LoadingScene` — No other load in progress

### `OnSceneLoaded(Scene scene, LoadSceneMode mode)` — Virtual (Line 90)

```csharp
public virtual void OnSceneLoaded(Scene scene, LoadSceneMode mode) { }
```

**Override point** for subclasses to implement loading screens. Called when a scene completes loading.

### `OnSceneEvent(SceneEvent sceneEvent)` — Virtual (Line 95)

```csharp
public virtual void OnSceneEvent(SceneEvent sceneEvent) { }
```

**Override point** for subclasses to react to network scene events (load started, load completed, all clients loaded, etc.).

### `BaseOnSceneEvent(SceneEvent sceneEvent)` — Private (Line 100)

```csharp
void BaseOnSceneEvent(SceneEvent sceneEvent)
{
    switch (sceneEvent.SceneEventType)
    {
        case SceneEventType.LoadComplete:
            m_LoadingScene = false;    // Reset the overlap guard
            break;
        case SceneEventType.Unload:
            break;
        case SceneEventType.SynchronizeComplete:
            break;
    }
    
    OnSceneEvent(sceneEvent);  // Forward to virtual
}
```

**`m_LoadingScene` reset:** Only resets on `LoadComplete`. If the load fails or times out, `m_LoadingScene` stays `true` forever, blocking all future network scene loads. No timeout mechanism exists.

---

## Subclassing Example

```csharp
public class MySceneLoader : SceneLoaderWrapper
{
    [SerializeField] GameObject m_LoadingScreen;

    public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        m_LoadingScreen.SetActive(false);
    }

    public override void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType == SceneEventType.Load)
        {
            m_LoadingScreen.SetActive(true);
        }
    }
}
```

---

## Thread Safety

Not thread-safe. Scene loading must occur on the main thread (Unity requirement). All methods are main-thread only.

---

## Potential Issues

1. **`m_LoadingScene` stuck** — If a network scene load fails without firing `LoadComplete`, all future network loads are blocked.
2. **Null `Instance`** — Before `OnNetworkSpawn()`, `Instance` is null. `OfflineState.Enter()` null-checks this.
3. **Hardcoded scene names** — `"MainMenu"` in OfflineState and `"CharSelect"` in HostingState are hardcoded. Not configurable from SceneLoaderWrapper.
