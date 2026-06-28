using NUnit.Framework;
using UnityEngine;

namespace TrailTrap.Tests
{
    public class SteerMathTests
    {
        const float Band = 15f * Mathf.Deg2Rad;   // 15° smoothing band (our default)

        [Test]
        public void Aligned_NoTurn()
        {
            // Facing right, target dead ahead → nothing to correct.
            float turn = SteerMath.TurnToward(0f, new Vector2(1f, 0f), Band);
            Assert.AreEqual(0f, turn, 1e-4f);
        }

        [Test]
        public void TargetToLeft_TurnsFullPositive()
        {
            // Facing right, target straight up = 90° to the left → clamps to +1.
            float turn = SteerMath.TurnToward(0f, new Vector2(0f, 1f), Band);
            Assert.AreEqual(1f, turn, 1e-4f);
        }

        [Test]
        public void TargetToRight_TurnsFullNegative()
        {
            // Facing right, target straight down = 90° to the right → clamps to -1.
            float turn = SteerMath.TurnToward(0f, new Vector2(0f, -1f), Band);
            Assert.AreEqual(-1f, turn, 1e-4f);
        }

        [Test]
        public void WithinBand_EasesProportionally()
        {
            // Error = half the band (7.5°) → eased to half a turn (no overshoot).
            float a = 7.5f * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float turn = SteerMath.TurnToward(0f, dir, Band);
            Assert.AreEqual(0.5f, turn, 1e-3f);
        }

        [Test]
        public void ZeroInput_NoTurn()
        {
            // No pointing direction → go straight.
            float turn = SteerMath.TurnToward(1.2f, Vector2.zero, Band);
            Assert.AreEqual(0f, turn, 1e-4f);
        }
    }
}
