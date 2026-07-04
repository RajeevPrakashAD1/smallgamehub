using UnityEngine;

namespace TrailTrap
{
    // Types get added here as we build them (Gap deliberately skipped for v1 — GDD allows 3 of 4).
    public enum PowerUpType { Boost, Phase, Eraser }

    /// <summary>A pickup sitting on the board, waiting to be collected.</summary>
    public struct PowerUp
    {
        public Vector2 pos;
        public PowerUpType type;
    }

    /// <summary>Per-player timed effects. Each field is seconds remaining; Tick drains them.</summary>
    public struct ActiveEffects
    {
        public float boost, phase;

        public bool BoostActive => boost > 0f;
        public bool PhaseActive => phase > 0f;

        public void Tick(float dt)
        {
            boost = Mathf.Max(0f, boost - dt);
            phase = Mathf.Max(0f, phase - dt);
        }
    }
}
