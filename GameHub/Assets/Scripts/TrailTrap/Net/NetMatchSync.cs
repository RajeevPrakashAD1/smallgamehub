using Unity.Netcode;
using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Server->client match facts (M5): phase/countdown/winner and alive flags ride
    /// NetworkVariables; board resets and crashes ride RPCs. The client pushes them into
    /// GameManager's local view state — positions stream elsewhere (NetworkTransform),
    /// this channel carries only the facts positions can't express.
    /// </summary>
    public sealed class NetMatchSync : NetworkBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] PlayerController p1, p2;

        readonly NetworkVariable<byte>  _phase     = new();
        readonly NetworkVariable<float> _countdown = new();
        readonly NetworkVariable<byte>  _winner    = new();
        readonly NetworkVariable<bool>  _alive1    = new(true);
        readonly NetworkVariable<bool>  _alive2    = new(true);

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            game.OnBoardReset += RelayReset;
            game.OnCrash      += RelayCrash;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            game.OnBoardReset -= RelayReset;
            game.OnCrash      -= RelayCrash;
        }

        void FixedUpdate()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                _phase.Value     = (byte)game.MatchPhase;
                _countdown.Value = game.Countdown;
                _winner.Value    = (byte)game.Winner;
                _alive1.Value    = p1.State.alive;
                _alive2.Value    = p2.State.alive;
            }
            else
            {
                game.ClientApplyMatch((Phase)_phase.Value, _countdown.Value, _winner.Value);
                p1.State.alive = _alive1.Value;
                p2.State.alive = _alive2.Value;
            }
        }

        void RelayReset() => ResetBoardRpc();
        void RelayCrash() => CrashRpc();

        [Rpc(SendTo.NotServer)]
        void ResetBoardRpc() => game.ClientResetBoard();

        [Rpc(SendTo.NotServer)]
        void CrashRpc() => game.ClientNotifyCrash();
    }
}
