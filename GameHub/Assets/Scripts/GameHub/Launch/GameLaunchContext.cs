using System;

namespace GameHub
{
    /// <summary>How a game was launched.</summary>
    public enum GameMode { Solo, Multiplayer }

    /// <summary>
    /// What the hub hands a game at launch. Plain data plus one callback — the game's
    /// only way to talk back, so it never needs a reference to any hub type.
    /// </summary>
    public sealed class GameLaunchContext
    {
        public string GameId { get; }
        public GameMode Mode { get; }

        /// <summary>Host or client in the session the hub already started. Solo is always Host.</summary>
        public NetKit.MatchRole Role { get; }

        /// <summary>This player's 0-based index in the match — use it to pick spawn points and
        /// colours. Stable for the match; NOT the NGO clientId, which is transport-assigned.</summary>
        public int Seat { get; }

        /// <summary>Seats in this match. 1 when Solo.</summary>
        public int PlayerCount { get; }

        /// <summary>The game calls this when the player is done. The hub unloads it.</summary>
        public Action ExitToHub { get; }

        public GameLaunchContext(string gameId, GameMode mode, Action exitToHub,
                                 NetKit.MatchRole role = NetKit.MatchRole.Host,
                                 int seat = 0, int playerCount = 1)
        {
            GameId = gameId;
            Mode = mode;
            ExitToHub = exitToHub;
            Role = role;
            Seat = seat;
            PlayerCount = playerCount;
        }
    }
}
