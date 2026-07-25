using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameHub
{
    /// <summary>
    /// One game tile in the Home grid. Pure leaf view: holds no state, decides nothing —
    /// it renders whatever manifest it's handed and forwards the tap.
    /// </summary>
    public sealed class GameCardView : MonoBehaviour
    {
        Image _cover;
        TMP_Text _title;
        TMP_Text _badge;
        Button _button;

        public HubGameManifest Manifest { get; private set; }

        public static GameCardView Create(Transform parent, HubConfig config)
        {
            var go = new GameObject("GameCard", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            var card = go.AddComponent<GameCardView>();
            card.Build(config);
            return card;
        }

        void Build(HubConfig config)
        {
            var root = (RectTransform)transform;

            var bg = HubUi.MakeButton("Surface", root, new Color(0.06f, 0.09f, 0.18f, 1f));
            _button = bg;
            HubUi.Stretch((RectTransform)bg.transform);

            _cover = HubUi.MakePanel("Cover", bg.transform, Color.white);
            var coverRt = _cover.rectTransform;
            coverRt.anchorMin = new Vector2(0f, 0.32f);
            coverRt.anchorMax = Vector2.one;
            coverRt.offsetMin = new Vector2(12f, 0f);
            coverRt.offsetMax = new Vector2(-12f, -12f);
            _cover.raycastTarget = false;
            _cover.preserveAspect = true;

            _title = HubUi.MakeText("Title", bg.transform, config.font, 40f, Color.white);
            var titleRt = _title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0.12f);
            titleRt.anchorMax = new Vector2(1f, 0.32f);
            titleRt.offsetMin = new Vector2(12f, 0f);
            titleRt.offsetMax = new Vector2(-12f, 0f);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _title.overflowMode = TextOverflowModes.Ellipsis;

            _badge = HubUi.MakeText("Badge", bg.transform, config.font, 26f, config.themePrimary);
            var badgeRt = _badge.rectTransform;
            badgeRt.anchorMin = Vector2.zero;
            badgeRt.anchorMax = new Vector2(1f, 0.12f);
            badgeRt.offsetMin = new Vector2(12f, 8f);
            badgeRt.offsetMax = new Vector2(-12f, 0f);
        }

        public void Bind(HubGameManifest manifest, Action<string> onClick)
        {
            Manifest = manifest;

            _title.text = manifest.title;
            _cover.sprite = manifest.coverCard;
            _cover.color = manifest.coverCard != null
                ? Color.white
                : new Color(0.10f, 0.14f, 0.26f, 1f);

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onClick?.Invoke(manifest.id));
        }

        public void SetBadge(string text, Color color)
        {
            _badge.text = text;
            _badge.color = color;
        }
    }
}
