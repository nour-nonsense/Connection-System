# Troubleshooting

[← Back to Index](index.md) · [Integration Guide](integration-guide.md)

---

## Compilation Errors

### `The type or namespace 'VContainer' could not be found`

**Cause:** VContainer is not installed in your project.

**Fix:**
1. Open **Window → Package Manager → + → Add package from git URL**
2. Enter: `https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.16.4`

### `The type or namespace 'Unity.Netcode' could not be found`

**Cause:** Netcode for GameObjects is not installed.

**Fix:**
1. Open **Window → Package Manager**
2. Search for **Netcode for GameObjects** (com.unity.netcode.gameobjects)
3. Install version **2.0.0** or later

### `The type or namespace 'MultiplayerService' could not be found`

**Cause:** Unity Multiplayer SDK is missing.

**Fix:**
1. Install `com.unity.services.multiplayer` (1.0.0+) via Package Manager

### `Assembly 'Unity.ConnectionManagement.Runtime' has reference to 'Unity.Collections'`

**Cause:** Unity Collections package is missing.

**Fix:** Install `com.unity.collections` via Package Manager. This is usually installed automatically with Netcode.

---

## Runtime Errors

### `No player data associated with client X`

**Where:** `HostingState.OnClientConnected()`

**Cause:** A client connected but their session data wasn't set up during connection approval. This usually means the ApprovalCheck was bypassed or failed silently.

**Fix:** Ensure `ConnectionManager.NetworkManager.ConnectionApprovalCallback` is properly wired (done automatically by `ConnectionManager.Start()`).

### `NullReferenceException` on `SceneLoaderWrapper.Instance`

**Cause:** `SceneLoaderWrapper` has not been added to the scene, or its `Awake()` hasn't run yet.

**Fix:**
1. Add a `SceneLoaderWrapper` component to a GameObject in your initial scene
2. Ensure it's registered in VContainer: `builder.RegisterComponentInHierarchy<SceneLoaderWrapper>()`

### `NetworkManager StartClient failed`

**Where:** `ClientConnectingState.ConnectClientAsync()`

**Cause:** `NetworkManager.StartClient()` returned false. Common reasons:
- NetworkManager already running
- Invalid transport configuration
- Port in use (for host)

**Fix:** Check Unity console for preceding warnings from NetworkManager or UnityTransport.

### `Can't subscribe to a local function...` / `Can't subscribe with an anonymous function...`

**Where:** `UpdateRunner.Subscribe()`

**Cause:** Passing a lambda or local function to `UpdateRunner`. These can't be unsubscribed reliably.

**Fix:** Use a named class member method instead:

```csharp
// ❌ Bad:
updateRunner.Subscribe((dt) => DoUpdate(dt), 1f);

// ✅ Good:
updateRunner.Subscribe(DoUpdate, 1f);
void DoUpdate(float dt) { /* ... */ }
```

---

## Connection Issues

### Client immediately disconnects after connecting

**Possible causes:**
1. **Debug/Release mismatch** — Client and host have different `Debug.isDebugBuild` values. Both must be the same build type.
2. **Server full** — `ConnectedClientsIds.Count >= MaxConnectedPlayers`
3. **Duplicate connection** — Same `playerId` is already connected
4. **Payload too large** — Connection data exceeds 1024 bytes

**Diagnosis:** Subscribe to `ISubscriber<ConnectStatus>` and check the returned status code.

### Reconnection always fails

**Possible causes:**
1. **Max attempts reached** — Increase `ConnectionManager.NbReconnectAttempts`
2. **Session deleted** — The host shut down and the session no longer exists
3. **Fatal disconnect reason** — `HostEndedSession`, `ServerFull`, or `IncompatibleBuildType` skip reconnection by design

**Timing defaults:**
- First attempt delay: 1 second
- Between attempts delay: 5 seconds

### Relay connection times out

**Possible causes:**
1. Unity Gaming Services not initialized (`UnityServices.InitializeAsync()`)
2. Not authenticated (`AuthenticationService.Instance.SignInAnonymouslyAsync()`)
3. Project not linked to Unity Dashboard
4. Relay/Multiplayer services not enabled in Dashboard

---

## VContainer Issues

### `VContainerException: Type not registered: ConnectionManager`

**Fix:** Register ConnectionManager in your LifetimeScope:

```csharp
builder.RegisterComponentInHierarchy<ConnectionManager>();
```

Ensure the `ConnectionManager` MonoBehaviour exists in the scene.

### `VContainerException: Type not registered: IPublisher<ConnectStatus>`

**Fix:** Register a MessageChannel for each message type:

```csharp
builder.RegisterInstance(new MessageChannel<ConnectStatus>())
       .AsImplementedInterfaces();
```

### States not being injected (NullReferenceException on state fields)

**Cause:** `ConnectionManager.Start()` calls `m_Resolver.Inject()` on each state. If the `IObjectResolver` is null, injection fails silently.

**Fix:** Ensure:
1. `ConnectionManager` is within a VContainer `LifetimeScope`
2. The LifetimeScope is on a parent or the same GameObject
3. The `[Inject]` attribute is from `VContainer`, not another DI framework

---

## Performance

### UpdateRunner causes frame drops

**Cause:** Too many subscribers running on every frame (period = 0).

**Fix:** Increase the update period for non-critical subscribers:

```csharp
// Instead of per-frame:
updateRunner.Subscribe(MyUpdate, 0f);

// Use a reasonable interval:
updateRunner.Subscribe(MyUpdate, 2f); // every 2 seconds
```

---

## Scene Loading

### Scene not found error

**Cause:** Scene is not added to Build Settings.

**Fix:**
1. **File → Build Settings → Add Open Scenes**
2. Ensure both "MainMenu" and "CharSelect" (or your custom scene names) are listed

### Network scene load not propagating to clients

**Cause:** `useNetworkSceneManager` is `false`, or the host hasn't spawned yet.

**Fix:** Use `SceneLoaderWrapper.Instance.LoadScene("Scene", useNetworkSceneManager: true)` — this requires the host to be spawned (`IsServer && IsSpawned`).
