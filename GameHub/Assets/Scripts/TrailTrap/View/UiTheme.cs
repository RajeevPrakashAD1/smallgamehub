using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// All VIEW colours in one tunable asset (the sim's SimConfig equivalent for looks).
    /// Views read <see cref="Instance"/> so palette lives in exactly one place.
    /// </summary>
    [CreateAssetMenu(menuName = "TrailTrap/UI Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        [Header("Players")]
        public Color p1 = new(0f, 1f, 1f);      // cyan
        public Color p2 = new(1f, 0f, 1f);      // magenta

        [Header("HUD")]
        public Color countdown = Color.white;
        public Color draw = new(0.8f, 0.8f, 0.8f);

        [Header("Power-ups")]
        public Color boost = new(0.4f, 1f, 0.6f);

        [Header("Arena")]
        [ColorUsage(true, true)] public Color wall = new(1.2f, 1.2f, 1.6f);  // HDR: soft blue-white glow

        static UiTheme _instance;

        // Loaded once from Assets/Resources/UiTheme.asset; falls back to code defaults if missing.
        public static UiTheme Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<UiTheme>("UiTheme");
                if (_instance == null) _instance = CreateInstance<UiTheme>();
                return _instance;
            }
        }
    }
}
