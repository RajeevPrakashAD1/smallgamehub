using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameHub
{
    /// <summary>
    /// The game page: one game's cover, tagline and actions. Unlike Home it is
    /// parameterized — it re-reads HubFlow.CurrentGameId every time it opens.
    /// </summary>
    public sealed class GamePageView : MonoBehaviour, IHubView
    {
        const float Margin = 40f;

        HubBootstrap _hub;
        Canvas _canvas;
        Image _cover;
        TMP_Text _title;
        TMP_Text _tagline;
        Button _primary;
        TMP_Text _primaryLabel;
        Button _remove;

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

        void OnStateChanged(HubState state)
        {
            bool show = state == HubState.GamePage;
            _canvas.gameObject.SetActive(show);
            if (show) Render(_hub.Catalogue.ById(_hub.Flow.CurrentGameId));
        }

        void Render(HubGameManifest m)
        {
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

            // Feature 4 replaces this with real download state.
            SetPrimary(m.supportsSolo || m.supportsMultiplayer ? "PLAY" : "COMING SOON", null);
            _remove.gameObject.SetActive(false);
        }

        /// <summary>Feature 4 drives the primary button through here (PLAY / DOWNLOAD / …).</summary>
        public void SetPrimary(string label, Action onClick)
        {
            _primaryLabel.text = label;
            _primary.onClick.RemoveAllListeners();
            if (onClick != null) _primary.onClick.AddListener(() => onClick());
            _primary.interactable = onClick != null;
        }

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
            tagRt.anchorMin = new Vector2(0f, 0.30f);
            tagRt.anchorMax = new Vector2(1f, 0.42f);
            tagRt.offsetMin = new Vector2(Margin, 0f);
            tagRt.offsetMax = new Vector2(-Margin, 0f);
            _tagline.alignment = TextAlignmentOptions.Top;

            // Actions sit low: bottom third is where a thumb reaches on a phone.
            _primary = HubUi.MakeButton("Primary", _canvas.transform, cfg.themePrimary);
            var primaryRt = (RectTransform)_primary.transform;
            primaryRt.anchorMin = primaryRt.anchorMax = new Vector2(0.5f, 0f);
            primaryRt.pivot = new Vector2(0.5f, 0f);
            primaryRt.sizeDelta = new Vector2(RectWidth(), 160f);
            primaryRt.anchoredPosition = new Vector2(0f, 260f);
            _primaryLabel = HubUi.MakeText("Label", _primary.transform, cfg.font, 60f, cfg.themeBg);
            HubUi.Stretch(_primaryLabel.rectTransform);

            _remove = HubUi.MakeButton("Remove", _canvas.transform, Color.clear);
            var removeRt = (RectTransform)_remove.transform;
            removeRt.anchorMin = removeRt.anchorMax = new Vector2(0.5f, 0f);
            removeRt.pivot = new Vector2(0.5f, 0f);
            removeRt.sizeDelta = new Vector2(RectWidth(), 90f);
            removeRt.anchoredPosition = new Vector2(0f, 140f);
            var removeLabel = HubUi.MakeText("Label", _remove.transform, cfg.font, 34f,
                                             new Color(0.55f, 0.60f, 0.72f, 1f));
            removeLabel.text = "Remove data";
            HubUi.Stretch(removeLabel.rectTransform);
        }

        static float RectWidth() => 1080f - 2f * Margin;
    }
}
