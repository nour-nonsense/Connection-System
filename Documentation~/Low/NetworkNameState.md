# `NetworkNameState.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/Utils/NetworkNameState.cs` |
| **Namespace** | `Unity.ConnectionManagement.Utils` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 37 |
| **Base class** | `NetworkBehaviour` |

---

## Purpose

Synchronizes a player's display name across the network using a `NetworkVariable`. Attached to the player's NetworkObject so that all clients can read each others' names.

---

## Fields

| Field | Type | Access | Line | Description |
|-------|------|--------|------|-------------|
| `Name` | `NetworkVariable<FixedPlayerName>` | `public` | 12 | Replicated name. Default `ReadPerm = Everyone`, `WritePerm = Server`. |

---

## `FixedPlayerName` (nested struct)

```csharp
public struct FixedPlayerName : INetworkSerializable
{
    FixedString32Bytes m_Name;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref m_Name);
    }

    public static implicit operator string(FixedPlayerName s) => s.m_Name.ToString();
    public static implicit operator FixedPlayerName(string s) => 
        new FixedPlayerName { m_Name = new FixedString32Bytes(s) };

    public override string ToString() => m_Name.ToString();
}
```

### Memory Layout

```
FixedPlayerName (INetworkSerializable)
└── m_Name : FixedString32Bytes
    ├── utf8LengthInBytes : ushort (2 bytes)
    └── bytes             : fixed byte[30] (30 bytes)
    Total: 32 bytes
```

**`FixedString32Bytes`** — Unity.Collections fixed-size UTF-8 string:
- Max 30 bytes of UTF-8 data + 2 bytes length prefix
- No heap allocation (stack/inline)
- Truncates strings longer than 30 bytes without error
- For ASCII text: max 30 characters
- For multi-byte UTF-8 (e.g., CJK, emoji): fewer characters

### Network Serialization

`INetworkSerializable` implementation uses `BufferSerializer.SerializeValue(ref FixedString32Bytes)` which Netcode handles natively. The 32-byte struct is serialized directly — no JSON, no string conversion.

### Implicit Operators

| Conversion | From → To | Example |
|-----------|-----------|---------|
| `string` → `FixedPlayerName` | Assignment: `FixedPlayerName name = "Alice";` |
| `FixedPlayerName` → `string` | Read: `string s = playerName;` |

These implicit conversions make the struct feel like a string in usage while maintaining fixed-size network serialization.

---

## NetworkVariable Behavior

```csharp
public NetworkVariable<FixedPlayerName> Name = new NetworkVariable<FixedPlayerName>();
```

| Property | Default | Description |
|----------|---------|-------------|
| `ReadPermission` | `Everyone` | All clients can read |
| `WritePermission` | `Server` | Only server can modify |
| `Value` | default `FixedPlayerName` (empty string) | Initial value |

**Setting the name (server only):**
```csharp
networkNameState.Name.Value = playerName; // implicit string → FixedPlayerName
```

**Reading the name (any client):**
```csharp
string displayName = networkNameState.Name.Value; // implicit FixedPlayerName → string
```

**Change notification:**
```csharp
networkNameState.Name.OnValueChanged += (oldName, newName) =>
{
    Debug.Log($"Name changed from {(string)oldName} to {(string)newName}");
};
```

---

## Usage

Typically attached to the same `GameObject` as the player's `NetworkObject`. The server sets the name during connection approval/setup, and all clients automatically receive the synchronized value.
