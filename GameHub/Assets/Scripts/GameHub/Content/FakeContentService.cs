using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameHub
{
    /// <summary>
    /// In-memory IContentService for tests and for building the download UX before any
    /// bundles exist. Time is injected via Tick, so tests advance it instantly and the
    /// editor advances it with Time.deltaTime — no coroutines, no frames required.
    /// </summary>
    public sealed class FakeContentService : IContentService
    {
        sealed class Entry
        {
            public ContentState state = ContentState.Unknown;
            public long sizeBytes;
            public float progress;
            public float remaining;    // seconds left on a running download
        }

        readonly Dictionary<string, Entry> _entries = new();

        public event Action<string> Changed;

        [Tooltip("Seconds a fake download takes.")]
        public float downloadSeconds = 3f;

        /// <summary>Set true to make the next Download fail — the branch you can't trigger for real.</summary>
        public bool failNextDownload;

        /// <summary>Bytes reported for a game that isn't downloaded yet.</summary>
        public long fakeSizeBytes = 12L * 1024 * 1024;

        Entry Get(HubGameManifest game)
        {
            if (game == null || string.IsNullOrEmpty(game.id)) return null;
            if (!_entries.TryGetValue(game.id, out var e))
                _entries[game.id] = e = new Entry();
            return e;
        }

        public ContentState StateOf(HubGameManifest game)
            => Get(game)?.state ?? ContentState.Unknown;

        public long DownloadSizeBytes(HubGameManifest game)
            => Get(game)?.sizeBytes ?? 0L;

        public float Progress(HubGameManifest game)
            => Get(game)?.progress ?? 0f;

        void SetState(HubGameManifest game, ContentState state)
        {
            var e = Get(game);
            if (e == null || e.state == state) return;
            e.state = state;
            Changed?.Invoke(game.id);
        }

        public void Refresh(HubGameManifest game)
        {
            var e = Get(game);
            if (e == null || e.state == ContentState.Downloading) return;

            if (e.state == ContentState.Ready)
            {
                e.sizeBytes = 0L;
                return;
            }

            e.sizeBytes = fakeSizeBytes;
            SetState(game, ContentState.NotDownloaded);
        }

        public void Download(HubGameManifest game)
        {
            var e = Get(game);
            if (e == null || e.state == ContentState.Downloading || e.state == ContentState.Ready)
                return;

            e.progress = 0f;
            e.remaining = Mathf.Max(0f, downloadSeconds);
            SetState(game, ContentState.Downloading);

            if (e.remaining <= 0f) Finish(game, e);
        }

        public void Free(HubGameManifest game)
        {
            var e = Get(game);
            if (e == null) return;

            e.progress = 0f;
            e.remaining = 0f;
            e.sizeBytes = fakeSizeBytes;
            SetState(game, ContentState.NotDownloaded);
        }

        /// <summary>Advance fake downloads. Tests pass a big dt; the editor passes Time.deltaTime.</summary>
        public void Tick(float dt)
        {
            // Copy the keys: finishing a download raises Changed, and a listener may
            // Refresh/Free and mutate the dictionary while we're walking it.
            if (_entries.Count == 0) return;
            _keys.Clear();
            foreach (var kv in _entries)
                if (kv.Value.state == ContentState.Downloading)
                    _keys.Add(kv.Key);

            foreach (var id in _keys)
            {
                var e = _entries[id];
                e.remaining -= dt;
                if (e.remaining > 0f)
                {
                    e.progress = 1f - Mathf.Clamp01(e.remaining / Mathf.Max(0.0001f, downloadSeconds));
                    continue;
                }
                FinishById(id, e);
            }
        }

        readonly List<string> _keys = new();

        void Finish(HubGameManifest game, Entry e) => FinishById(game.id, e);

        void FinishById(string id, Entry e)
        {
            e.remaining = 0f;

            if (failNextDownload)
            {
                failNextDownload = false;
                e.progress = 0f;
                e.state = ContentState.Failed;
            }
            else
            {
                e.progress = 1f;
                e.sizeBytes = 0L;
                e.state = ContentState.Ready;
            }

            Changed?.Invoke(id);
        }
    }
}
