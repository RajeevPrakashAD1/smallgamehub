using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameHub
{
    /// <summary>
    /// The matchmaking screen: headline, spinner, elapsed clock and one action button.
    /// All label/button decisions come from MatchmakingController.Describe; this file only
    /// applies them and dispatches the resulting MatchAction to the launcher.
    /// </summary>
    public sealed class MatchmakingView : MonoBehaviour, IHubView
    {
        const float Margin = 40f;
        const float SpinDegreesPerSecond = 220f;

        HubBootstrap _hub;
        Canvas _canvas;
        TMP_Text _headline;
        TMP_Text _gameTitle;
        TMP_Text _status;
        TMP_Text _elapsed;
        RectTransform _spinner;
        Button _primary;
        TMP_Text _primaryLabel;

        MatchAction _action;

        // ---- lifecycle ----------------------------------------------------------------

        public void Init(HubBootstrap hub)
        {
            _hub = hub;
            BuildUi();

            _hub.Flow.StateChanged += OnStateChanged;
            OnStateChanged(_hub.Flow.State);
        }

        void OnDestroy()
        {
            if (_hub != null) _hub.Flow.StateChanged -= OnStateChanged;
        }

        void Update()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf) return;

            // The elapsed clock changes every frame, so this screen polls rather than
            // waiting for PhaseChanged — same reason GamePageView polls during a download.
            Apply();

            if (_spinner.gameObject.activeSelf)
                _spinner.Rotate(0f, 0f, -SpinDegreesPerSecond * Time.deltaTime);
        }

        // ---- reacting -----------------------------------------------------------------

        void OnStateChanged(HubState state)
        {
            bool show = state == HubState.Matchmaking;
            _canvas.gameObject.SetActive(show);
            if (!show) return;

            var m = _hub.Catalogue.ById(_hub.Flow.CurrentGameId);
            _gameTitle.text = m != null ? m.title : "";
            _spinner.localRotation = Quaternion.identity;
            Apply();
        }

        void Apply()
        {
            var mm = _hub.Matchmaker;
            if (mm == null) return;

            var p = MatchmakingController.Describe(mm.Phase, mm.StatusText, mm.ElapsedSeconds);

            _headline.text = p.Headline;
            _status.text = p.StatusLine;

            _elapsed.gameObject.SetActive(p.ShowElapsed);
            _elapsed.text = p.ElapsedText;

            _spinner.gameObject.SetActive(p.ShowSpinner);

            _action = p.PrimaryAction;
            _primary.gameObject.SetActive(p.PrimaryAction != MatchAction.None);
            _primaryLabel.text = p.PrimaryLabel;
        }

        void OnPrimaryClicked()
        {
            switch (_action)
            {
                case MatchAction.Cancel: _hub.Launcher.CancelMatchmaking(); break;
                case MatchAction.Retry:  _hub.Launcher.RetryMatchmaking();  break;
                case MatchAction.Back:   _hub.Launcher.AbandonMatchmaking(); break;
            }
        }

        // ---- construction (runs once) --------------------------------------------------

        void BuildUi()
        {
            var cfg = _hub.Config;
            float width = 1080f - 2f * Margin;

            _canvas = HubUi.MakeCanvas("Matchmaking Canvas", sortingOrder: 30);
            _canvas.transform.SetParent(transform, worldPositionStays: false);

            var bg = HubUi.MakePanel("Background", _canvas.transform, cfg.themeBg);
            HubUi.Stretch(bg.rectTransform);

            _gameTitle = HubUi.MakeText("GameTitle", _canvas.transform, cfg.font, 38f,
                                        new Color(0.55f, 0.60f, 0.72f, 1f));
            HubUi.Center(_gameTitle.rectTransform, new Vector2(0f, 520f), new Vector2(width, 60f));

            _headline = HubUi.MakeText("Headline", _canvas.transform, cfg.font, 76f, Color.white);
            HubUi.Center(_headline.rectTransform, new Vector2(0f, 420f), new Vector2(width, 110f));

            // Spinner: a themed square rotating on Z. Cheap, no sprite asset needed, and it
            // reads as "working" at any phone size.
            var spin = HubUi.MakePanel("Spinner", _canvas.transform, cfg.themePrimary);
            _spinner = spin.rectTransform;
            HubUi.Center(_spinner, new Vector2(0f, 150f), new Vector2(120f, 120f));
            spin.raycastTarget = false;

            _status = HubUi.MakeText("Status", _canvas.transform, cfg.font, 36f,
                                     new Color(0.66f, 0.72f, 0.85f, 1f));
            HubUi.Center(_status.rectTransform, new Vector2(0f, -40f), new Vector2(width, 60f));

            _elapsed = HubUi.MakeText("Elapsed", _canvas.transform, cfg.font, 44f, cfg.themePrimary);
            HubUi.Center(_elapsed.rectTransform, new Vector2(0f, -130f), new Vector2(width, 70f));

            _primary = HubUi.MakeButton("Primary", _canvas.transform, cfg.themePrimary);
            var rt = (RectTransform)_primary.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(width, 160f);
            rt.anchoredPosition = new Vector2(0f, 200f);
            _primaryLabel = HubUi.MakeText("Label", _primary.transform, cfg.font, 60f, cfg.themeBg);
            HubUi.Stretch(_primaryLabel.rectTransform);
            _primary.onClick.AddListener(OnPrimaryClicked);

            _canvas.gameObject.SetActive(false);
        }
    }
}
