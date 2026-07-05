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

        public static bool IsRunning => Nm != null && Nm.IsListening;
        public static bool IsServer  => IsRunning && Nm.IsServer;

        /// <summary>Start hosting (server + local player). True if hosting after the call.</summary>
        public static bool StartHost()
            => Nm != null && (Nm.IsListening ? Nm.IsHost : Nm.StartHost());

        public static void Shutdown() { if (IsRunning) Nm.Shutdown(); }
    }
}
