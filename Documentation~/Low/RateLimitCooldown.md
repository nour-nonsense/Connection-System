# `RateLimitCooldown.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/UnityServices/Infrastructure/RateLimitCooldown.cs` |
| **Namespace** | `Unity.ConnectionManagement.UnityServices` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 26 |
| **Access** | `public class` |

---

## Purpose

Simple time-based cooldown to throttle API calls and prevent hitting Unity Gaming Services rate limits.

---

## Fields

| Field | Type | Access | Description |
|-------|------|--------|-------------|
| `m_CooldownTimeLength` | `float` | `readonly` | Duration in seconds |
| `m_CooldownFinishedTime` | `float` | `private` | `Time.time` when cooldown expires |

---

## Constructor

```csharp
public RateLimitCooldown(float cooldownTimeLength)
{
    m_CooldownTimeLength = cooldownTimeLength;
    m_CooldownFinishedTime = -1f;  // Ready immediately
}
```

---

## Properties

### `CooldownTimeLength` — `float` (get)

Returns `m_CooldownTimeLength`.

### `CanCall` — `bool` (get)

```csharp
public bool CanCall => Time.time > m_CooldownFinishedTime;
```

Returns `true` if enough time has passed since last `PutOnCooldown()`. Uses `Time.time` (unscaled real time if `Time.timeScale = 0`, depends on Unity version — in 2022.3, `Time.time` IS affected by `timeScale`).

**Warning:** If `Time.timeScale = 0` (e.g., game paused), `Time.time` stops advancing and the cooldown will never expire. Use `Time.unscaledTime` for pause-immune cooldowns.

---

## Methods

### `PutOnCooldown()` — Line 20

```csharp
public void PutOnCooldown()
{
    m_CooldownFinishedTime = Time.time + m_CooldownTimeLength;
}
```

---

## Usage Instances

Created in `MultiplayerServicesFacade.Start()`:

| Instance | Cooldown | Used By |
|----------|----------|---------|
| `m_RateLimitQuery` | 1.0s | `QueryAllSessions()` |
| `m_RateLimitJoin` | 1.0s | `TryJoinSessionByCodeAsync()`, `TryJoinSessionByNameAsync()` |
| `m_RateLimitQuickJoin` | 1.0s | `TryQuickJoinSessionAsync()` |
| `m_RateLimitHost` | 3.0s | `TryCreateSessionAsync()` |

**Host cooldown is longer** because session creation is more expensive server-side (allocates Relay, creates session record).

---

## Pattern

```csharp
if (!m_RateLimitHost.CanCall)
{
    Debug.LogWarning("Rate limited: please wait.");
    return;
}

// ... make API call ...

m_RateLimitHost.PutOnCooldown();
```

Cooldown is applied AFTER the call succeeds, not before. This means if a call fails fast, the cooldown still applies.
