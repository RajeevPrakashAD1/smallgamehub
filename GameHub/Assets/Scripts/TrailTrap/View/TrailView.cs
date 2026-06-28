using System.Collections.Generic;
using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Pure VIEW: every frame it copies each player's trail-point list into a LineRenderer so
    /// we can see it. It owns no gameplay data — the TrailSystem (truth) does. Mirrors §2's
    /// "State is truth, transform is the picture" (§3.3).
    /// </summary>
    public sealed class TrailView : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] LineRenderer line1, line2;   // one ribbon per player

        // HDR colors: values can exceed 1, which is what lets Bloom (next step) make them glow.
        [Header("Trail colors (HDR — enables glow with Bloom)")]
        [ColorUsage(true, true)] [SerializeField] Color color1 = Color.cyan;
        [ColorUsage(true, true)] [SerializeField] Color color2 = Color.magenta;

        void Start()
        {
            // The gradient runs tail(0) -> head(1): transparent at the old tail, solid at the head.
            line1.colorGradient = FadeGradient(color1);
            line2.colorGradient = FadeGradient(color2);
        }

        // LateUpdate: draw after the sim/visuals have moved this frame.
        void LateUpdate()
        {
            if (game.Trails == null) return;   // sim not started yet (before GameManager.Start)
            Draw(line1, game.Trails.P1Trail);
            Draw(line2, game.Trails.P2Trail);
        }

        // Same hue along the whole line; alpha ramps 0 -> 1 so the tail melts into the background.
        static Gradient FadeGradient(Color c)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static void Draw(LineRenderer lr, IReadOnlyList<TrailPoint> pts)
        {
            lr.positionCount = pts.Count;                 // match the ribbon length to live points
            for (int i = 0; i < pts.Count; i++)
                lr.SetPosition(i, pts[i].pos);            // Vector2 -> Vector3 (z = 0)
        }
    }
}
