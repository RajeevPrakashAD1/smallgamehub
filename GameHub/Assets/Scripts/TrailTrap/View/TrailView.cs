using System.Collections.Generic;
using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Pure VIEW: mirrors each player's trail-point list into LineRenderers. One renderer can
    /// only show ONE gradient, so the list is split into contiguous RUNS of same state
    /// (live / erased-dead), one pooled renderer per run (§7 Eraser view).
    /// </summary>
    public sealed class TrailView : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] LineRenderer line1, line2;   // authored renderers = pool slot 0 + clone template

        [Header("Trail colors (HDR — enables glow with Bloom)")]
        [ColorUsage(true, true)] [SerializeField] Color color1 = Color.cyan;
        [ColorUsage(true, true)] [SerializeField] Color color2 = Color.magenta;

        // Prebuilt once — Gradient is a class; building per frame would allocate garbage.
        Gradient _live1, _live2, _dead;
        readonly List<LineRenderer> _pool1 = new(), _pool2 = new();

        void Start()
        {
            _live1 = FadeGradient(color1);
            _live2 = FadeGradient(color2);
            _dead  = FlatGradient(UiTheme.Instance.erased);   // dim ghost: visible "broken wall", no bloom
            _pool1.Add(line1);
            _pool2.Add(line2);
        }

        void LateUpdate()
        {
            if (game.Trails == null) return;
            Draw(_pool1, line1, game.Trails.P1Trail, _live1);
            Draw(_pool2, line2, game.Trails.P2Trail, _live2);
        }

        // Tail(0) -> head(1): transparent tail melting into the background.
        static Gradient FadeGradient(Color c)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static Gradient FlatGradient(Color c)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(c.a, 0f), new GradientAlphaKey(c.a, 1f) });
            return g;
        }

        void Draw(List<LineRenderer> pool, LineRenderer template, IReadOnlyList<TrailPoint> pts, Gradient live)
        {
            float now = game.Trails.Elapsed;
            int used = 0, runStart = 0;

            while (runStart < pts.Count)
            {
                // Grow the run while points share the first point's state (dead = deathAt passed).
                bool dead = pts[runStart].deathAt <= now;
                int runEnd = runStart + 1;
                while (runEnd < pts.Count && (pts[runEnd].deathAt <= now) == dead) runEnd++;

                var lr = Rent(pool, template, used++);
                lr.colorGradient = dead ? _dead : live;
                int n = runEnd - runStart;
                lr.positionCount = n;
                for (int i = 0; i < n; i++)
                    lr.SetPosition(i, pts[runStart + i].pos);

                runStart = runEnd;
            }

            // Hide leftovers from frames that had more runs; never destroy (pooling).
            for (int i = used; i < pool.Count; i++)
                if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
        }

        static LineRenderer Rent(List<LineRenderer> pool, LineRenderer template, int index)
        {
            if (index == pool.Count)
                pool.Add(Instantiate(template, template.transform.parent));   // inherits material/width/sorting
            var lr = pool[index];
            if (!lr.gameObject.activeSelf) lr.gameObject.SetActive(true);
            return lr;
        }
    }
}
