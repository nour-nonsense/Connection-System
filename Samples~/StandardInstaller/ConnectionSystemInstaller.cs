using UnityEngine;
using VContainer;
using VContainer.Unity;
using Unity.Netcode;
using Unity.ConnectionManagement;
using Unity.ConnectionManagement.Infrastructure;
using Unity.ConnectionManagement.UnityServices;
using Unity.ConnectionManagement.UnityServices.Sessions;
using Unity.ConnectionManagement.Utils;

namespace Unity.ConnectionManagement.Samples
{
    /// <summary>
    /// A sample LifetimeScope that demonstrates how to bind the Connection Management system.
    /// Copy this code into your project's GameLifetimeScope or similar.
    /// </summary>
    public class ConnectionSystemInstaller : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Core Connection Management
            builder.Register<ConnectionManager>(Lifetime.Singleton);
            builder.Register<MultiplayerServicesFacade>(Lifetime.Singleton);

            // Infrastructure & Utils
            builder.RegisterComponentInHierarchy<UpdateRunner>();
            builder.Register<LocalSession>(Lifetime.Singleton);
            builder.Register<LocalSessionUser>(Lifetime.Singleton);
            builder.Register<ProfileManager>(Lifetime.Singleton);

            // Pub/Sub Channels
            builder.RegisterInstance(new MessageChannel<UnityServiceErrorMessage>()).AsImplementedInterfaces();
            builder.RegisterInstance(new MessageChannel<SessionListFetchedMessage>()).AsImplementedInterfaces();

            // Netcode for GameObjects
            // Assumes a NetworkManager exists in the scene or is created via code
            if (NetworkManager.Singleton != null)
            {
                builder.RegisterComponent(NetworkManager.Singleton);
            }
            else
            {
                Debug.LogWarning("NetworkManager.Singleton is null. Make sure NetworkManager is present in the scene.");
            }
        }
    }
}
