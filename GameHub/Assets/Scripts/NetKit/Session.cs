using Unity.Netcode;

namespace NetKit
{
    /// <summary>
    /// Thin session facade over NGO's NetworkManager: one place to start/stop a session and ask
    /// our role. Game code talks to this, never to NetworkManager directly, so the connection
    /// layer (direct / Relay / matchmaking) can evolve without touching gameplay. Game-agnostic:
    /// this file must never know Trail Trap exists.
    /// </summary>
    public static class Session
    {
        static NetworkManager Nm => NetworkManager.Singleton;

        /// <summary>Fires when a client finishes connecting / disconnects. On the server this
        /// fires for every client (including the host's own); clients only hear about themselves.</summary>
        public static event System.Action<ulong> OnClientJoined;
        public static event System.Action<ulong> OnClientLeft;

        public static bool IsRunning => Nm != null && Nm.IsListening;
        public static bool IsServer  => IsRunning && Nm.IsServer;
        public static bool IsClientOnly => IsRunning && !Nm.IsServer;
        public static ulong LocalClientId => Nm != null ? Nm.LocalClientId : 0;

        /// <summary>Start hosting (server + local player). True if hosting after the call.</summary>
        public static bool StartHost()
            => Nm != null && (Nm.IsListening ? Nm.IsHost : (Hook() && Nm.StartHost()));

        /// <summary>Join a host as a client. True if connecting/connected after the call.</summary>
        public static bool StartClient()
            => Nm != null && (Nm.IsListening ? !Nm.IsServer : (Hook() && Nm.StartClient()));

        public static void Shutdown() { if (IsRunning) Nm.Shutdown(); }

        // Re-subscribe defensively (-= then +=) so calling Start* twice can't double-fire events.
        static bool Hook()
        {
            Nm.OnClientConnectedCallback  -= HandleConnected;
            Nm.OnClientConnectedCallback  += HandleConnected;
            Nm.OnClientDisconnectCallback -= HandleDisconnected;
            Nm.OnClientDisconnectCallback += HandleDisconnected;
            return true;
        }
        static void HandleConnected(ulong id)    => OnClientJoined?.Invoke(id);
        static void HandleDisconnected(ulong id) => OnClientLeft?.Invoke(id);
    }
}
