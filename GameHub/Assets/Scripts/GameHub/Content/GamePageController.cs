using UnityEngine;

namespace GameHub
{
    /// <summary>What tapping the primary button should do.</summary>
    public enum PageAction { None, Play, Download, Retry }

    /// <summary>How the game page should look right now. Plain data — no Unity objects.</summary>
    public readonly struct PagePresentation
    {
        public readonly string PrimaryLabel;
        public readonly PageAction PrimaryAction;
        public readonly bool ShowProgress;
        public readonly float Progress;
        public readonly bool CanFree;
        public readonly string StatusLine;

        public PagePresentation(string primaryLabel, PageAction primaryAction,
                                bool showProgress, float progress, bool canFree, string statusLine)
        {
            PrimaryLabel = primaryLabel;
            PrimaryAction = primaryAction;
            ShowProgress = showProgress;
            Progress = progress;
            CanFree = canFree;
            StatusLine = statusLine;
        }
    }

    /// <summary>
    /// The game page's decision logic: content state in, presentation out. A pure static
    /// function like SteerMath — no Unity objects, no side effects, EditMode-testable.
    /// </summary>
    public static class GamePageController
    {
        public static PagePresentation Describe(HubGameManifest game, IContentService content)
        {
            if (game == null)
                return new PagePresentation("", PageAction.None, false, 0f, false, "");

            switch (content?.StateOf(game) ?? ContentState.Unknown)
            {
                case ContentState.Ready:
                    return new PagePresentation("PLAY", PageAction.Play,
                                                false, 0f, true, "Installed");

                case ContentState.Downloading:
                    float p = content.Progress(game);
                    return new PagePresentation("DOWNLOADING", PageAction.None,
                                                true, p, false, $"{Mathf.RoundToInt(p * 100f)}%");

                case ContentState.NotDownloaded:
                    long bytes = content.DownloadSizeBytes(game);
                    return new PagePresentation($"DOWNLOAD ({FormatBytes(bytes)})", PageAction.Download,
                                                false, 0f, false, "");

                case ContentState.Failed:
                    return new PagePresentation("RETRY", PageAction.Retry,
                                                false, 0f, false, "Download failed");

                default:  // Unknown — the size query hasn't resolved yet
                    return new PagePresentation("…", PageAction.None,
                                                false, 0f, false, "Checking…");
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0L) return "0 MB";
            if (bytes < 1024L * 1024L) return $"{Mathf.CeilToInt(bytes / 1024f)} KB";
            return $"{bytes / (1024f * 1024f):0.#} MB";
        }
    }
}
