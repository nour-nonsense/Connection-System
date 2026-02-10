# `ConnectionSystemInstaller.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Samples~/StandardInstaller/ConnectionSystemInstaller.cs` |
| **Namespace** | `Unity.ConnectionManagement.Samples` |
| **Assembly** | (Sample — added to project's default assembly or a user-defined asmdef) |
| **Lines** | 61 |
| **Base class** | `LifetimeScope` (VContainer) |

---

## Purpose

Sample VContainer `LifetimeScope` demonstrating how to register all Connection Management package components for dependency injection. This is the reference implementation that consumers should copy and customize.

---

## Registration Table

```csharp
protected override void Configure(IContainerBuilder builder)
```

### MonoBehaviour Components (in-scene)

| Registration | Lifetime | Description |
|-------------|----------|-------------|
| `RegisterComponentInHierarchy<ConnectionManager>()` | Scene | Must exist in scene hierarchy |
| `RegisterComponentInHierarchy<UpdateRunner>()` | Scene | Must exist in scene hierarchy |
| `RegisterComponentInHierarchy<SceneLoaderWrapper>()` | Scene | Must exist in scene hierarchy |

**`RegisterComponentInHierarchy<T>()`** — VContainer searches the scene hierarchy for an existing `T` component and registers it. If not found, throws `VContainerException` at container build time.

### Plain C# Services

| Registration | Lifetime | Interfaces | Description |
|-------------|----------|------------|-------------|
| `Register<MultiplayerServicesFacade>(Lifetime.Singleton)` | Singleton | Self + `IStartable` + `IDisposable` | Auto-starts via `IStartable` |
| `Register<ProfileManager>(Lifetime.Singleton)` | Singleton | Self | |
| `Register<LocalSession>(Lifetime.Singleton)` | Singleton | Self | |
| `Register<LocalSessionUser>(Lifetime.Singleton)` | Singleton | Self | |

### Message Channels

```csharp
builder.RegisterInstance(new MessageChannel<ConnectStatus>())
       .AsImplementedInterfaces();
builder.RegisterInstance(new MessageChannel<ConnectionEventMessage>())
       .AsImplementedInterfaces();
builder.RegisterInstance(new MessageChannel<ReconnectMessage>())
       .AsImplementedInterfaces();
builder.RegisterInstance(new MessageChannel<UnityServiceErrorMessage>())
       .AsImplementedInterfaces();
builder.RegisterInstance(new MessageChannel<SessionListFetchedMessage>())
       .AsImplementedInterfaces();
```

**Pattern:** `RegisterInstance` → pre-allocated instance, not created by VContainer.

**`.AsImplementedInterfaces()`** — Registers the instance for all interfaces it implements: `IPublisher<T>`, `ISubscriber<T>`, `IMessageChannel<T>`, `IDisposable`.

**Critical:** Each `MessageChannel<T>` type requires a concrete `MessageChannel<T>` class. The package does NOT provide this class — consumers must implement it. The Boss Room sample project contains this implementation.

---

## VContainer Lifecycle

```
Scene loads → LifetimeScope.Awake()
  → Configure(builder) runs
    → All registrations recorded (no instantiation yet)
  → builder.Build()
    → MonoBehaviours resolved from hierarchy
    → Singletons instantiated
    → [Inject] fields injected into all registered objects
  → IStartable.Start() called on MultiplayerServicesFacade
    → Creates child scope for MultiplayerServicesInterface
    → Initializes rate limiters
  → ConnectionManager.Start() (Unity lifecycle)
    → m_Resolver.Inject(each state)
      → States receive their [Inject] fields
    → Initial state set to m_Offline
```

**Order dependency:** VContainer's `Build()` and `IStartable.Start()` run during `Awake()` phase. `ConnectionManager.Start()` runs during Unity's `Start()` phase (after all `Awake()`). This ordering is correct because states need IObjectResolver from VContainer before they can be injected.

---

## Customization Points

| What to Change | How |
|---------------|-----|
| Add more message channels | Add `RegisterInstance(new MessageChannel<YourMessage>()).AsImplementedInterfaces()` |
| Use custom SceneLoader | Register your subclass instead: `RegisterComponentInHierarchy<MySceneLoader>()` |
| Add game-specific services | Register in the same `Configure()` method |
| Use different lifetimes | Change `Lifetime.Singleton` to `Lifetime.Transient` or `Lifetime.Scoped` |

---

## Important Notes

1. **Not auto-included** — Samples are imported manually via Package Manager → Samples → Import
2. **Not in an asmdef** — Sample code doesn't have its own assembly definition. It's compiled into whatever assembly the user places it in.
3. **Requires `MessageChannel<T>`** — The most common integration issue. The `MessageChannel<T>` concrete class must exist in the project.
