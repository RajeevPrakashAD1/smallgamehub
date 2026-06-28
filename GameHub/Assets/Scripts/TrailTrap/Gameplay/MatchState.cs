namespace TrailTrap
{
    /// <summary>The match is always in exactly one of these phases.</summary>
    public enum Phase { Countdown, Playing, Over }

    /// <summary>
    /// Tiny state machine that gates the tick and resolves the winner. Plain class (no Unity),
    /// so it's testable and the server can own it at M4.
    /// </summary>
    public sealed class MatchState
    {
        public Phase phase;
        public float countdown;   // seconds left in countdown
        public int   winner;      // 0 = none/draw, 1 = p1, 2 = p2

        // Begin (or restart) a match in the countdown phase.
        public void StartMatch(SimConfig cfg)
        {
            phase = Phase.Countdown;
            countdown = cfg.countdownSeconds;
            winner = 0;
        }

        // Runs only during Countdown: tick the timer down, then start the duel.
        public void TickCountdown(float dt)
        {
            countdown -= dt;
            if (countdown <= 0f) phase = Phase.Playing;
        }

        // Runs after CollisionStep during Playing: end the match when someone has crashed.
        public void Step(PlayerController p1, PlayerController p2)
        {
            if (phase != Phase.Playing) return;

            bool a1 = p1.State.alive, a2 = p2.State.alive;
            if (a1 && a2) return;                       // both alive → keep dueling

            phase  = Phase.Over;
            winner = (a1 == a2) ? 0 : (a1 ? 1 : 2);     // both dead → draw(0); else the survivor
        }
    }
}
