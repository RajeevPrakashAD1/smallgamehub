using System;

namespace NetKit
{
    /// <summary>
    /// Resolve who you are playing with. The seam that keeps the matchmaking backend
    /// (fake / Relay+Lobby / anything later) out of the hub's logic and out of every game:
    /// callers drive this and read MatchResult, so their branching is EditMode-testable with
    /// no cloud, no sockets and no frames involved.
    ///
    /// Contract: this does NOT start a session. It resolves role, seat and (later) the Relay
    /// join code, and the caller hands those to the game. The NetworkManager lives in the
    /// game's scene, which is not loaded yet while this runs — so the game opens the session
    /// itself with Session.StartAs(role) once it is up.
    /// </summary>
    public interface IMatchmaker
    {
        /// <summary>Fires on every phase transition. Views re-render on this.</summary>
        event Action<MatchPhase> PhaseChanged;

        MatchPhase Phase { get; }

        /// <summary>Short backend-supplied line for the UI ("searching…", "opponent found").
        /// Cosmetic only — never branch on it.</summary>
        string StatusText { get; }

        /// <summary>Seconds since BeginSearch. Resets on each new search.</summary>
        float ElapsedSeconds { get; }

        /// <summary>Valid once Phase == Ready. On Failed, only FailureReason is meaningful.</summary>
        MatchResult Result { get; }

        /// <summary>Start looking. Ignored unless Phase is Idle, Failed or Cancelled.</summary>
        void BeginSearch(MatchRequest request);

        /// <summary>Give up. Tears down any half-built session and lands on Cancelled.</summary>
        void Cancel();

        /// <summary>Drive in-flight work. Real impls pump async handles; the fake advances timers.</summary>
        void Tick(float dt);
    }
}
