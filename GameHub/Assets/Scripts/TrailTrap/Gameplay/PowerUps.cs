using UnityEngine;

namespace TrailTrap
{
    // v1 has one type; more get added to this enum as we build them (Phase, Gap, Eraser).
    public enum PowerUpType { Boost }

    /// <summary>A pickup sitting on the board, waiting to be collected.</summary>
    public struct PowerUp
    {
        public Vector2 pos;
        public PowerUpType type;
    }

    /// <summary>Per-player timed effects. Each field is seconds remaining; Tick drains them.</summary>
    public struct ActiveEffects
    {
        public float boost;

        public bool BoostActive => boost > 0f;

        public void Tick(float dt) => boost = Mathf.Max(0f, boost - dt);
    }
}
