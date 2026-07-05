using Unity.Netcode;
using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Drives one player. Now a NetworkBehaviour (LLD §8.3): the OWNER reads its input device
    /// and ships a quantized turn to the server; the SERVER runs the same pure MovementStep and
    /// NetworkTransform streams the result out. With no session running it short-circuits the
    /// RPC and behaves exactly like the local M1 version (practice mode, tests).
    /// </summary>
    public sealed class PlayerController : NetworkBehaviour
    {
        public PlayerState State;        // gameplay truth for this player (server-owned in a session)
        public ActiveEffects Effects;    // timed power-up effects (Boost...); read by the tick

        public enum TurnScheme { MouseAim, Keyboard, Joystick }

        [Header("Input")]
        [SerializeField] TurnScheme scheme = TurnScheme.MouseAim;
        [SerializeField] KeyCode leftKey  = KeyCode.A;          // used only by Keyboard scheme
        [SerializeField] KeyCode rightKey = KeyCode.D;
        [SerializeField] FloatingJoystick joystick;             // used only by Joystick scheme

        ITurnInput _turnSource;          // the chosen device, behind one interface
        sbyte _serverTurn;               // latest turn the server holds for us, quantized [-127,127]

        Vector2 _spawnPos;               // remembered so a rematch can reset us (§5.4)
        float   _spawnHeading;

        void Awake()
        {
            _turnSource = scheme switch
            {
                TurnScheme.Keyboard => new KeyboardTurnInput(leftKey, rightKey),
                TurnScheme.Joystick => new JoystickTurnInput(joystick),
                _                   => new MouseAimTurnInput(Camera.main),
            };
        }

        /// <summary>Override the input device (tests inject a stub; M5 swaps per-owner).</summary>
        public void SetTurnInput(ITurnInput source) => _turnSource = source;

        // Gather input every frame — but only where this player's human sits (the owner).
        void Update()
        {
            if (IsSpawned && !IsOwner) return;   // remote copy: the RPC feeds _serverTurn instead

            sbyte turn = Quantize(_turnSource.Read(in State));
            if (IsSpawned) SubmitTurnRpc(turn);  // owner -> server (runs locally, instantly, on host)
            else _serverTurn = turn;             // offline: no session, keep M1 behavior
        }

        [Rpc(SendTo.Server)]
        void SubmitTurnRpc(sbyte turn) => _serverTurn = turn;

        // One byte on the wire instead of four; 1/127 steps are far below the sim's noise floor.
        static sbyte Quantize(float turn)
            => (sbyte)Mathf.RoundToInt(Mathf.Clamp(turn, -1f, 1f) * 127f);

        /// <summary>Place the player at a spawn pose, remember it, and bring it to life.</summary>
        public void Spawn(Vector2 position, float headingRad, SimConfig cfg)
        {
            _spawnPos = position;
            _spawnHeading = headingRad;
            Respawn(cfg);
        }

        /// <summary>Reset to the remembered spawn pose (used by rematch, §5.4).</summary>
        public void Respawn(SimConfig cfg)
        {
            State = new PlayerState
            {
                position = _spawnPos,
                heading  = _spawnHeading,
                speed    = cfg.baseSpeed,
                alive    = true,
            };
            Effects = default;
            ApplyToTransform();
        }

        /// <summary>Advance one fixed tick. Server-only in a session (GameManager guards).</summary>
        public void ServerTick(float dt, SimConfig cfg)
        {
            State = MovementStep(State, new InputFrame { turn = _serverTurn / 127f }, dt, cfg);
            ApplyToTransform();
        }

        /// <summary>
        /// Pure: same inputs always give the same output. No Time.deltaTime, no Input.*,
        /// no transform. dt is passed in so movement is tick-rate independent.
        /// </summary>
        public static PlayerState MovementStep(PlayerState p, InputFrame inp, float dt, SimConfig cfg)
        {
            if (!p.alive) return p;

            float turnRate = cfg.turnRateDeg * Mathf.Deg2Rad;
            p.heading = Mathf.Repeat(p.heading + inp.turn * turnRate * dt, Mathf.PI * 2f);

            Vector2 fwd = new Vector2(Mathf.Cos(p.heading), Mathf.Sin(p.heading));
            p.position += fwd * (p.speed * dt);
            return p;
        }

        // Push the current state onto the visible transform. In a session the server's
        // NetworkTransform streams this to clients; the pure math never touches it.
        void ApplyToTransform()
        {
            transform.position = State.position;
            transform.rotation = Quaternion.Euler(0f, 0f, State.heading * Mathf.Rad2Deg);
        }
    }
}
