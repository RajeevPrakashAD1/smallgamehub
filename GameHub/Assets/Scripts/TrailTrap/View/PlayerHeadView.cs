using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Pure VIEW: turns a player's SpriteRenderer into a glowing round head. Reuses the
    /// existing renderer (no extra objects), the trails' glow material (HDR -> Bloom), and
    /// the player's UiTheme colour boosted past 1.0 so it blooms. Colour is picked by a
    /// fixed player index, so host and client agree with no networking.
    /// </summary>
    public sealed class PlayerHeadView : MonoBehaviour
    {
        [SerializeField] int player = 1;             // 1 -> UiTheme.p1, 2 -> p2
        [SerializeField] Material glowMaterial;      // Sprite-Unlit (shared with trails)
        [SerializeField] float diameter = 0.7f;      // world units
        [SerializeField] float hdrBoost = 2.2f;      // >1 so Bloom catches the head

        void Awake()
        {
            var sr = GetComponent<SpriteRenderer>();
            sr.sprite = ProcSprites.MakeDot(64, 64f / diameter);
            if (glowMaterial != null) sr.sharedMaterial = glowMaterial;

            Color c = player == 2 ? UiTheme.Instance.p2 : UiTheme.Instance.p1;
            sr.color = new Color(c.r * hdrBoost, c.g * hdrBoost, c.b * hdrBoost, 1f);
            sr.sortingOrder = 15;                    // above trails/pickups, below particles
        }
    }
}
