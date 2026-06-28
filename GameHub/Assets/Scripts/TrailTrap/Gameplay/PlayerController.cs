using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Drives one player. Two jobs, kept apart on purpose:
    ///  - <see cref="MovementStep"/> is the *pure* sim math (no Unity globals) — reused
    ///    unchanged by the server at M4 and easy to unit-test.
    ///  - <see cref="Tick"/> is the glue: run the math, then show the result on the transform.
    /// Input gathering comes in Step 3b; for now <c>_input</c> stays straight (turn = 0).
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        public PlayerState State;        // gameplay truth for this player

        public enum TurnScheme { MouseAim, Keyboard }   // dev pickers; mobile joystick comes later

        [Header("Input (dev)")]
        [SerializeField] TurnScheme scheme = TurnScheme.MouseAim;
        [SerializeField] KeyCode leftKey  = KeyCode.A;   // used only by Keyboard scheme
        [SerializeField] KeyCode rightKey = KeyCode.D;

        ITurnInput _turnSource;          // the chosen device, behind one interface
        InputFrame _input;               // "mailbox": Update() writes, Tick() reads

        Vector2 _spawnPos;               // remembered so a rematch can reset us (§5.4)
        float   _spawnHeading;

        void Awake()
        {
            _turnSource = scheme == TurnScheme.Keyboard
                ? new KeyboardTurnInput(leftKey, rightKey)
                : new MouseAimTurnInput(Camera.main);
        }

        // Gather input every frame. NOT simulation — we only record intent here.
        // Writing the same field every frame is robust: the tick can never "miss" input.
        void Update()
        {
            _input = new InputFrame { turn = _turnSource.Read(in State) };
        }

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
            ApplyToTransform();
        }

        /// <summary>Advance one fixed tick. Called from GameManager.FixedUpdate (Step 3c).</summary>
        public void Tick(float dt, SimConfig cfg)
        {
            State = MovementStep(State, _input, dt, cfg);
            ApplyToTransform();
        }

        /// <summary>
        /// Pure: same inputs always give the same output. No Time.deltaTime, no Input.*,
        /// no transform. dt is passed in so movement is tick-rate independent.
        /// </summary>
        public static PlayerState MovementStep(PlayerState p, InputFrame inp, float dt, SimConfig cfg)
        {
            if (!p.alive) return p;

            float turnRate = cfg.turnRateDeg * Mathf.Deg2Rad;            // degrees authored, radians used
            p.heading = Mathf.Repeat(p.heading + inp.turn * turnRate * dt, Mathf.PI * 2f);

            Vector2 fwd = new Vector2(Mathf.Cos(p.heading), Mathf.Sin(p.heading));
            p.position += fwd * (p.speed * dt);
            return p;
        }

        // Push the current state onto the visible transform (Z untouched: 2D).
        void ApplyToTransform()
        {
            transform.position = State.position;
            transform.rotation = Quaternion.Euler(0f, 0f, State.heading * Mathf.Rad2Deg);
        }
    }
}
