# `LocalSessionUser.cs` — Technical Reference

[← Back to Low-Level Index](index.md) · [LocalSession](LocalSession.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/UnityServices/Sessions/LocalSessionUser.cs` |
| **Namespace** | `Unity.ConnectionManagement.UnityServices.Sessions` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 128 |
| **Access** | `public class` |

---

## Purpose

Represents a single player within a local session mirror. Wraps player properties with change-notification events for reactive UI binding.

---

## Properties with Change Notification

Each property follows the pattern:
```csharp
bool m_IsHost;
public bool IsHost
{
    get => m_IsHost;
    set
    {
        if (m_IsHost != value)
        {
            m_IsHost = value;
            OnChanged();
        }
    }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsHost` | `bool` | `false` | Whether this user is the session host |
| `DisplayName` | `string` | `""` | User's display name |
| `ID` | `string` | `""` | Persistent user identifier (UGS Auth ID) |

---

## Events

| Event | Signature | Description |
|-------|-----------|-------------|
| `changed` | `event Action<LocalSessionUser>` | Fires when any property changes. Only fires if value actually differs. |

---

## Methods

### `CopyDataFrom(ISessionUser player)` — Line 68

```csharp
public void CopyDataFrom(ISessionUser player)
{
    ID = player.Id;
    DisplayName = player.Properties.TryGetValue("DisplayName", out var val)
        ? val.Value
        : "";
    // Note: IsHost is determined by comparing with session host ID, 
    // NOT from player properties
}
```

**Property access:** Player properties are stored as `Dictionary<string, SessionProperty>`. The `"DisplayName"` key is a convention used by this system.

### `CopyDataFrom(LocalSessionUser user)` — Line 80

```csharp
public void CopyDataFrom(LocalSessionUser user)
{
    IsHost = user.IsHost;
    DisplayName = user.DisplayName;
    ID = user.ID;
}
```

Local-to-local copy. Each setter may fire `changed` independently (could result in multiple events for one copy).

### `SessionUserData()` — Line 90

```csharp
public Dictionary<string, SessionProperty> SessionUserData()
{
    return new Dictionary<string, SessionProperty>
    {
        { "DisplayName", new SessionProperty(DisplayName) }
    };
}
```

**Returns session-compatible properties** for UGS SDK calls (e.g., `CreateSession`, `JoinSession`). Creates a new dictionary each call (GC allocation).

---

## GC Considerations

- `SessionUserData()` allocates a new `Dictionary<string, SessionProperty>` per call. Called on every session operation.
- `CopyDataFrom(LocalSessionUser)` may fire `changed` up to 3 times (one per property). Could be optimized to batch-fire once.
