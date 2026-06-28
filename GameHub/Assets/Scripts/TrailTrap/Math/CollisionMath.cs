using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Pure collision geometry. A trail is a stripe of segments; a head crashes when its
    /// distance to a segment is within the stripe's half-width. Static + pure = testable and
    /// reusable by the server at M4.
    /// </summary>
    public static class CollisionMath
    {
        /// <summary>
        /// Squared distance from point p to the segment a-b. Squared (no sqrt) so callers can
        /// compare against r*r — cheaper, and exact for "is it within r?".
        /// </summary>
        public static float DistSqToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a, ap = p - a;
            float abLenSq = Vector2.Dot(ab, ab);             // |AB|^2

            // t = how far p's projection lands along AB, as a 0..1 fraction.
            float t = abLenSq > 1e-6f                         // guard a degenerate (A==B) segment
                      ? Vector2.Dot(ap, ab) / abLenSq
                      : 0f;

            t = Mathf.Clamp01(t);                             // pin to the real segment [A..B]
            Vector2 c = a + t * ab;                           // closest point ON the segment
            return (p - c).sqrMagnitude;
        }
    }
}
