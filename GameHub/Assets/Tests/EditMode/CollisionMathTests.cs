using NUnit.Framework;
using UnityEngine;

namespace TrailTrap.Tests
{
    public class CollisionMathTests
    {
        // Reusable horizontal segment A(0,0) -> B(4,0).
        static readonly Vector2 A = new Vector2(0f, 0f);
        static readonly Vector2 B = new Vector2(4f, 0f);

        // Data-driven: each row is (pointX, pointY, expectedSquaredDist) against segment A-B.
        // Shows the three clamp regimes in one place; each row is a separate row in Test Runner.
        [TestCase( 1f,  3f, 9f, TestName = "MidSegment_PerpendicularDistance")]   // shadow at (1,0)
        [TestCase( 6f,  1f, 5f, TestName = "BeyondEnd_ClampsToB")]                // clamps to B(4,0)
        [TestCase(-2f,  0f, 4f, TestName = "BeforeStart_ClampsToA")]              // clamps to A(0,0)
        public void DistSqToSegment_AlongHorizontalSegment(float px, float py, float expectedSq)
        {
            Assert.AreEqual(expectedSq, CollisionMath.DistSqToSegment(new Vector2(px, py), A, B), 1e-4f);
        }

        [Test]
        public void DegenerateSegment_MeasuresToThePoint()
        {
            // A==B guard: distance from (5,6) to (2,2) = (3,4) → squared 25.
            Vector2 p = new Vector2(2f, 2f);
            Assert.AreEqual(25f, CollisionMath.DistSqToSegment(new Vector2(5f, 6f), p, p), 1e-4f);
        }
    }
}
