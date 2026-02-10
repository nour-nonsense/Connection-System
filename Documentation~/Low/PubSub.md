# PubSub System — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/Infrastructure/PubSub/IMessageChannel.cs` |
| **Namespace** | `Unity.ConnectionManagement.Infrastructure` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 27 |
| **Contains** | 4 interfaces |

---

## Purpose

Defines the contract for a type-safe publish/subscribe messaging system. Allows decoupled communication between states, services, and game UI.

---

## Interface Hierarchy

```
IDisposable
    │
    ▼
IMessageChannel<T>  ─────────────────── IBufferedMessageChannel<T>
    │         │                                    │
    ▼         ▼                                    │
IPublisher<T>  ISubscriber<T>                     │
                                     (adds buffering)
```

### `IPublisher<T>`

```csharp
public interface IPublisher<T>
{
    void Publish(T message);
}
```

**Single method.** Message is delivered synchronously to all subscribers in subscription order.

### `ISubscriber<T>`

```csharp
public interface ISubscriber<T>
{
    IDisposable Subscribe(Action<T> handler);
    void Unsubscribe(Action<T> handler);
}
```

**Two unsubscribe mechanisms:**
1. `Subscribe()` returns `IDisposable` — call `.Dispose()` to unsubscribe (preferred, works with `using`)
2. `Unsubscribe(handler)` — explicit removal by handler reference (requires keeping a reference to the same delegate instance)

### `IMessageChannel<T>` : IPublisher\<T\>, ISubscriber\<T\>, IDisposable

```csharp
public interface IMessageChannel<T> : IPublisher<T>, ISubscriber<T>, IDisposable
{
    bool IsDisposed { get; }
}
```

**Combined interface.** A single object that can both publish and subscribe. `Dispose()` cleans up all subscriptions and prevents further use.

### `IBufferedMessageChannel<T>` : IMessageChannel\<T\>

```csharp
public interface IBufferedMessageChannel<T> : IMessageChannel<T>
{
    bool HasBufferedMessage { get; }
    T BufferedMessage { get; }
}
```

**Adds last-message caching.** Late subscribers can read the most recent published message without waiting for a new one. Useful for status channels where a new subscriber needs the current state.

---

## Concrete Implementations

The package does **not** include concrete `MessageChannel<T>` implementations. Consumers must provide their own. The `ConnectionSystemInstaller` sample assumes a `MessageChannel<T>` class exists that implements `IMessageChannel<T>`.

**Minimal implementation contract:**
```csharp
// What the system expects:
public class MessageChannel<T> : IMessageChannel<T>
{
    List<Action<T>> handlers;
    
    void Publish(T msg) 
    {
        foreach (var h in handlers) h(msg);
    }
    
    IDisposable Subscribe(Action<T> handler) { /* add + return disposable */ }
    void Unsubscribe(Action<T> handler) { /* remove */ }
    void Dispose() { handlers.Clear(); }
    bool IsDisposed { get; }
}
```

---

## Usage in the Package

| Channel Type | Publisher(s) | Subscriber(s) |
|-------------|-------------|---------------|
| `IPublisher<ConnectStatus>` | All `ConnectionState` subclasses | Game UI |
| `IPublisher<ConnectionEventMessage>` | `HostingState` | Game UI (connect/disconnect notifications) |
| `IPublisher<ReconnectMessage>` | `ClientReconnectingState` | Game UI (reconnection progress) |
| `IPublisher<UnityServiceErrorMessage>` | `MultiplayerServicesFacade` | Game UI (UGS error display) |
| `IPublisher<SessionListFetchedMessage>` | `MultiplayerServicesFacade` | Game UI (session browser) |

---

## Design Notes

- **Interfaces only** — The package only defines contracts, not implementations. This follows the Dependency Inversion Principle.
- **Synchronous delivery** — `Publish()` calls handlers synchronously on the calling thread. No queuing or thread marshaling.
- **No message filtering** — All subscribers receive all messages of their subscribed type. Filtering must be done in handlers.
