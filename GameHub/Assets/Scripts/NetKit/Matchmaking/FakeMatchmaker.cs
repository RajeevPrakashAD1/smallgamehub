using System;

namespace NetKit
{
    /// <summary>
    /// Local, deterministic IMatchmaker for building and testing the flow before any cloud
    /// backend exists. Advances on a timer instead of a network, and can be told to produce
    /// any outcome, so every UI branch is reachable from a single editor instance.
    /// Touches no NGO types at all — like the real backend, it only resolves the match.
    /// </summary>
    public sealed class FakeMatchmaker : IMatchmaker
    {
        /// <summary>Which ending this fake plays out.</summary>
        public enum Outcome { SucceedAsHost, SucceedAsClient, Fail, NeverFind }

        readonly float _searchSeconds;
        readonly float _joinSeconds;
        readonly Outcome _outcome;

        MatchRequest _request;
        float _phaseElapsed;

        public event Action<MatchPhase> PhaseChanged;

        public MatchPhase Phase { get; private set; } = MatchPhase.Idle;
        public string StatusText { get; private set; } = "";
        public float ElapsedSeconds { get; private set; }
        public MatchResult Result { get; private set; }

        public FakeMatchmaker(
            Outcome outcome = Outcome.SucceedAsHost,
            float searchSeconds = 2f,
            float joinSeconds = 0.75f)
        {
            _outcome = outcome;
            _searchSeconds = searchSeconds;
            _joinSeconds = joinSeconds;
        }

        public void BeginSearch(MatchRequest request)
        {
            if (Phase == MatchPhase.Searching || Phase == MatchPhase.Joining) return;

            _request = request;
            ElapsedSeconds = 0f;
            _phaseElapsed = 0f;
            Result = default;
            StatusText = "Looking for an opponent…";
            Set(MatchPhase.Searching);
        }

        public void Cancel()
        {
            if (Phase != MatchPhase.Searching && Phase != MatchPhase.Joining) return;

            StatusText = "Cancelled";
            Set(MatchPhase.Cancelled);
        }

        public void Tick(float dt)
        {
            if (Phase != MatchPhase.Searching && Phase != MatchPhase.Joining) return;

            ElapsedSeconds += dt;
            _phaseElapsed += dt;

            if (Phase == MatchPhase.Searching)
            {
                if (_outcome == Outcome.NeverFind) return;
                if (_phaseElapsed < _searchSeconds) return;

                if (_outcome == Outcome.Fail)
                {
                    Result = MatchResult.Failed("No opponent found");
                    StatusText = Result.FailureReason;
                    Set(MatchPhase.Failed);
                    return;
                }

                _phaseElapsed = 0f;
                StatusText = "Opponent found — connecting…";
                Set(MatchPhase.Joining);
                return;
            }

            if (_phaseElapsed < _joinSeconds) return;

            var role = _outcome == Outcome.SucceedAsHost ? MatchRole.Host : MatchRole.Client;

            // Host takes seat 0, the joiner takes seat 1. Real backends assign seats
            // host-side; with one seat per role this fake matches that for 1v1.
            Result = MatchResult.Ready(role, role == MatchRole.Host ? 0 : 1,
                                       Math.Max(1, _request.PlayersPerMatch));
            StatusText = "Ready";
            Set(MatchPhase.Ready);
        }

        void Set(MatchPhase next)
        {
            Phase = next;
            PhaseChanged?.Invoke(Phase);
        }
    }
}
