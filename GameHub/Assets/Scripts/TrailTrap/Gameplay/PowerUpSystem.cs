using System.Collections.Generic;
using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Gameplay TRUTH for power-ups: the pickups on the board, collecting them, and spawning
    /// new ones. A plain class (no Unity base) like TrailSystem/MatchState, so it's testable and
    /// the server can own it at M4. The view (§ code sprites) only reads <see cref="Board"/>.
    /// </summary>
    public sealed class PowerUpSystem
    {
        readonly List<PowerUp> _board = new();
        float _nextIn;

        public IReadOnlyList<PowerUp> Board => _board;

        // Rematch: clear the board and re-arm the spawn timer.
        public void Clear()
        {
            _board.Clear();
            _nextIn = 0f;
        }

        // Tick step 4 (before collision): a head touching a pickup collects it this tick.
        public void Collect(PlayerController p1, PlayerController p2, SimConfig cfg)
        {
            CollectFor(p1, cfg);
            CollectFor(p2, cfg);
        }

        void CollectFor(PlayerController pl, SimConfig cfg)
        {
            if (!pl.State.alive) return;
            float rSq = cfg.pickupRadius * cfg.pickupRadius;
            Vector2 head = pl.State.position;

            for (int i = _board.Count - 1; i >= 0; i--)
                if ((head - _board[i].pos).sqrMagnitude <= rSq)
                {
                    Apply(pl, _board[i].type, cfg);
                    _board.RemoveAt(i);
                }
        }

        static void Apply(PlayerController pl, PowerUpType type, SimConfig cfg)
        {
            switch (type)
            {
                case PowerUpType.Boost: pl.Effects.boost = cfg.boostDur; break;
            }
        }

        // Tick step 7 (end of tick): count down, then try to drop one pickup in open space.
        public void SpawnTick(float dt, SimConfig cfg, TrailSystem trails)
        {
            if (_board.Count >= cfg.maxPickups) return;

            _nextIn -= dt;
            if (_nextIn > 0f) return;
            _nextIn = Random.Range(cfg.spawnMin, cfg.spawnMax);

            if (TryFindOpenSpot(cfg, trails, out Vector2 pos))
                _board.Add(new PowerUp { pos = pos, type = RandomType() });
        }

        static PowerUpType RandomType() => PowerUpType.Boost;   // one type in v1

        // A few random darts inside the arena; take the first that isn't sitting on a live trail.
        // Keeps pickups out in the open (GDD §4.1). Gives up after a few tries — try again next spawn.
        static bool TryFindOpenSpot(SimConfig cfg, TrailSystem trails, out Vector2 pos)
        {
            const float margin = 1f;       // keep pickups off the killing walls
            const float clearance = 1f;    // min distance from any live trail point
            Vector2 half = cfg.arenaHalfSize - Vector2.one * margin;
            float clearSq = clearance * clearance;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                pos = new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
                if (!NearTrail(pos, trails.P1Trail, clearSq) && !NearTrail(pos, trails.P2Trail, clearSq))
                    return true;
            }
            pos = default;
            return false;
        }

        static bool NearTrail(Vector2 p, IReadOnlyList<TrailPoint> trail, float clearSq)
        {
            for (int i = 0; i < trail.Count; i++)
                if ((trail[i].pos - p).sqrMagnitude <= clearSq) return true;
            return false;
        }
    }
}
