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
        readonly MatchState _match = new();                // phase / countdown / winner (§5)

        [Header("Tick")]
        [Tooltip("Fixed simulation rate in Hz. 25 Hz = a 0.04s tick (our locked default).")]
        [SerializeField] float tickRateHz = 25f;

        [Header("Spawn")]
        [Tooltip("How far left/right of center the two players start.")]
        [SerializeField] float spawnX = 6f;

        void Start()
        {
            Time.fixedDeltaTime = 1f / tickRateHz;   // make FixedUpdate run at our sim rate
            Trails = new TrailSystem();

            // §2.4: P1 starts left facing +X (heading 0), P2 right facing -X (heading π).
            p1.Spawn(new Vector2(-spawnX, 0f), 0f, config);
            p2.Spawn(new Vector2( spawnX, 0f), Mathf.PI, config);

            _match.StartMatch(config);          // begin in Countdown
        }

        void Update()
        {
            // Dev rematch trigger; M2 replaces with a winner-screen button/tap (§5.4).
            if (_match.phase == Phase.Over && Input.GetKeyDown(KeyCode.R))
                Rematch();
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

            // Fixed order: move -> trail -> fade -> collide -> resolve win.
            p1.Tick(dt, config);                // 1. move          (§2)
            p2.Tick(dt, config);
            Trails.Append(p1, p2, dt, config);  // 2. lay trail pts (§3)
            Trails.Fade(dt, config);            // 3. fade old      (§3)
            CollisionStep(dt);                  // 4. who crashed   (§4)
            _match.Step(p1, p2);                // 5. resolve win   (§5)
        }

        // Instant rematch: reset players + trails and re-enter Countdown — no scene reload (§5.4).
        public void Rematch()
        {
            p1.Respawn(config);
            p2.Respawn(config);
            Trails.Clear();
            _match.StartMatch(config);
        }

        // Temporary dev HUD (M2 replaces with real UI). Shows the match phase so we can see §5 work.
        void OnGUI()
        {
            GUI.skin.label.fontSize = 28;
            string msg = _match.phase switch
            {
                Phase.Countdown => Mathf.CeilToInt(_match.countdown).ToString(),
                Phase.Playing   => "",
                Phase.Over      => (_match.winner == 0 ? "DRAW" : $"P{_match.winner} WINS")
                                   + "   —   press R to rematch",
                _ => "",
            };
            if (msg != "")
                GUI.Label(new Rect(20, 20, 600, 60), msg);
        }

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

            bool HitsTrail(PlayerController pl, System.Collections.Generic.IReadOnlyList<TrailPoint> t, int skipNewest)
            {
                if (!pl.State.alive) return false;
                Vector2 head = pl.State.position;
                int last = t.Count - 1 - skipNewest;          // segment i = points[i]..points[i+1]
                for (int i = 0; i < last; i++)
                    if (CollisionMath.DistSqToSegment(head, t[i].pos, t[i + 1].pos) <= rSq) return true;
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
