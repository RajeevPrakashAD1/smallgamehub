using NetKit;
using NUnit.Framework;

namespace GameHub.Tests
{
    /// <summary>
    /// Pure logic tests — the matchmaker resolves a match and never touches NGO, so there is
    /// no NetworkManager, no sockets and no frames involved.
    /// </summary>
    public class FakeMatchmakerTests
    {
        static FakeMatchmaker Make(FakeMatchmaker.Outcome outcome) =>
            new(outcome, searchSeconds: 2f, joinSeconds: 1f);

        static readonly MatchRequest Duel = new("trailtrap", 2);

        [Test]
        public void StartsIdle()
        {
            Assert.AreEqual(MatchPhase.Idle, Make(FakeMatchmaker.Outcome.SucceedAsHost).Phase);
        }

        [Test]
        public void RunsSearchingThenJoiningThenReady()
        {
            var mm = Make(FakeMatchmaker.Outcome.SucceedAsHost);
            mm.BeginSearch(Duel);
            Assert.AreEqual(MatchPhase.Searching, mm.Phase);

            mm.Tick(2f);
            Assert.AreEqual(MatchPhase.Joining, mm.Phase, "Joining must be reachable, not skipped");

            mm.Tick(1f);
            Assert.AreEqual(MatchPhase.Ready, mm.Phase);
        }

        [Test]
        public void HostTakesSeatZero_ClientTakesSeatOne()
        {
            var host = Make(FakeMatchmaker.Outcome.SucceedAsHost);
            host.BeginSearch(Duel);
            host.Tick(2f); host.Tick(1f);

            var client = Make(FakeMatchmaker.Outcome.SucceedAsClient);
            client.BeginSearch(Duel);
            client.Tick(2f); client.Tick(1f);

            Assert.AreEqual(MatchRole.Host, host.Result.Role);
            Assert.AreEqual(0, host.Result.Seat);
            Assert.AreEqual(MatchRole.Client, client.Result.Role);
            Assert.AreEqual(1, client.Result.Seat);
            Assert.AreEqual(2, host.Result.PlayerCount);
        }

        [Test]
        public void Fail_LandsOnFailedWithAReason()
        {
            var mm = Make(FakeMatchmaker.Outcome.Fail);
            mm.BeginSearch(Duel);
            mm.Tick(2f);

            Assert.AreEqual(MatchPhase.Failed, mm.Phase);
            Assert.IsFalse(mm.Result.Success);
            Assert.IsNotEmpty(mm.Result.FailureReason);
        }

        [Test]
        public void NeverFind_KeepsSearching()
        {
            var mm = Make(FakeMatchmaker.Outcome.NeverFind);
            mm.BeginSearch(Duel);
            mm.Tick(60f);

            Assert.AreEqual(MatchPhase.Searching, mm.Phase);
            Assert.AreEqual(60f, mm.ElapsedSeconds, 0.001f);
        }

        [Test]
        public void Cancel_MidSearch_LandsOnCancelled()
        {
            var mm = Make(FakeMatchmaker.Outcome.NeverFind);
            mm.BeginSearch(Duel);
            mm.Tick(1f);
            mm.Cancel();

            Assert.AreEqual(MatchPhase.Cancelled, mm.Phase);
        }

        [Test]
        public void Cancel_AfterReady_IsIgnored()
        {
            var mm = Make(FakeMatchmaker.Outcome.SucceedAsHost);
            mm.BeginSearch(Duel);
            mm.Tick(2f); mm.Tick(1f);
            mm.Cancel();

            Assert.AreEqual(MatchPhase.Ready, mm.Phase,
                "the match already started — cancelling would strand a live session");
        }

        [Test]
        public void BeginSearch_WhileSearching_IsIgnored()
        {
            var mm = Make(FakeMatchmaker.Outcome.NeverFind);
            mm.BeginSearch(Duel);
            mm.Tick(5f);
            mm.BeginSearch(Duel);

            Assert.AreEqual(5f, mm.ElapsedSeconds, 0.001f, "a second search must not reset the clock");
        }

        [Test]
        public void CanRetryAfterFailure()
        {
            var mm = Make(FakeMatchmaker.Outcome.Fail);
            mm.BeginSearch(Duel);
            mm.Tick(2f);
            Assert.AreEqual(MatchPhase.Failed, mm.Phase);

            mm.BeginSearch(Duel);
            Assert.AreEqual(MatchPhase.Searching, mm.Phase);
            Assert.AreEqual(0f, mm.ElapsedSeconds, 0.001f, "retry restarts the clock");
        }

        [Test]
        public void PhaseChanged_FiresOnEveryTransition()
        {
            var mm = Make(FakeMatchmaker.Outcome.SucceedAsHost);
            int fired = 0;
            mm.PhaseChanged += _ => fired++;

            mm.BeginSearch(Duel);
            mm.Tick(2f);
            mm.Tick(1f);

            Assert.AreEqual(3, fired, "Searching, Joining, Ready");
        }
    }
}
