# `ProfileManager.cs` — Technical Reference

[← Back to Low-Level Index](index.md)

---

## File Metadata

| Property | Value |
|----------|-------|
| **Path** | `Runtime/Utils/ProfileManager.cs` |
| **Namespace** | `Unity.ConnectionManagement.Utils` |
| **Assembly** | `Unity.ConnectionManagement.Runtime` |
| **Lines** | 121 |
| **Access** | `public class` |

---

## Purpose

Manages multiple authentication profiles for multi-instance testing. Each profile gets a unique UGS authentication sign-in, allowing multiple editor instances (or builds) to connect as different players without conflicting.

---

## Fields

| Field | Type | Access | Line | Description |
|-------|------|--------|------|-------------|
| `m_AvailableProfiles` | `List<string>` | `private` | 18 | All known profiles. Persisted via `ClientPrefs`. |
| `m_Profile` | `string` | `private` | 19 | Currently active profile. |
| `m_Resolver` | `IObjectResolver` | `[Inject]` | 21 | VContainer resolver (not used directly). |

---

## Properties

### `Profile` — `string`

```csharp
public string Profile
{
    get
    {
        if (m_Profile == null)
        {
            m_Profile = GetProfile();
        }
        return m_Profile;
    }
    set
    {
        m_Profile = value;
        onProfileChanged?.Invoke();
    }
}
```

**Lazy initialization:** `GetProfile()` is called on first access.

### `AvailableProfiles` — `ReadOnlyCollection<string>`

```csharp
public ReadOnlyCollection<string> AvailableProfiles
{
    get
    {
        EnsureProfilesLoaded();
        return m_AvailableProfiles.AsReadOnly();
    }
}
```

**`AsReadOnly()`** creates a new `ReadOnlyCollection<T>` wrapper each call. Minor GC pressure.

---

## Events

| Event | Type | Description |
|-------|------|-------------|
| `onProfileChanged` | `event Action` | Fired when `Profile` property is set (including to the same value). |

---

## Profile Resolution

### `GetProfile()` — Private

```csharp
string GetProfile()
{
    // 1. Check command-line arguments
    var args = Environment.GetCommandLineArgs();
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "-AuthProfile" && i + 1 < args.Length)
        {
            return args[i + 1];
        }
    }

    // 2. Check Unity Editor data path (for ParrelSync)
    var editorPath = Application.dataPath;
    if (editorPath.Contains("_clone"))
    {
        // Extract clone number from path
        var cloneDir = Path.GetFileName(Path.GetDirectoryName(editorPath));
        return cloneDir;
    }

    // 3. Default profile
    return "default";
}
```

**Resolution order:**
1. **Command-line:** `-AuthProfile MyProfile` — Used for standalone builds running multiple instances
2. **Editor clone detection:** Checks if `Application.dataPath` contains `"_clone"` (ParrelSync or similar tools). Uses the clone folder name as the profile.
3. **Fallback:** `"default"`

**ParrelSync integration:** ParrelSync creates project clones with paths like `ProjectName_clone0`, `ProjectName_clone1`. This auto-detection means each clone gets a unique profile without manual configuration.

### `EnsureProfilesLoaded()` — Private

```csharp
void EnsureProfilesLoaded()
{
    if (m_AvailableProfiles == null || m_AvailableProfiles.Count == 0)
    {
        var profilesString = ClientPrefs.GetAvailableProfiles();
        if (!string.IsNullOrEmpty(profilesString))
        {
            m_AvailableProfiles = profilesString.Split(',').ToList();
        }
        else
        {
            m_AvailableProfiles = new List<string>() { "default" };
        }
    }
}
```

---

## Profile CRUD

### `CreateProfile(string name)` — Public

```csharp
public void CreateProfile(string name)
{
    EnsureProfilesLoaded();
    if (!m_AvailableProfiles.Contains(name))
    {
        m_AvailableProfiles.Add(name);
        SaveProfiles();
    }
}
```

**No validation:** Profile names can contain any characters including commas (which would break the comma-separated storage in `ClientPrefs`).

### `DeleteProfile(string name)` — Public

```csharp
public void DeleteProfile(string name)
{
    EnsureProfilesLoaded();
    if (m_AvailableProfiles.Contains(name))
    {
        m_AvailableProfiles.Remove(name);
        SaveProfiles();
    }
}
```

### `SaveProfiles()` — Private

```csharp
void SaveProfiles()
{
    ClientPrefs.SetAvailableProfiles(string.Join(",", m_AvailableProfiles));
}
```

---

## Connection to UGS Authentication

The profile name is appended to the client GUID in `ConnectionMethodBase.GetPlayerId()`:
```csharp
ClientPrefs.GetGuid() + m_ProfileManager.Profile
```

This composite string is used as the `playerId` for connection payloads and `SessionManager` lookups, ensuring different profiles produce different player IDs even on the same machine.

---

## Thread Safety

Not thread-safe. `Environment.GetCommandLineArgs()` and `Application.dataPath` are main-thread-safe reads. Profile modifications must be on the main thread.
