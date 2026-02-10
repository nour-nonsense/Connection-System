# Integration Guide

[← Back to Index](index.md) · [Architecture](architecture.md)

---

## Prerequisites

Before installing the Connection Management package, ensure you have:

1. **Unity 2022.3+** (LTS recommended)
2. **Netcode for GameObjects** 2.0.0+
3. **Unity Transport** 2.0.0+
4. **VContainer** — Must be installed separately

### Installing VContainer

```
https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.16.4
```
Add via **Window → Package Manager → + → Add package from git URL**.

---

## Step 1: Install the Package

### Option A — Git URL (Recommended)

```
https://github.com/YOUR_USERNAME/com.unity.connectionmanagement.git
```

### Option B — Local Folder

1. Clone or download the package
2. **Window → Package Manager → + → Add package from disk**
3. Select the `package.json` file

---

## Step 2: Import the Sample Installer

1. Open **Window → Package Manager**
2. Find **Unity Connection Management**
3. Expand **Samples**
4. Click **Import** next to **Standard Installer**

This gives you `ConnectionSystemInstaller.cs` — a working VContainer `LifetimeScope`.

---

## Step 3: Set Up VContainer

Create or modify a `LifetimeScope` in your scene. Below is a minimal example based on the sample installer, with annotations:

```csharp
using Unity.ConnectionManagement;
using Unity.ConnectionManagement.Infrastructure;
using Unity.ConnectionManagement.UnityServices;
using Unity.ConnectionManagement.UnityServices.Sessions;
using Unity.ConnectionManagement.Utils;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // ── Core ────────────────────────────────────────────
        // The main state machine (MonoBehaviour in scene)
        builder.RegisterComponentInHierarchy<ConnectionManager>();

        // ── Unity Services ──────────────────────────────────
        // Facade for session management (IStartable → auto-starts)
        builder.Register<MultiplayerServicesFacade>(Lifetime.Singleton);

        // Local session state tracking
        builder.Register<LocalSession>(Lifetime.Singleton);
        builder.Register<LocalSessionUser>(Lifetime.Singleton);

        // ── Infrastructure ──────────────────────────────────
        // Periodic update loop (MonoBehaviour in scene)
        builder.RegisterComponentInHierarchy<UpdateRunner>();

        // ── Utils ───────────────────────────────────────────
        // Scene loading (MonoBehaviour in scene)
        builder.RegisterComponentInHierarchy<SceneLoaderWrapper>();

        // Profile management (for multi-instance testing)
        builder.Register<ProfileManager>(Lifetime.Singleton);

        // ── PubSub Message Channels ─────────────────────────
        // You need to provide MessageChannel<T> implementations.
        // Example using a simple implementation:
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
    }
}
```

> **Note:** `MessageChannel<T>` is a concrete implementation of `IMessageChannel<T>`. You must provide this class. See [Infrastructure](infrastructure.md) for the interface definitions.

---

## Step 4: Set Up the Scene

Your scene needs these GameObjects:

| GameObject | Components | Notes |
|-----------|------------|-------|
| **NetworkManager** | `NetworkManager`, `UnityTransport` | Standard NGO setup |
| **ConnectionManager** | `ConnectionManager` | Configure `MaxConnectedPlayers`, `NbReconnectAttempts` |
| **SceneLoaderWrapper** | `SceneLoaderWrapper` | Or your custom subclass |
| **UpdateRunner** | `UpdateRunner` | For service polling |
| **GameLifetimeScope** | Your `LifetimeScope` | VContainer entry point |

---

## Step 5: Connect from Your UI

### Host via IP

```csharp
[Inject] ConnectionManager m_ConnectionManager;

public void OnHostButtonClicked()
{
    m_ConnectionManager.StartHostIp("PlayerName", "127.0.0.1", 7777);
}
```

### Join via IP

```csharp
public void OnJoinButtonClicked()
{
    m_ConnectionManager.StartClientIp("PlayerName", "192.168.1.100", 7777);
}
```

### Host via Relay/Session

```csharp
public void OnHostSessionClicked()
{
    m_ConnectionManager.StartHostSession("PlayerName");
}
```

### Join via Relay/Session

```csharp
public void OnJoinSessionClicked()
{
    m_ConnectionManager.StartClientSession("PlayerName");
}
```

### Disconnect

```csharp
public void OnDisconnectClicked()
{
    m_ConnectionManager.RequestShutdown();
}
```

---

## Step 6: Subscribe to Events

React to connection status changes in your UI:

```csharp
[Inject] ISubscriber<ConnectStatus> m_StatusSubscriber;
[Inject] ISubscriber<ReconnectMessage> m_ReconnectSubscriber;

void Start()
{
    m_StatusSubscriber.Subscribe(OnStatusChanged);
    m_ReconnectSubscriber.Subscribe(OnReconnecting);
}

void OnStatusChanged(ConnectStatus status)
{
    switch (status)
    {
        case ConnectStatus.Success:
            HideLoadingScreen();
            break;
        case ConnectStatus.StartClientFailed:
            ShowError("Failed to connect to server.");
            break;
        case ConnectStatus.ServerFull:
            ShowError("Server is full.");
            break;
        case ConnectStatus.HostEndedSession:
            ShowError("The host ended the session.");
            break;
    }
}

void OnReconnecting(ReconnectMessage msg)
{
    ShowMessage($"Reconnecting... Attempt {msg.CurrentAttempt + 1}/{msg.MaxAttempt}");
}
```

---

## Step 7: Configure Unity Gaming Services (Optional)

If using **Relay/Session** mode:

1. **Link your project** in **Edit → Project Settings → Services**
2. Enable **Authentication**, **Relay**, and **Multiplayer** in the Unity Dashboard
3. Initialize services before connecting:

```csharp
await UnityServices.InitializeAsync();
await AuthenticationService.Instance.SignInAnonymouslyAsync();
```

4. Use `StartHostSession()` / `StartClientSession()` instead of the IP versions

---

## Customization Points

| What | How |
|------|-----|
| **Add loading screens** | Subclass `SceneLoaderWrapper`, override `OnSceneLoaded` / `OnSceneEvent` |
| **Change max players** | Set `ConnectionManager.MaxConnectedPlayers` in Inspector |
| **Change reconnect attempts** | Set `ConnectionManager.NbReconnectAttempts` in Inspector |
| **Add a new transport** | Create a new `ConnectionMethodBase` subclass ([see guide](connection-methods.md#adding-a-custom-connection-method)) |
| **Custom connection approval** | Override `HostingState.ApprovalCheck()` logic |
| **Custom scene on host start** | Modify `HostingState.Enter()` to load your scene |
| **Custom scene on offline** | Modify `OfflineState.Enter()` to load your menu scene |
