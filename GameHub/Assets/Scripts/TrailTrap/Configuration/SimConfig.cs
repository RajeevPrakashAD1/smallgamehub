using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// All tunable gameplay numbers for Trail Trap, stored as a single asset so they can be
    /// balanced in the Inspector without touching code. Grows as we add systems (trails,
    /// collision, power-ups...). Authored in human-friendly units (e.g. degrees) where noted.
    /// </summary>
    [CreateAssetMenu(menuName = "TrailTrap/Sim Config", fileName = "SimConfig")]
    public sealed class SimConfig : ScriptableObject
    {
        [Header("Movement")]
        [Tooltip("Constant forward speed in world units per second.")]
        public float baseSpeed = 5f;

        [Tooltip("How fast the heading can turn, in degrees per second (authored in degrees, " +
                 "converted to radians in code).")]
        public float turnRateDeg = 180f;

        [Header("Trail")]
        [Tooltip("How long a freshly-laid point lives, early in the match (seconds).")]
        public float fadeStart = 4f;

        [Tooltip("How long a freshly-laid point lives, late in the match (seconds).")]
        public float fadeEnd = 9f;

        [Tooltip("Seconds over which point lifespan ramps from fadeStart up to fadeEnd.")]
        public float fadeRampDuration = 90f;

        [Tooltip("Trail thickness in world units (also the LineRenderer width).")]
        public float trailWidth = 0.2f;

        [Header("Collision")]
        [Tooltip("Forgiveness added to trailWidth/2 — bigger = easier to dodge ('close but not cheap').")]
        public float grace = 0.1f;

        [Tooltip("How much of your own newest trail you can't crash into (stops tight turns self-killing).")]
        public float selfImmuneSeconds = 0.3f;

        [Tooltip("Half-size of the arena rectangle (world units). Head outside this = death.")]
        public Vector2 arenaHalfSize = new Vector2(10f, 10f);

        [Header("Match")]
        [Tooltip("Length of the '3..2..1..GO' countdown before the duel starts (seconds).")]
        public float countdownSeconds = 3f;
    }
}
