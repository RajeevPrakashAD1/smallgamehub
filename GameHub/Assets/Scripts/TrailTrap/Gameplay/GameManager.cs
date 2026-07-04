using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Owns the simulation tick. One place decides the fixed order things happen each tick,
    /// so the sim is explicit and deterministic-enough for host authority later (M4).
    /// M1 wires up movement only; trail/collision/match steps slot in as we build them.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] SimConfig config;
        [SerializeField] PlayerController p1, p2;

        public TrailSystem Trails { get; private set; }   // gameplay truth; renderer/collision read it
        public PowerUpSystem PowerUps { get; private set; } // pickups on the board (§7); view reads it
        readonly MatchState _match = new();                // phase / countdown / winner (§5)
        float _playElapsed;                                // seconds spent Playing; drives speed ramp

        public Phase MatchPhase => _match.phase;           // read-only views (HUD, tests, later netcode)
        public int   Winner     => _match.winner;
        public float Countdown  => _match.countdown;       // seconds left (audio reads this for beeps)

        // Fired the tick a player crashes. Pure-view listeners (screen shake, crash SFX) subscribe;
        // the sim stays ignorant of them. Sim event in FixedUpdate -> view reacts in Update/LateUpdate.
        public event System.Action OnCrash;

        [Header("Tick")]
        [Tooltip("Fixed simulation rate in Hz. 25 Hz = a 0.04s tick (our locked default).")]
        [SerializeField] float tickRateHz = 25f;

        [Header("Spawn")]
        [Tooltip("How far left/right of center the two players start.")]
        [SerializeField] float spawnX = 6f;

        [Header("Practice (dev)")]
        [Tooltip("Solo mode: no opponent, and crashing respawns you instead of ending the match.")]
        [SerializeField] bool practiceMode;

        // Test/host seam: wire dependencies directly instead of via the Inspector. Call right
        // after AddComponent (before Start runs) so Start spawns with these values.
        public void Configure(SimConfig cfg, PlayerController player1, PlayerController player2, float spawnDistance)
        {
            config = cfg;
            p1 = player1;
            p2 = player2;
            spawnX = spawnDistance;
        }

        void Start()
        {
            Time.fixedDeltaTime = 1f / tickRateHz;   // make FixedUpdate run at our sim rate
            Trails = new TrailSystem();
            PowerUps = new PowerUpSystem();

            // Both face "up" (+Y) so they run parallel — spawning them toward each other made
            // every round a head-on crash. Practice mode centers P1 and drops the opponent.
            const float faceUp = Mathf.PI / 2f;
            p1.Spawn(practiceMode ? Vector2.zero : new Vector2(-spawnX, 0f), faceUp, config);
            if (practiceMode)
                p2.gameObject.SetActive(false);          // no opponent to dodge while testing
            else
                p2.Spawn(new Vector2(spawnX, 0f), faceUp, config);

            _match.StartMatch(config);          // begin in Countdown
            _playElapsed = 0f;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;     // the fixed tick step — never Time.deltaTime here

            // Dispatch on phase: countdown advances even though the sim is paused (§5.3).
            switch (_match.phase)
            {
                case Phase.Countdown: _match.TickCountdown(dt); return;   // "3..2..1", no sim
                case Phase.Over:      return;                            // frozen, await rematch
                case Phase.Playing:   break;
            }

            // Drain effect timers first, so a Boost that expires this tick stops boosting now.
            p1.Effects.Tick(dt);
            p2.Effects.Tick(dt);

            // Speed ramp (M2) times the Boost multiplier (M3). MovementStep just reads State.speed.
            float rampSpeed = SpeedAt(_playElapsed, config);
            p1.State.speed = rampSpeed * (p1.Effects.BoostActive ? config.boostMul : 1f);
            p2.State.speed = rampSpeed * (p2.Effects.BoostActive ? config.boostMul : 1f);
            _playElapsed += dt;

            // Fixed order: move -> trail -> fade -> collect -> collide -> resolve -> spawn.
            p1.Tick(dt, config);                     // 1. move          (§2)
            p2.Tick(dt, config);
            Trails.Append(p1, p2, dt, config);       // 2. lay trail pts (§3)
            Trails.Fade(dt, config);                 // 3. fade old      (§3)
            PowerUps.Collect(p1, p2, config, Trails); // 4. grab pickups  (§7) — before collide
            CollisionStep(dt);                       // 5. who crashed   (§4)
            if (practiceMode)
            {
                if (!p1.State.alive) RespawnPractice(); // never end — pop back and keep testing
            }
            else
            {
                _match.Step(p1, p2);                 // 6. resolve win   (§5)
            }
            PowerUps.SpawnTick(dt, config, Trails);  // 7. maybe spawn   (§7)
        }

        // Instant rematch: reset players + trails and re-enter Countdown — no scene reload (§5.4).
        public void Rematch()
        {
            p1.Respawn(config);
            p2.Respawn(config);
            Trails.Clear();
            PowerUps.Clear();
            _match.StartMatch(config);
            _playElapsed = 0f;
        }

        // Practice: a crash just puts P1 back at spawn with a clean board, still in Playing.
        void RespawnPractice()
        {
            p1.Respawn(config);
            Trails.Clear();          // wipe the loop you just crashed into so you don't re-die
            _playElapsed = 0f;       // reset the speed ramp for a fresh run
        }

        // Forward speed ramps baseSpeed -> maxSpeed over speedRampDuration, then holds. Mirrors the
        // trail fade ramp (§3.4): the late game gets faster AND more crowded — rising tension.
        static float SpeedAt(float elapsed, SimConfig cfg)
            => Mathf.Lerp(cfg.baseSpeed, cfg.maxSpeed,
                          Mathf.Clamp01(elapsed / cfg.speedRampDuration));

        // §4: a head crashes if it's within the stripe of any LIVE segment, or leaves the arena.
        void CollisionStep(float dt)
        {
            int immune = Mathf.CeilToInt(config.selfImmuneSeconds / dt);   // newest own-points to skip
            float r = config.trailWidth * 0.5f + config.grace;            // stripe half-width + forgiveness
            float rSq = r * r;

            // Evaluate BOTH before applying death. '|' not '||' so a true same-tick head-on
            // marks both dead -> §5 will call it a draw (not let check-order pick a winner).
            bool d1 = HitsTrail(p1, Trails.P1Trail, immune)   // own trail: skip newest N (self-immunity)
                    | HitsTrail(p1, Trails.P2Trail, 0)        // opponent's whole trail
                    | OutOfBounds(p1);
            bool d2 = HitsTrail(p2, Trails.P2Trail, immune)
                    | HitsTrail(p2, Trails.P1Trail, 0)
                    | OutOfBounds(p2);

            if (d1) p1.State.alive = false;
            if (d2) p2.State.alive = false;
            if (d1 || d2) OnCrash?.Invoke();   // someone died this tick -> notify view juice

            bool HitsTrail(PlayerController pl, System.Collections.Generic.IReadOnlyList<TrailPoint> t, int skipNewest)
            {
                if (!pl.State.alive) return false;
                if (pl.Effects.PhaseActive) return false;   // Phase (§7): pass through trails
                Vector2 head = pl.State.position;
                float now = Trails.Elapsed;
                int last = t.Count - 1 - skipNewest;          // segment i = points[i]..points[i+1]
                for (int i = 0; i < last; i++)
                {
                    // Erased (§7.5): a segment with a dead endpoint doesn't exist anymore.
                    if (t[i].deathAt <= now || t[i + 1].deathAt <= now) continue;
                    if (CollisionMath.DistSqToSegment(head, t[i].pos, t[i + 1].pos) <= rSq) return true;
                }
                return false;
            }

            bool OutOfBounds(PlayerController pl)
            {
                Vector2 h = pl.State.position;
                return Mathf.Abs(h.x) > config.arenaHalfSize.x
                    || Mathf.Abs(h.y) > config.arenaHalfSize.y;
            }
        }
    }
}
