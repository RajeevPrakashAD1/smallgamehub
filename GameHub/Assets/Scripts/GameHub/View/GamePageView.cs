using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameHub
{
    /// <summary>
    /// The game page: one game's cover, tagline and actions. Parameterized — it re-reads
    /// HubFlow.CurrentGameId on every show. All button/label decisions come from
    /// GamePageController.Describe; this file only applies them.
    /// </summary>
    public sealed class GamePageView : MonoBehaviour, IHubView
    {
        const float Margin = 40f;

        HubBootstrap _hub;
        Canvas _canvas;
        Image _cover;
        TMP_Text _title;
        TMP_Text _tagline;
        TMP_Text _status;
        Button _primary;
        TMP_Text _primaryLabel;
        Button _playOnline;
        Button _remove;
        RectTransform _progressTrack;
        RectTransform _progressFill;

        PageAction _action;        // what the primary button does right now
        bool _pollProgress;        // true only while a download is running

        // ---- lifecycle ----------------------------------------------------------------

        public void Init(HubBootstrap hub)
        {
            _hub = hub;
            BuildUi();

            _hub.Flow.StateChanged += OnStateChanged;
            _hub.Content.Changed += OnContentChanged;
            OnStateChanged(_hub.Flow.State);
        }

        void OnDestroy()
        {
            if (_hub == null) return;
            _hub.Flow.StateChanged -= OnStateChanged;
            _hub.Content.Changed -= OnContentChanged;
        }

        void Update()
        {
            // Progress moves every frame; state transitions arrive via OnContentChanged.
            if (_pollProgress) Apply();
        }

        // ---- reacting -----------------------------------------------------------------

        void OnStateChanged(HubState state)
        {
            bool show = state == HubState.GamePage;
            _canvas.gameObject.SetActive(show);
            _pollProgress = false;

            if (!show) return;

            var m = _hub.Catalogue.ById(_hub.Flow.CurrentGameId);
            if (m == null)
            {
                Debug.LogWarning($"GamePageView: no game with id '{_hub.Flow.CurrentGameId}'.");
                _hub.Flow.GoHome();
                return;
            }

            _title.text = m.title;
            _tagline.text = m.tagline;
            _cover.sprite = m.coverCard;
            _cover.color = m.coverCard != null
                ? Color.white
                : new Color(0.10f, 0.14f, 0.26f, 1f);

            _hub.Content.Refresh(m);   // async size query; resolves through Changed
            Apply();
        }

        void OnContentChanged(string id)
        {
            if (id == _hub.Flow.CurrentGameId) Apply();
        }

        /// <summary>Make the screen match what the controller says it should look like.</summary>
        void Apply()
        {
            var m = _hub.Catalogue.ById(_hub.Flow.CurrentGameId);
            if (m == null) return;

            var p = GamePageController.Describe(m, _hub.Content);

            _primaryLabel.text = p.PrimaryLabel;
            _primary.interactable = p.PrimaryAction != PageAction.None;
            _action = p.PrimaryAction;

            _status.text = p.StatusLine;
            _playOnline.gameObject.SetActive(p.ShowPlayOnline);
            _remove.gameObject.SetActive(p.CanFree);

            _progressTrack.gameObject.SetActive(p.ShowProgress);
            _progressFill.anchorMax = new Vector2(Mathf.Clamp01(p.Progress), 1f);

            _pollProgress = p.ShowProgress;
        }

        void OnPrimaryClicked()
        {
            var m = _hub.Catalogue.ById(_hub.Flow.CurrentGameId);
            if (m == null) return;

            switch (_action)
            {
                case PageAction.Download:
                case PageAction.Retry:
                    _hub.Content.Download(m);
                    break;

                case PageAction.Play:
                    _hub.Launcher.Launch(m, GameMode.Solo);
                    break;
            }
        }

        void OnPlayOnlineClicked()
        {
            var m = _hub.Catalogue.ById(_hub.Flow.CurrentGameId);
            if (m != null) _hub.Launcher.Launch(m, GameMode.Multiplayer);
        }

        void OnRemoveClicked()
        {
            var m = _hub.Catalogue.ById(_hub.Flow.CurrentGameId);
            if (m != null) _hub.Content.Free(m);
        }

        // ---- construction (runs once) --------------------------------------------------

        void BuildUi()
        {
            var cfg = _hub.Config;

            _canvas = HubUi.MakeCanvas("GamePage Canvas", sortingOrder: 20);
            _canvas.transform.SetParent(transform, worldPositionStays: false);

            var bg = HubUi.MakePanel("Background", _canvas.transform, cfg.themeBg);
            HubUi.Stretch(bg.rectTransform);

            var back = HubUi.MakeButton("Back", _canvas.transform, Color.clear);
            var backRt = (RectTransform)back.transform;
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
            backRt.pivot = new Vector2(0f, 1f);
            backRt.sizeDelta = new Vector2(160f, 100f);
            backRt.anchoredPosition = new Vector2(Margin, -Margin);
            var backLabel = HubUi.MakeText("Label", back.transform, cfg.font, 56f, cfg.themePrimary);
            backLabel.text = "←";
            HubUi.Stretch(backLabel.rectTransform);
            back.onClick.AddListener(() => _hub.Flow.GoHome());

            _cover = HubUi.MakePanel("Cover", _canvas.transform, Color.white);
            var coverRt = _cover.rectTransform;
            coverRt.anchorMin = new Vector2(0f, 0.52f);
            coverRt.anchorMax = new Vector2(1f, 1f);
            coverRt.offsetMin = new Vector2(Margin, 0f);
            coverRt.offsetMax = new Vector2(-Margin, -(Margin + 100f));
            _cover.preserveAspect = true;
            _cover.raycastTarget = false;

            _title = HubUi.MakeText("Title", _canvas.transform, cfg.font, 80f, Color.white);
            var titleRt = _title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0.42f);
            titleRt.anchorMax = new Vector2(1f, 0.52f);
            titleRt.offsetMin = new Vector2(Margin, 0f);
            titleRt.offsetMax = new Vector2(-Margin, 0f);

            _tagline = HubUi.MakeText("Tagline", _canvas.transform, cfg.font, 38f,
                                      new Color(0.66f, 0.72f, 0.85f, 1f));
            var tagRt = _tagline.rectTransform;
            tagRt.anchorMin = new Vector2(0f, 0.34f);
            tagRt.anchorMax = new Vector2(1f, 0.42f);
            tagRt.offsetMin = new Vector2(Margin, 0f);
            tagRt.offsetMax = new Vector2(-Margin, 0f);
            _tagline.alignment = TextAlignmentOptions.Top;

            BuildActions(cfg);
        }

        void BuildActions(HubConfig cfg)
        {
            float width = 1080f - 2f * Margin;

            // Progress bar: a dark track with a fill whose right anchor IS the progress.
            var track = HubUi.MakePanel("ProgressTrack", _canvas.transform,
                                        new Color(0.10f, 0.14f, 0.26f, 1f));
            _progressTrack = track.rectTransform;
            _progressTrack.anchorMin = _progressTrack.anchorMax = new Vector2(0.5f, 0f);
            _progressTrack.pivot = new Vector2(0.5f, 0f);
            _progressTrack.sizeDelta = new Vector2(width, 16f);
            _progressTrack.anchoredPosition = new Vector2(0f, 600f);
            track.raycastTarget = false;

            var fill = HubUi.MakePanel("ProgressFill", track.transform, cfg.themePrimary);
            _progressFill = fill.rectTransform;
            _progressFill.anchorMin = Vector2.zero;
            _progressFill.anchorMax = new Vector2(0f, 1f);
            _progressFill.offsetMin = Vector2.zero;
            _progressFill.offsetMax = Vector2.zero;
            fill.raycastTarget = false;
            track.gameObject.SetActive(false);

            _status = HubUi.MakeText("Status", _canvas.transform, cfg.font, 32f,
                                     new Color(0.55f, 0.60f, 0.72f, 1f));
            var statusRt = _status.rectTransform;
            statusRt.anchorMin = statusRt.anchorMax = new Vector2(0.5f, 0f);
            statusRt.pivot = new Vector2(0.5f, 0f);
            statusRt.sizeDelta = new Vector2(width, 50f);
            statusRt.anchoredPosition = new Vector2(0f, 540f);

            _primary = HubUi.MakeButton("Primary", _canvas.transform, cfg.themePrimary);
            var primaryRt = (RectTransform)_primary.transform;
            primaryRt.anchorMin = primaryRt.anchorMax = new Vector2(0.5f, 0f);
            primaryRt.pivot = new Vector2(0.5f, 0f);
            primaryRt.sizeDelta = new Vector2(width, 160f);
            primaryRt.anchoredPosition = new Vector2(0f, 350f);
            _primaryLabel = HubUi.MakeText("Label", _primary.transform, cfg.font, 60f, cfg.themeBg);
            HubUi.Stretch(_primaryLabel.rectTransform);
            _primary.onClick.AddListener(OnPrimaryClicked);

            // Secondary styling (dark fill, themed text) so PLAY stays the obvious default.
            _playOnline = HubUi.MakeButton("PlayOnline", _canvas.transform,
                                           new Color(0.10f, 0.14f, 0.26f, 1f));
            var onlineRt = (RectTransform)_playOnline.transform;
            onlineRt.anchorMin = onlineRt.anchorMax = new Vector2(0.5f, 0f);
            onlineRt.pivot = new Vector2(0.5f, 0f);
            onlineRt.sizeDelta = new Vector2(width, 130f);
            onlineRt.anchoredPosition = new Vector2(0f, 200f);
            var onlineLabel = HubUi.MakeText("Label", _playOnline.transform, cfg.font, 46f,
                                             cfg.themePrimary);
            onlineLabel.text = "PLAY ONLINE";
            HubUi.Stretch(onlineLabel.rectTransform);
            _playOnline.onClick.AddListener(OnPlayOnlineClicked);
            _playOnline.gameObject.SetActive(false);

            _remove = HubUi.MakeButton("Remove", _canvas.transform, Color.clear);
            var removeRt = (RectTransform)_remove.transform;
            removeRt.anchorMin = removeRt.anchorMax = new Vector2(0.5f, 0f);
            removeRt.pivot = new Vector2(0.5f, 0f);
            removeRt.sizeDelta = new Vector2(width, 90f);
            removeRt.anchoredPosition = new Vector2(0f, 90f);
            var removeLabel = HubUi.MakeText("Label", _remove.transform, cfg.font, 34f,
                                             new Color(0.55f, 0.60f, 0.72f, 1f));
            removeLabel.text = "Remove data";
            HubUi.Stretch(removeLabel.rectTransform);
            _remove.onClick.AddListener(OnRemoveClicked);
        }
    }
}
