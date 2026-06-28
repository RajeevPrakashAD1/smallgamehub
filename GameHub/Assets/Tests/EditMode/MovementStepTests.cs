using NUnit.Framework;
using UnityEngine;

namespace TrailTrap.Tests
{
    public class MovementStepTests
    {
        SimConfig _cfg;

        [SetUp]
        public void Setup()
        {
            // SimConfig is a ScriptableObject; create one in memory for the test.
            _cfg = ScriptableObject.CreateInstance<SimConfig>();
            _cfg.baseSpeed = 5f;
            _cfg.turnRateDeg = 180f;
        }

        [TearDown]
        public void Teardown() => Object.DestroyImmediate(_cfg);

        static PlayerState Alive(float heading = 0f) =>
            new PlayerState { position = Vector2.zero, heading = heading, speed = 5f, alive = true };

        [Test]
        public void Straight_StepsForwardBySpeedTimesDt()
        {
            // speed 5 * dt 0.04 = 0.2 along +X; heading unchanged.
            var next = PlayerController.MovementStep(Alive(), new InputFrame { turn = 0f }, 0.04f, _cfg);
            Assert.AreEqual(0.2f, next.position.x, 1e-4f);
            Assert.AreEqual(0f, next.position.y, 1e-4f);
            Assert.AreEqual(0f, next.heading, 1e-4f);
        }

        [Test]
        public void FullTurn_RotatesByTurnRateTimesDt()
        {
            // turn 1 at 180°/s for 0.04s = π * 0.04 radians.
            var next = PlayerController.MovementStep(Alive(), new InputFrame { turn = 1f }, 0.04f, _cfg);
            Assert.AreEqual(Mathf.PI * 0.04f, next.heading, 1e-4f);
        }

        [Test]
        public void AnalogTurn_ScalesWithMagnitude()
        {
            // Half input → half the rotation. Confirms the analog seam works.
            var next = PlayerController.MovementStep(Alive(), new InputFrame { turn = 0.5f }, 0.04f, _cfg);
            Assert.AreEqual(Mathf.PI * 0.04f * 0.5f, next.heading, 1e-4f);
        }

        [Test]
        public void Dead_DoesNotMoveOrTurn()
        {
            var p = Alive(); p.alive = false;
            var next = PlayerController.MovementStep(p, new InputFrame { turn = 1f }, 0.04f, _cfg);
            Assert.AreEqual(p.position, next.position);
            Assert.AreEqual(p.heading, next.heading, 1e-4f);
        }
    }
}
