# `UpdateRunner.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/Infrastructure/UpdateRunner.cs` |
| **Namespace** | `Unity.ConnectionManagement.Infrastructure` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 102 |
| **Base class** | `MonoBehaviour` |
| **Access** | `public class` |

---

## Purpose

Provides a centralized `Update()` loop that dispatches to subscriber callbacks at configurable intervals. Allows non-MonoBehaviour classes (plain C# objects, VContainer services) to receive periodic updates without creating their own MonoBehaviours.

---

## Internal Data Structures

### `SubscriberData` — Private struct

```csharp
struct SubscriberData
{
    public Action<float> Handler;
    public float Period;
    public float NextCallTime;
}
```

| Field | Type | Description |
|-------|------|-------------|
| `Handler` | `Action<float>` | Callback receiving `deltaTime` since last call |
| `Period` | `float` | Minimum seconds between calls. `0` = every frame |
| `NextCallTime` | `float` | `Time.time` threshold for next invocation |

### `m_Subscribers` — `List<SubscriberData>`

Flat list of all subscribers. Iterated each `Update()`. Not sorted by `NextCallTime` — every subscriber is checked every frame even if not yet due.

---

## Methods

### `Subscribe(Action<float> handler, float period)` — Line 35

```csharp
public void Subscribe(Action<float> handler, float period)
{
    if (handler.Target == null)
    {
        throw new ArgumentException("Can't subscribe with a static method.");
    }

    if (handler.Method.Name.StartsWith("<"))
    {
        throw new ArgumentException("Can't subscribe with a local function or lambda.");
    }

    m_Subscribers.Add(new SubscriberData
    {
        Handler = handler,
        Period = period,
        NextCallTime = 0
    });
}
```

**Lambda/local function guard:** `handler.Method.Name.StartsWith("<")` — C# compiler generates methods starting with `<` for lambdas and local functions. These can't be reliably unsubscribed because each lambda creates a new delegate instance.

**Static method guard:** `handler.Target == null` — Static methods have null targets. While technically subscribable, the system blocks them as a design choice (possibly to ensure unsubscribe works reliably).

**Duplicate check:** None. The same handler can be subscribed multiple times, resulting in multiple calls per update. `Unsubscribe` removes only the first match.

### `Unsubscribe(Action<float> handler)` — Line 53

```csharp
public void Unsubscribe(Action<float> handler)
{
    for (int i = m_Subscribers.Count - 1; i >= 0; i--)
    {
        if (m_Subscribers[i].Handler == handler)
        {
            m_Subscribers.RemoveAt(i);
            return;  // Only removes FIRST match (reverse search)
        }
    }
}
```

**Reverse iteration:** Searches from end to beginning. `RemoveAt` shifts elements, so reverse search avoids issues. Returns after first match — if subscribed twice, only one is removed.

### `Update()` — Line 66

```csharp
void Update()
{
    float currentTime = Time.time;
    float deltaTime = Time.deltaTime;

    for (int i = m_Subscribers.Count - 1; i >= 0; i--)
    {
        var sub = m_Subscribers[i];
        if (currentTime >= sub.NextCallTime)
        {
            sub.Handler.Invoke(deltaTime);
            sub.NextCallTime = currentTime + sub.Period;
            m_Subscribers[i] = sub;  // Write back (struct copy)
        }
    }
}
```

**Reverse iteration:** Iterates backwards so that if a handler's callback leads to `Unsubscribe()`, the removal doesn't affect indices of not-yet-visited elements.

**Struct write-back:** `sub` is a copy (value type). After modifying `NextCallTime`, the modified copy must be written back to the list. This is a common C# struct-in-collection pattern.

**Period = 0 behavior:** `NextCallTime` is always `currentTime + 0 = currentTime`, so `currentTime >= NextCallTime` is always true. The handler runs every `Update()` frame.

---

## Performance Characteristics

| Scenario | Cost per Frame |
|----------|---------------|
| 10 subscribers, all period=0 | 10 delegate invocations |
| 10 subscribers, all period=5 | O(10) checks, ~2 invocations per second per subscriber |
| 100 subscribers | O(100) checks regardless of periods |

**Not optimized for many subscribers.** Linear scan every frame. For a typical game with <20 services, this is fine. For hundreds of subscribers, consider a priority queue sorted by `NextCallTime`.

---

## Thread Safety

Not thread-safe. `Subscribe()`, `Unsubscribe()`, and `Update()` must all run on the main thread. `Update()` is called by Unity's MonoBehaviour lifecycle, which is single-threaded.
