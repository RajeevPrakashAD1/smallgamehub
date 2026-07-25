using System;

namespace GameHub
{
    /// <summary>Where a game's downloadable content stands right now.</summary>
    public enum ContentState
    {
        Unknown,        // not asked yet — the size query is itself async
        NotDownloaded,
        Downloading,
        Ready,
        Failed
    }

    /// <summary>
    /// Download/free a game's content. The seam that keeps Addressables out of the hub's
    /// logic: GamePageController talks only to this, so its branching is EditMode-testable
    /// against a fake, with no bundles, network or frames involved.
    /// </summary>
    public interface IContentService
    {
        /// <summary>Fires with the game id whose state changed. Views re-render on this.</summary>
        event Action<string> Changed;

        ContentState StateOf(HubGameManifest game);

        /// <summary>Bytes still to fetch. 0 once Ready. Meaningful only after Refresh.</summary>
        long DownloadSizeBytes(HubGameManifest game);

        /// <summary>0..1 while Downloading; otherwise 0.</summary>
        float Progress(HubGameManifest game);

        /// <summary>Ask how big this game is. Async — resolves into Changed.</summary>
        void Refresh(HubGameManifest game);

        void Download(HubGameManifest game);

        /// <summary>Delete cached content. Returns the game to NotDownloaded.</summary>
        void Free(HubGameManifest game);

        /// <summary>Drive in-flight work. Real impls poll their handles; the fake advances timers.</summary>
        void Tick(float dt);
    }
}
