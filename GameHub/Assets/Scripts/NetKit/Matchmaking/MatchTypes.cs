namespace NetKit
{
    /// <summary>Which side of the session you got. The host also runs the server.</summary>
    public enum MatchRole { Host, Client }

    /// <summary>Lifecycle of a single matchmaking attempt. One value at a time.</summary>
    public enum MatchPhase { Idle, Searching, Joining, Ready, Failed, Cancelled }

    /// <summary>What the caller asks for. Everything the matchmaker needs, nothing more.</summary>
    public readonly struct MatchRequest
    {
        public readonly string GameId;
        public readonly int PlayersPerMatch;

        public MatchRequest(string gameId, int playersPerMatch)
        {
            GameId = gameId;
            PlayersPerMatch = playersPerMatch;
        }
    }

    /// <summary>
    /// The outcome of a matchmaking attempt. Valid once MatchPhase reaches Ready; on any
    /// other phase only Success and FailureReason are meaningful.
    /// </summary>
    public readonly struct MatchResult
    {
        public readonly bool Success;
        public readonly MatchRole Role;

        /// <summary>Deterministic 0..PlayerCount-1 index a game uses to pick colours and
        /// spawn points. Assigned by the host — NOT the NGO clientId, which is transport-
        /// assigned and differs between runs.</summary>
        public readonly int Seat;

        public readonly int PlayerCount;
        public readonly string FailureReason;

        MatchResult(bool success, MatchRole role, int seat, int playerCount, string failureReason)
        {
            Success = success;
            Role = role;
            Seat = seat;
            PlayerCount = playerCount;
            FailureReason = failureReason;
        }

        public static MatchResult Ready(MatchRole role, int seat, int playerCount)
            => new(success: true, role, seat, playerCount, null);

        public static MatchResult Failed(string reason)
            => new(false, MatchRole.Client, 0, 0, reason);
    }
}
