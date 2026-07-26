using NetKit;
using NUnit.Framework;

namespace GameHub.Tests
{
    public class MatchmakingControllerTests
    {
        [Test]
        public void Searching_OffersCancelAndSpins()
        {
            var p = MatchmakingController.Describe(MatchPhase.Searching, "Looking…", 3f);
            Assert.AreEqual(MatchAction.Cancel, p.PrimaryAction);
            Assert.AreEqual("CANCEL", p.PrimaryLabel);
            Assert.IsTrue(p.ShowSpinner);
            Assert.IsTrue(p.ShowElapsed);
        }

        [Test]
        public void Searching_PastThreshold_ReassuresInsteadOfLookingFrozen()
        {
            var brief = MatchmakingController.Describe(MatchPhase.Searching, "Looking…", 1f);
            var long_ = MatchmakingController.Describe(
                MatchPhase.Searching, "Looking…", MatchmakingController.LongSearchSeconds + 1f);

            Assert.AreEqual("Looking…", brief.StatusLine);
            Assert.AreEqual("Still looking — hang tight", long_.StatusLine);
        }

        [Test]
        public void Joining_StillCancellable()
        {
            var p = MatchmakingController.Describe(MatchPhase.Joining, "Connecting…", 5f);
            Assert.AreEqual("OPPONENT FOUND", p.Headline);
            Assert.AreEqual(MatchAction.Cancel, p.PrimaryAction);
        }

        [Test]
        public void Ready_OffersNothing_LauncherTakesOver()
        {
            var p = MatchmakingController.Describe(MatchPhase.Ready, "", 8f);
            Assert.AreEqual(MatchAction.None, p.PrimaryAction,
                "cancelling a started match would strand a live session");
            Assert.IsFalse(p.ShowSpinner);
        }

        [Test]
        public void Failed_OffersRetryAndShowsTheReason()
        {
            var p = MatchmakingController.Describe(MatchPhase.Failed, "No opponent found", 30f);
            Assert.AreEqual(MatchAction.Retry, p.PrimaryAction);
            Assert.AreEqual("No opponent found", p.StatusLine);
        }

        [Test]
        public void Cancelled_OffersBackNotRetry()
        {
            var p = MatchmakingController.Describe(MatchPhase.Cancelled, "", 12f);
            Assert.AreEqual(MatchAction.Back, p.PrimaryAction,
                "the player chose to stop — offering Retry is tone-deaf");
        }

        [Test]
        public void EmptyStatusText_FallsBackRatherThanShowingBlank()
        {
            Assert.IsNotEmpty(MatchmakingController.Describe(MatchPhase.Searching, "", 0f).StatusLine);
            Assert.IsNotEmpty(MatchmakingController.Describe(MatchPhase.Failed, null, 0f).StatusLine);
        }

        [Test]
        public void FormatElapsed_IsClockShaped()
        {
            Assert.AreEqual("0:00", MatchmakingController.FormatElapsed(0f));
            Assert.AreEqual("0:07", MatchmakingController.FormatElapsed(7.9f));
            Assert.AreEqual("1:05", MatchmakingController.FormatElapsed(65f));
            Assert.AreEqual("12:34", MatchmakingController.FormatElapsed(754f));
            Assert.AreEqual("0:00", MatchmakingController.FormatElapsed(-3f));
        }
    }
}
