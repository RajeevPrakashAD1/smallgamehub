using GameHub;
using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Trail Trap's half of the hub launch contract. The hub loads this scene and calls
    /// Launch; this configures the match for the requested mode, shows a HOME button, and
    /// shuts the session down on the way out.
    /// </summary>
    public sealed class TrailTrapEntry : GameEntryPoint
    {
        [Tooltip("Dev Host/Join menu. Disabled automatically when launched from the hub.")]
        [SerializeField] NetBootstrap devMenu;

        GameObject _homeButton;

        protected override void OnLaunch()
        {
            // The hub owns session start now — the dev menu would offer a second one.
            if (devMenu != null) devMenu.enabled = false;

            if (Context.Mode == GameMode.Multiplayer && !NetKit.Session.IsRunning)
                NetKit.Session.StartHost();

            BuildHomeButton();
        }

        public override void OnExitToHub()
        {
            // Runs while the scene is still loaded, so NetworkManager is alive to shut down.
            if (NetKit.Session.IsRunning) NetKit.Session.Shutdown();
            if (_homeButton != null) Destroy(_homeButton);
        }

        void BuildHomeButton()
        {
            var canvas = HubUi.MakeCanvas("Game Exit Canvas", sortingOrder: 200);
            _homeButton = canvas.gameObject;

            var btn = HubUi.MakeButton("Home", canvas.transform, new Color(0f, 0f, 0f, 0.45f));
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(180f, 100f);
            rt.anchoredPosition = new Vector2(-30f, -30f);

            var label = HubUi.MakeText("Label", btn.transform, null, 44f, Color.white);
            label.text = "HOME";
            HubUi.Stretch(label.rectTransform);

            btn.onClick.AddListener(RequestExit);
        }
    }
}
