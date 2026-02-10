# `NetworkGuid.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/Infrastructure/NetworkGuid.cs` |
| **Namespace** | `Unity.ConnectionManagement.Infrastructure` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 31 |
| **Implements** | `INetworkSerializeByMemcpy` |
| **Type** | `struct` (unmanaged value type) |

---

## Purpose

Network-serializable representation of a GUID. Splits a 128-bit GUID into two 64-bit halves for efficient network transmission using Netcode's `INetworkSerializeByMemcpy` (zero-copy memcpy serialization).

---

## Memory Layout

```
NetworkGuid (16 bytes total, no alignment padding needed)
├── FirstHalf  : ulong (8 bytes) — bits 0–63 of the GUID
└── SecondHalf : ulong (8 bytes) — bits 64–127 of the GUID
```

**`INetworkSerializeByMemcpy`** — Marker interface telling Netcode this struct can be serialized by copying its raw bytes directly. No reflection, no field-by-field serialization. This is the fastest serialization method in Netcode.

**Requirements for memcpy serialization:**
- Struct must be unmanaged (no reference type fields)
- Layout must be sequential (default for unmanaged structs)
- No padding issues between platforms (guaranteed by using only `ulong` fields)

---

## Extension Methods — `NetworkGuidExtensions`

### `ToNetworkGuid(this Guid guid)` — Static

```csharp
public static NetworkGuid ToNetworkGuid(this Guid id)
{
    var networkId = new NetworkGuid();
    var bytes = id.ToByteArray();
    networkId.FirstHalf = BitConverter.ToUInt64(bytes, 0);
    networkId.SecondHalf = BitConverter.ToUInt64(bytes, 8);
    return networkId;
}
```

**Allocation:** `id.ToByteArray()` allocates a 16-byte array on the heap each call. For frequent conversions, consider `Guid.TryWriteBytes(Span<byte>)` (available in .NET Standard 2.1+).

### `ToGuid(this NetworkGuid networkId)` — Static

```csharp
public static Guid ToGuid(this NetworkGuid networkId)
{
    var bytes = new byte[16];
    Buffer.BlockCopy(BitConverter.GetBytes(networkId.FirstHalf), 0, bytes, 0, 8);
    Buffer.BlockCopy(BitConverter.GetBytes(networkId.SecondHalf), 0, bytes, 8, 8);
    return new Guid(bytes);
}
```

**Allocations:**
- `BitConverter.GetBytes(ulong)` → 8-byte array (x2)
- `new byte[16]` → 16-byte array
- Total: 3 heap allocations per conversion

**Endianness:** `BitConverter` uses the native platform's byte order. Both `ToNetworkGuid` and `ToGuid` use the same conversion, so round-tripping on the same platform is lossless. However, if a little-endian client communicates with a big-endian server, the GUID would be mangled. In practice, Unity targets are almost exclusively little-endian.

---

## Usage

| Location | Usage |
|----------|-------|
| `SessionPlayerData.AvatarNetworkGuid` | Stores avatar/character selection as a GUID |
| `HostingState.ApprovalCheck()` | Initializes with empty `new NetworkGuid()` (all zeros) |

---

## Equality

No custom `Equals` or `GetHashCode` — uses the default value-type equality (field-by-field comparison). As a purely value-type struct, this is correct but uses reflection under the hood for `GetHashCode`. If used as a dictionary key, consider overriding for performance.
