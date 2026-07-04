using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Pure VIEW: draws the arena's death boundary as a glowing rectangle so players can
    /// SEE the wall they die on. Built once at Start from config.arenaHalfSize — the same
    /// number CollisionStep checks, so the picture can never drift from the rule.
    /// </summary>
    public sealed class ArenaView : MonoBehaviour
    {
        [SerializeField] SimConfig config;
        [SerializeField] Material glowMaterial;   // TrailGlow.mat — unlit, HDR passes to Bloom
        [SerializeField] float width = 0.15f;

        void Start()
        {
            var lr = gameObject.AddComponent<LineRenderer>();
            lr.material = glowMaterial;
            lr.loop = true;                        // closes corner 4 back to corner 1
            lr.useWorldSpace = true;
            lr.startWidth = lr.endWidth = width;
            lr.startColor = lr.endColor = UiTheme.Instance.wall;
            lr.sortingOrder = 5;                   // above background, below pickups (10)

            Vector2 h = config.arenaHalfSize;
            lr.positionCount = 4;
            lr.SetPosition(0, new Vector3(-h.x, -h.y));
            lr.SetPosition(1, new Vector3( h.x, -h.y));
            lr.SetPosition(2, new Vector3( h.x,  h.y));
            lr.SetPosition(3, new Vector3(-h.x,  h.y));
        }
    }
}
