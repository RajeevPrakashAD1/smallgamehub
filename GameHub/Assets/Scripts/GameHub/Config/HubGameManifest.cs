using UnityEngine;

namespace GameHub
{
    /// <summary>
    /// One asset per game: everything the hub needs to list and launch a game
    /// WITHOUT referencing the game's code. The data half of the game contract
    /// (the behaviour half is GameEntryPoint, added in Feature 5).
    /// </summary>
    [CreateAssetMenu(menuName = "GameHub/Game Manifest", fileName = "GameManifest")]
    public sealed class HubGameManifest : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique key used in code and save data. Never shown to players.")]
        public string id;

        [Tooltip("Display name on the card and game page.")]
        public string title;

        [Tooltip("One-line pitch shown under the title.")]
        public string tagline;

        [Header("Art")]
        [Tooltip("Cover image for the game card in the hub.")]
        public Sprite coverCard;

        [Header("Content (Addressables — Feature 4)")]
        [Tooltip("Addressable address of the game's entry scene, loaded when the player hits Play.")]
        public string sceneAddress;

        [Tooltip("Addressables label grouping this game's downloadable content.")]
        public string contentLabel;

        [Header("Compatibility")]
        [Tooltip("Lowest hub version that can run this game (semver string).")]
        public string minHubVersion = "1.0.0";

        [Tooltip("Can this game be played solo (no matchmaking)?")]
        public bool supportsSolo = true;

        [Tooltip("Does this game use online matchmaking?")]
        public bool supportsMultiplayer = false;

        [Tooltip("Seats in one online match. The hub matchmakes to this number — it never " +
                 "asks the game, which is how a new game gets matchmaking for free.")]
        [Min(2)] public int playersPerMatch = 2;
    }
}
