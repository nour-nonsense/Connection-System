# Infrastructure

[← Back to Index](index.md) · [Architecture](architecture.md)

---

## Overview

The Infrastructure layer provides foundational utilities used throughout the package. These are generic, reusable components with no dependency on connection-specific logic.

**Namespace:** `Unity.ConnectionManagement.Infrastructure`

---

## PubSub Messaging System

A lightweight **Publish/Subscribe** system for decoupled communication. States publish events; your game's UI or logic subscribes to them.

### Interfaces

#### `IPublisher<T>`

```csharp
public interface IPublisher<T>
{
    void Publish(T message);
}
```

#### `ISubscriber<T>`

```csharp
public interface ISubscriber<T>
{
    IDisposable Subscribe(Action<T> handler);
    void Unsubscribe(Action<T> handler);
}
```

#### `IMessageChannel<T>`

Combines both publisher and subscriber. This is what you register in VContainer.

```csharp
public interface IMessageChannel<T> : IPublisher<T>, ISubscriber<T>, IDisposable
{
    bool IsDisposed { get; }
}
```

#### `IBufferedMessageChannel<T>`

Extends `IMessageChannel<T>` with a buffered message (the last published message is retained).

```csharp
public interface IBufferedMessageChannel<T> : IMessageChannel<T>
{
    bool HasBufferedMessage { get; }
    T BufferedMessage { get; }
}
```

### Message Types Used

| Message Type | Published By | Purpose |
|-------------|-------------|---------|
| `ConnectStatus` | All `ConnectionState` subclasses | Connection status changes (Success, Failed, etc.) |
| `ConnectionEventMessage` | `HostingState` | Player connect/disconnect with name |
| `ReconnectMessage` | `ClientReconnectingState` | Reconnection progress (attempt X of Y) |
| `UnityServiceErrorMessage` | `MultiplayerServicesFacade` | UGS API errors |
| `SessionListFetchedMessage` | `MultiplayerServicesFacade` | Available sessions list |

### Registration in VContainer

```csharp
// In your LifetimeScope:
builder.RegisterInstance(new MessageChannel<ConnectStatus>()).AsImplementedInterfaces();
builder.RegisterInstance(new MessageChannel<ReconnectMessage>()).AsImplementedInterfaces();
builder.RegisterInstance(new MessageChannel<ConnectionEventMessage>()).AsImplementedInterfaces();
builder.RegisterInstance(new MessageChannel<UnityServiceErrorMessage>()).AsImplementedInterfaces();
builder.RegisterInstance(new MessageChannel<SessionListFetchedMessage>()).AsImplementedInterfaces();
```

### Subscribing to Events (in your game code)

```csharp
[Inject] ISubscriber<ConnectStatus> m_ConnectStatusSub;

void Start()
{
    m_ConnectStatusSub.Subscribe(OnConnectStatusChanged);
}

void OnConnectStatusChanged(ConnectStatus status)
{
    switch (status)
    {
        case ConnectStatus.Success:
            ShowMessage("Connected!");
            break;
        case ConnectStatus.ServerFull:
            ShowMessage("Server is full.");
            break;
        // ... handle other statuses
    }
}
```

> **Note:** You must provide a `MessageChannel<T>` implementation. The package defines the interfaces only. Use the implementation from VContainer samples or create your own.

---

## `UpdateRunner`

A MonoBehaviour that provides a **configurable-period update loop** for non-MonoBehaviour classes.

**Use case:** Services that need periodic polling (e.g., session heartbeats) without being coupled to a MonoBehaviour.

### API

```csharp
// Subscribe to receive updates every 2 seconds:
updateRunner.Subscribe(OnPeriodicUpdate, updatePeriod: 2.0f);

// Subscribe for per-frame updates:
updateRunner.Subscribe(OnFrameUpdate, updatePeriod: 0f);

// Unsubscribe:
updateRunner.Unsubscribe(OnPeriodicUpdate);
```

### Callback Signature

```csharp
void OnPeriodicUpdate(float deltaTime)
{
    // deltaTime = time since last call to this subscriber
}
```

### Safety Checks

`UpdateRunner` guards against common mistakes:
- **Null callbacks** → ignored with no error
- **Local functions** → rejected (they can go out of scope and can't be unsubscribed)
- **Anonymous lambdas** → rejected (same reason as local functions)

### Registration

```csharp
// In your LifetimeScope:
builder.RegisterComponentInHierarchy<UpdateRunner>();
```

---

## `NetworkGuid`

A network-serializable GUID struct, split into two `ulong` halves for efficient Netcode transmission.

### Definition

```csharp
public struct NetworkGuid : INetworkSerializeByMemcpy
{
    public ulong FirstHalf;
    public ulong SecondHalf;
}
```

### Extension Methods

```csharp
// Convert System.Guid to NetworkGuid:
NetworkGuid netGuid = myGuid.ToNetworkGuid();

// Convert NetworkGuid back to System.Guid:
Guid guid = netGuid.ToGuid();
```

### Usage

Used in `SessionPlayerData.AvatarNetworkGuid` to identify player avatars across the network without sending full 16-byte GUIDs as strings.
