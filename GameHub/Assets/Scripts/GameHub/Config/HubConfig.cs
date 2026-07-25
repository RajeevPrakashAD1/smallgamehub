using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GameHub
{
    /// <summary>
    /// The whole hub as one tunable asset: branding, theme, storage budget, and the
    /// catalogue of games. One instance lives in Resources/; the composition root
    /// (HubBootstrap, Feature 2) loads it and hands the pieces to the systems that need them.
    /// </summary>
    [CreateAssetMenu(menuName = "GameHub/Hub Config", fileName = "HubConfig")]
    public sealed class HubConfig : ScriptableObject
    {
        [Header("Branding")]
        [Tooltip("App name shown on the home screen.")]
        public string appName = "GameHub";

        [Header("Theme")]
        [Tooltip("Accent colour for buttons, highlights, active states.")]
        public Color themePrimary = new(0f, 1f, 1f);          // cyan, matches Trail Trap p1

        [Tooltip("Background colour behind the hub UI.")]
        public Color themeBg = new(0.024f, 0.039f, 0.110f);   // #060A1C deep-space navy

        [Tooltip("Font used across the hub UI (TextMeshPro SDF asset).")]
        public TMP_FontAsset font;

        [Header("Storage")]
        [Tooltip("Soft cap on downloaded game content (MB). Feature 7 uses it for eviction.")]
        public int storageBudgetMB = 512;

        [Header("Catalogue")]
        [Tooltip("Games shown in this hub. MVP: the catalogue is just this list.")]
        public List<HubGameManifest> games = new();
    }
}
