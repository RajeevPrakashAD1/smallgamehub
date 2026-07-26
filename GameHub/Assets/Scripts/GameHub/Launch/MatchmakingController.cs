namespace GameHub
{
    /// <summary>What tapping the primary button should do.</summary>
    public enum MatchAction { None, Cancel, Retry, Back }

    /// <summary>How the matchmaking screen should look right now. Plain data.</summary>
    public readonly struct MatchmakingPresentation
    {
        public readonly string Headline;
        public readonly string StatusLine;
        public readonly bool ShowSpinner;
        public readonly bool ShowElapsed;
        public readonly string ElapsedText;
        public readonly string PrimaryLabel;
        public readonly MatchAction PrimaryAction;

        public MatchmakingPresentation(string headline, string statusLine, bool showSpinner,
                                       bool showElapsed, string elapsedText,
                                       string primaryLabel, MatchAction primaryAction)
        {
            Headline = headline;
            StatusLine = statusLine;
            ShowSpinner = showSpinner;
            ShowElapsed = showElapsed;
            ElapsedText = elapsedText;
            PrimaryLabel = primaryLabel;
            PrimaryAction = primaryAction;
        }
    }

    /// <summary>
    /// The matchmaking screen's decision logic: phase in, presentation out. A pure static
    /// function like GamePageController.Describe — no Unity objects, no side effects, and
    /// no IMatchmaker either, so its tests need no fake at all.
    /// </summary>
    public static class MatchmakingController
    {
        /// <summary>After this long we reassure the player rather than look frozen.</summary>
        public const float LongSearchSeconds = 20f;

        public static MatchmakingPresentation Describe(
            NetKit.MatchPhase phase, string statusText, float elapsedSeconds)
        {
            string elapsed = FormatElapsed(elapsedSeconds);

            switch (phase)
            {
                case NetKit.MatchPhase.Searching:
                    string line = elapsedSeconds >= LongSearchSeconds
                        ? "Still looking — hang tight"
                        : Or(statusText, "Looking for an opponent…");
                    return new MatchmakingPresentation("FINDING A MATCH", line,
                        true, true, elapsed, "CANCEL", MatchAction.Cancel);

                case NetKit.MatchPhase.Joining:
                    return new MatchmakingPresentation("OPPONENT FOUND",
                        Or(statusText, "Connecting…"),
                        true, true, elapsed, "CANCEL", MatchAction.Cancel);

                case NetKit.MatchPhase.Ready:
                    // Terminal-but-good: the launcher takes over and loads the scene.
                    return new MatchmakingPresentation("MATCH READY",
                        Or(statusText, "Starting…"),
                        false, false, "", "", MatchAction.None);

                case NetKit.MatchPhase.Failed:
                    return new MatchmakingPresentation("NO MATCH FOUND",
                        Or(statusText, "Something went wrong"),
                        false, false, "", "RETRY", MatchAction.Retry);

                case NetKit.MatchPhase.Cancelled:
                    return new MatchmakingPresentation("SEARCH CANCELLED", "",
                        false, false, "", "BACK", MatchAction.Back);

                default:  // Idle — the screen shouldn't be up at all
                    return new MatchmakingPresentation("", "", false, false, "",
                        "BACK", MatchAction.Back);
            }
        }

        public static string FormatElapsed(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = (int)seconds;
            return $"{total / 60:0}:{total % 60:00}";
        }

        static string Or(string value, string fallback)
            => string.IsNullOrEmpty(value) ? fallback : value;
    }
}
