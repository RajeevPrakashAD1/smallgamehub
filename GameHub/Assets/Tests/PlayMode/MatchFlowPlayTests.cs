using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TrailTrap.Tests
{
    public class MatchFlowPlayTests
    {
        // A test double (stub): always reports the same turn, so the test never needs a real
        // camera or input device. This is why we added PlayerController.SetTurnInput.
        sealed class FixedTurnInput : ITurnInput
        {
            readonly float _v;
            public FixedTurnInput(float v) => _v = v;
            public float Read(in PlayerState _) => _v;
        }

        [UnityTest]
        public IEnumerator HeadOnCollision_EndsMatchInDraw()
        {
            // Arrange — tune config so the duel resolves fast and deterministically.
            var cfg = ScriptableObject.CreateInstance<SimConfig>();
            cfg.baseSpeed = 5f;
            cfg.turnRateDeg = 180f;
            cfg.countdownSeconds = 0f;                 // skip the 3..2..1 wait
            cfg.arenaHalfSize = new Vector2(10f, 10f);

            var p1 = new GameObject("P1").AddComponent<PlayerController>();
            var p2 = new GameObject("P2").AddComponent<PlayerController>();
            p1.SetTurnInput(new FixedTurnInput(0f));   // both drive straight (no camera needed)
            p2.SetTurnInput(new FixedTurnInput(0f));

            var gm = new GameObject("GM").AddComponent<GameManager>();
            gm.Configure(cfg, p1, p2, spawnDistance: 1f);   // 1u apart, facing each other → head-on

            // Act — let the REAL FixedUpdate loop run until the match ends (or we time out).
            float timeout = 5f, elapsed = 0f;
            while (gm.MatchPhase != Phase.Over && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;                      // advance one real frame
            }

            // Assert — they met head-on; symmetric setup → a draw.
            Assert.AreEqual(Phase.Over, gm.MatchPhase, "match should have ended");
            Assert.AreEqual(0, gm.Winner, "symmetric head-on should be a draw");

            // Cleanup
            Object.Destroy(p1.gameObject);
            Object.Destroy(p2.gameObject);
            Object.Destroy(gm.gameObject);
            Object.DestroyImmediate(cfg);
        }
    }
}
