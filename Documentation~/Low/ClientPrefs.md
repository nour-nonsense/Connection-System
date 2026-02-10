# `ClientPrefs.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/Utils/ClientPrefs.cs` |
| **Namespace** | `Unity.ConnectionManagement.Utils` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 70 |
| **Access** | `public static class` |

---

## Purpose

Static wrapper around `UnityEngine.PlayerPrefs` for storing persistent client-specific settings. Centralizes all PlayerPrefs keys to avoid string duplication and typos.

---

## Constants (PlayerPrefs Keys)

| Constant | Value | Type | Description |
|----------|-------|------|-------------|
| `k_MasterVolumeKey` | `"MasterVolume"` | string | Master audio volume |
| `k_MusicVolumeKey` | `"MusicVolume"` | string | Music audio volume |
| `k_ClientGuidKey` | `"client_guid"` | string | Persistent unique client identifier |
| `k_AvailableProfilesKey` | `"AvailableProfiles"` | string | Comma-separated profile list |

---

## Methods

### Volume Methods

```csharp
public static float GetMasterVolume()
{
    return PlayerPrefs.GetFloat(k_MasterVolumeKey, 0.5f);
}

public static void SetMasterVolume(float volume)
{
    PlayerPrefs.SetFloat(k_MasterVolumeKey, volume);
}
```

**Default:** `0.5f` (50% volume) if key doesn't exist.

`GetMusicVolume()` / `SetMusicVolume()` follow the same pattern with `k_MusicVolumeKey`.

### `GetGuid()` — Line 40

```csharp
public static string GetGuid()
{
    if (string.IsNullOrEmpty(s_ClientGuid))
    {
        s_ClientGuid = PlayerPrefs.GetString(k_ClientGuidKey, "");
        if (string.IsNullOrEmpty(s_ClientGuid))
        {
            s_ClientGuid = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(k_ClientGuidKey, s_ClientGuid);
        }
    }
    return s_ClientGuid;
}
```

**Lazy initialization with caching:**
1. Check static cache `s_ClientGuid`
2. If empty → read from PlayerPrefs
3. If still empty → generate new GUID, save to PlayerPrefs
4. Return cached value

**Static field:** `s_ClientGuid` persists for the application lifetime. Once set, PlayerPrefs is never re-read.

**GUID format:** Standard `Guid.NewGuid().ToString()` → `"550e8400-e29b-41d4-a716-446655440000"` (36 characters).

### Profile Methods

```csharp
public static string GetAvailableProfiles()
{
    return PlayerPrefs.GetString(k_AvailableProfilesKey, "");
}

public static void SetAvailableProfiles(string profiles)
{
    PlayerPrefs.SetString(k_AvailableProfilesKey, profiles);
}
```

**Format:** Comma-separated profile names, e.g., `"default,player2,player3"`. No validation or escaping — profile names must not contain commas.

---

## Storage

| Platform | PlayerPrefs Location |
|----------|---------------------|
| **Windows** | Registry: `HKCU\SOFTWARE\Unity\UnityEditor\CompanyName\ProductName` |
| **macOS** | `~/Library/Preferences/unity.CompanyName.ProductName.plist` |
| **Linux** | `~/.config/unity3d/CompanyName/ProductName/prefs` |

**No explicit save:** The code never calls `PlayerPrefs.Save()`. Unity auto-saves PlayerPrefs on application quit, but if the app crashes, unsaved prefs are lost.

---

## Thread Safety

Not thread-safe. `PlayerPrefs` is a Unity API that must be called from the main thread. The static `s_ClientGuid` field has no synchronization — safe only because Unity is single-threaded.
