# Agones Dedicated Server Integration

The Connection System includes built-in support for **Agones**, an open-source dedicated game server orchestrator for Kubernetes.

This integration allows your game server to:
1.  **Boot Headlessly**: Automatically start in server mode without a local client.
2.  **Connect to Sidecar**: Register with the local Agones sidecar via the official SDK.
3.  **Manage Lifecycle**: Report `Ready` state, handle `Allocated` events, and shutdown gracefully.
4.  **Report Health**: sending periodic health pings to Agones.

---

## Architecture: "Agones Priority" Fallback

The system is designed to enable Agones in production without breaking your local workflow. 

**Logic in `StartingServerState`:**
1.  **Check for Adapter**: Is `IHostingAdapter` registered in the DI container?
2.  **Prioritize Agones**: If yes, try to `InitializeAsync()`. 
    - If successful → Enter `DedicatedServerHostingState`.
3.  **Fallback to Local**: If no adapter is found or initialization fails → Fall back to standard `HostingState`.

**Result**: You can run the same build locally (no Agones) and in the cloud (with Agones) without changing code.

---

## 1. Installation

To enable Agones support, you must install the **Agones Unity SDK**.

1.  Open **Package Manager**.
2.  Add package from git URL: `https://github.com/googleforgames/agones.git?path=/sdks/unity`
3.  Add `AGONES_SDK` to your **Scripting Define Symbols** (Project Settings > Player > Other Settings).

> [!NOTE]
> The `AgonesHostingAdapter` code is wrapped in `#if AGONES_SDK`. It will not compile or run unless you define this symbol.

---

## 2. Setup (VContainer)

Register the adapter in your `LifetimeScope` (e.g., `ConnectionSystemInstaller.cs` or a server-specific scope).

```csharp
protected override void Configure(IContainerBuilder builder)
{
    // ... other registrations ...

    // Only register the adapter if running in Batch Mode (headless server)
    // AND if the SDK is present.
#if AGONES_SDK
    if (Application.isBatchMode)
    {
        builder.Register<IHostingAdapter, AgonesHostingAdapter>(Lifetime.Singleton);
    }
#endif
}
```

---

## 3. Configuration

The `AgonesHostingAdapter` reads configuration from the environment (Standard Agones behavior).
- **Port**: Determined by Agones (via simple-udp or similar) or passed via command line args.
- **Allocation**: When your server is allocated, it receives metadata (Map name, Mode, etc.).

### Receiving Allocation Data

In `DedicatedServerHostingState.cs`, the server automatically:
1.  Listens for `OnAllocated`.
2.  Calls `GetAllocationDataAsync()`.
3.  Loads the scene defined in the allocation label `agones.dev/map` (defaults to "CharSelect").

To change this, modify `DedicatedServerHostingState.HandleAllocated`.

---

## Troubleshooting

- **Local Testing**: If you run in Editor, the adapter is likely not registered (unless you forced it). Even if registered, `InitializeAsync` will fail (no sidecar found) and it will seamlessly fall back to local hosting.
- **"AgonesSDK not found"**: Ensure you added `AGONES_SDK` to Scripting Define Symbols.
- **Health Check Failures**: Check `DedicatedServerHostingState.HealthCheckLoop`. By default, it pings every 5 seconds.
