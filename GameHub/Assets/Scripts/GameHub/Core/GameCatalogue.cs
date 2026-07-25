using System.Collections.Generic;

namespace GameHub
{
    /// <summary>
    /// Read-only view over the hub's games with id lookup. A plain class (no Unity
    /// base type) so it can be unit-tested with a hand-made list — the DI seam is the
    /// constructor, exactly like GameManager.Configure takes its dependencies.
    /// </summary>
    public sealed class GameCatalogue
    {
        readonly List<HubGameManifest> _games;

        public GameCatalogue(IEnumerable<HubGameManifest> games)
        {
            _games = games != null
                ? new List<HubGameManifest>(games)
                : new List<HubGameManifest>();
        }

        public IReadOnlyList<HubGameManifest> Games => _games;

        /// <summary>The game with this id, or null if there's no match (null/empty id → null).</summary>
        public HubGameManifest ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var g in _games)
                if (g != null && g.id == id)
                    return g;
            return null;
        }
    }
}
