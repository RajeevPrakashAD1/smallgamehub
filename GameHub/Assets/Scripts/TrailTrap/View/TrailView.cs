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

        // LateUpdate: draw after the sim/visuals have moved this frame.
        void LateUpdate()
        {
            if (game.Trails == null) return;   // sim not started yet (before GameManager.Start)
            Draw(line1, game.Trails.P1Trail);
            Draw(line2, game.Trails.P2Trail);
        }

        static void Draw(LineRenderer lr, IReadOnlyList<TrailPoint> pts)
        {
            lr.positionCount = pts.Count;                 // match the ribbon length to live points
            for (int i = 0; i < pts.Count; i++)
                lr.SetPosition(i, pts[i].pos);            // Vector2 -> Vector3 (z = 0)
        }
    }
}
