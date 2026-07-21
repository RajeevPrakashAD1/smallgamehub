using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Server-side glue between session and match (M5): when the remote player connects,
    /// hand them ownership of P2 and restart the match so both begin together. If they
    /// leave, take P2 back. Does nothing on pure clients or offline.
    /// </summary>
    public sealed class NetMatchLink : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] PlayerController p2;

        void OnEnable()
        {
            NetKit.Session.OnClientJoined += HandleJoined;
            NetKit.Session.OnClientLeft   += HandleLeft;
        }

        void OnDisable()
        {
            NetKit.Session.OnClientJoined -= HandleJoined;
            NetKit.Session.OnClientLeft   -= HandleLeft;
        }

        void HandleJoined(ulong clientId)
        {
            if (!NetKit.Session.IsServer) return;
            if (clientId == NetKit.Session.LocalClientId) return;  // host's own connection
            if (!p2.IsOwner) return;                               // P2 already handed out (1v1 is full)

            p2.NetworkObject.ChangeOwnership(clientId);
            game.Rematch();                                        // clean simultaneous start
        }

        void HandleLeft(ulong clientId)
        {
            if (!NetKit.Session.IsServer) return;
            if (p2.OwnerClientId != clientId) return;
            if (!p2.NetworkObject.IsSpawned) return;               // session teardown already despawned it
            p2.NetworkObject.RemoveOwnership();                    // P2 back to the host
        }
    }
}
