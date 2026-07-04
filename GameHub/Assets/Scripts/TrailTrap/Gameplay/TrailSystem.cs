using System.Collections.Generic;
using UnityEngine;

namespace TrailTrap
{
    /// <summary>One stamped point of a trail. Its lifespan is locked the instant it's laid (§3.4).</summary>
    public struct TrailPoint
    {
        public Vector2 pos;       // where it was dropped (sim units)
        public float   deathAt;   // match time when it vanishes (fixed at birth)
    }

    /// <summary>
    /// The trail is gameplay TRUTH (a point list), not visuals — collision (§4), Eraser (§7)
    /// and netcode (§8) all read this. Each player owns one list, appended at the end, so it
    /// stays sorted by deathAt → fading = dropping from the front.
    /// </summary>
    public sealed class TrailSystem
    {
        readonly List<TrailPoint> _t1 = new(), _t2 = new();
        float _elapsed;   // match time, advanced each tick

        // Read-only views for the renderer / collision to consume.
        public IReadOnlyList<TrailPoint> P1Trail => _t1;
        public IReadOnlyList<TrailPoint> P2Trail => _t2;

        // Read-only sim clock — consumers decide "dead" by point.deathAt <= Elapsed.
        public float Elapsed => _elapsed;

        // Wipe both trails and reset match time (rematch, §5.4).
        public void Clear()
        {
            _t1.Clear();
            _t2.Clear();
            _elapsed = 0f;
        }

        // Tick step 2: drop one point per living player, with its death time locked in now.
        public void Append(PlayerController p1, PlayerController p2, float dt, SimConfig cfg)
        {
            _elapsed += dt;
            float death = _elapsed + CurrentFadeTime(_elapsed, cfg);
            if (p1.State.alive) _t1.Add(new TrailPoint { pos = p1.State.position, deathAt = death });
            if (p2.State.alive) _t2.Add(new TrailPoint { pos = p2.State.position, deathAt = death });
        }

        // Tick step 3: expired points are contiguous at the front (list is sorted by deathAt).
        // Perf: RemoveRange from the front is O(n) (array shift), but n is bounded (~225/player
        // at 9s/25Hz) so it's negligible. If profiling ever flags it, switch to a head-index
        // (advance instead of remove) or a ring buffer for O(1) fade. Not worth it now (§3.6).
        public void Fade(float dt, SimConfig cfg)
        {
            DropExpired(_t1);
            DropExpired(_t2);

            void DropExpired(List<TrailPoint> t)
            {
                int i = 0;
                while (i < t.Count && t[i].deathAt <= _elapsed) i++;
                if (i > 0) t.RemoveRange(0, i);
            }
        }

        // Eraser (§7): kill every point within radius, in BOTH trails, instantly. Points stay
        // in the lists (front-only removal is the sorted-list invariant) but stop counting —
        // collision and the view must skip dead points from now on.
        public void EraseAround(Vector2 center, float radius)
        {
            float rSq = radius * radius;
            Mark(_t1);
            Mark(_t2);

            void Mark(List<TrailPoint> t)
            {
                for (int i = 0; i < t.Count; i++)
                    if (t[i].deathAt > _elapsed && (t[i].pos - center).sqrMagnitude <= rSq)
                    {
                        var p = t[i];
                        p.deathAt = _elapsed;   // "you expired just now" — dead by the normal rule
                        t[i] = p;
                    }
            }
        }

        // Point lifespan ramps fadeStart -> fadeEnd over fadeRampDuration, then holds (§3.4).
        static float CurrentFadeTime(float elapsed, SimConfig cfg)
            => Mathf.Lerp(cfg.fadeStart, cfg.fadeEnd,
                          Mathf.Clamp01(elapsed / cfg.fadeRampDuration));
    }
}
