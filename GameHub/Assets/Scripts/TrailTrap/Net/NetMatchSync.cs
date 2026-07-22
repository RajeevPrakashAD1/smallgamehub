using System.Collections.Generic;
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

        // Pickup board channel: the server mirrors PowerUpSystem.Board into this list only
        // on ticks the board's Version changed; clients copy arrivals back into their local
        // PowerUpSystem so PowerUpView needs no changes.
        struct NetPickup : INetworkSerializable, System.IEquatable<NetPickup>
        {
            public Vector2 pos;
            public byte type;

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref pos);
                s.SerializeValue(ref type);
            }

            public bool Equals(NetPickup other) => pos == other.pos && type == other.type;
        }

        readonly NetworkList<NetPickup> _pickups = new();
        int _pushedVersion = -1;                 // -1 so the first server tick always pushes
        static readonly List<PowerUp> Tmp = new();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                game.OnBoardReset += RelayReset;
                game.OnCrash      += RelayCrash;
                game.OnCrashAt    += RelayCrashAt;
                game.PowerUps.Erased    += RelayErase;
                game.PowerUps.Collected += RelayCollected;
            }
            else
            {
                _pickups.OnListChanged += OnPickupsChanged;
                MirrorBoard();                   // catch up on pickups that pre-date our join
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                game.OnBoardReset -= RelayReset;
                game.OnCrash      -= RelayCrash;
                game.OnCrashAt    -= RelayCrashAt;
                game.PowerUps.Erased    -= RelayErase;
                game.PowerUps.Collected -= RelayCollected;
            }
            else
            {
                _pickups.OnListChanged -= OnPickupsChanged;
            }
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

                if (_pushedVersion != game.PowerUps.Version)
                {
                    _pushedVersion = game.PowerUps.Version;
                    _pickups.Clear();
                    var board = game.PowerUps.Board;
                    for (int i = 0; i < board.Count; i++)
                        _pickups.Add(new NetPickup { pos = board[i].pos, type = (byte)board[i].type });
                }
            }
            else
            {
                game.ClientApplyMatch((Phase)_phase.Value, _countdown.Value, _winner.Value);
                p1.State.alive = _alive1.Value;
                p2.State.alive = _alive2.Value;
            }
        }

        void OnPickupsChanged(NetworkListEvent<NetPickup> _) => MirrorBoard();

        void MirrorBoard()
        {
            Tmp.Clear();
            foreach (var p in _pickups)
                Tmp.Add(new PowerUp { pos = p.pos, type = (PowerUpType)p.type });
            game.PowerUps.ClientSetBoard(Tmp);
        }

        void RelayReset() => ResetBoardRpc();
        void RelayCrash() => CrashRpc();
        void RelayCrashAt(Vector2 pos) => CrashAtRpc(pos);
        void RelayErase(Vector2 pos, float radius) => EraseRpc(pos, radius);
        void RelayCollected(Vector2 pos, PowerUpType type) => CollectedRpc(pos, (byte)type);

        [Rpc(SendTo.NotServer)]
        void ResetBoardRpc() => game.ClientResetBoard();

        [Rpc(SendTo.NotServer)]
        void CrashRpc() => game.ClientNotifyCrash();

        [Rpc(SendTo.NotServer)]
        void CrashAtRpc(Vector2 pos) => game.ClientNotifyCrashAt(pos);

        [Rpc(SendTo.NotServer)]
        void EraseRpc(Vector2 pos, float radius) => game.ClientEraseAt(pos, radius);

        [Rpc(SendTo.NotServer)]
        void CollectedRpc(Vector2 pos, byte type) => game.ClientNotifyCollected(pos, (PowerUpType)type);
    }
}
