using System.Collections.Generic;
using Unity.Services.Multiplayer;

namespace Unity.ConnectionManagement.UnityServices.Sessions
{
    public struct SessionListFetchedMessage
    {
        public readonly IList<ISessionInfo> LocalSessions;

        public SessionListFetchedMessage(IList<ISessionInfo> localSessions)
        {
            LocalSessions = localSessions;
        }
    }
}
