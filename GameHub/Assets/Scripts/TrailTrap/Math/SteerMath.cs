using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Aim-to-steer math, shared by every pointing device (mouse, joystick). Converts an
    /// "absolute" desired direction into the "relative" turn axis MovementStep wants.
    /// Pure + static so it's reusable and unit-testable.
    /// </summary>
    public static class SteerMath
    {
        /// <summary>
        /// Turn axis in [-1,1] that rotates <paramref name="headingRad"/> toward
        /// <paramref name="desiredDir"/>. Full turn when far off; eases toward 0 within
        /// <paramref name="smoothBandRad"/> of the target so it settles without overshoot/jitter.
        /// </summary>
        public static float TurnToward(float headingRad, Vector2 desiredDir, float smoothBandRad)
        {
            if (desiredDir.sqrMagnitude < 1e-6f) return 0f;       // no input → go straight

            Vector2 fwd = new Vector2(Mathf.Cos(headingRad), Mathf.Sin(headingRad));
            Vector2 des = desiredDir.normalized;

            float cross = fwd.x * des.y - fwd.y * des.x;          // + = target is to my left (CCW)
            float dot   = fwd.x * des.x + fwd.y * des.y;          // + = target is ahead
            float err   = Mathf.Atan2(cross, dot);                // signed angle to target, radians

            return Mathf.Clamp(err / smoothBandRad, -1f, 1f);
        }
    }
}
