using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameHub
{
    /// <summary>
    /// The Home screen: a grid of game cards. Shows itself when HubFlow says Home,
    /// hides otherwise. Taps report back to the flow; it decides nothing itself.
    /// </summary>
    public sealed class HomeView : MonoBehaviour, IHubView
    {
        const float RefWidth = 1080f;
        const float Margin = 40f;
        const float Gutter = 32f;
        const int Columns = 2;

        HubBootstrap _hub;
        Canvas _canvas;
        RectTransform _grid;
        readonly List<GameCardView> _cards = new();

        public void Init(HubBootstrap hub)
        {
            _hub = hub;
            BuildUi();
            Populate();

            _hub.Flow.StateChanged += OnStateChanged;
            OnStateChanged(_hub.Flow.State);
        }

        void OnDestroy()
        {
            if (_hub != null) _hub.Flow.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(HubState state)
        {
            _canvas.gameObject.SetActive(state == HubState.Home);
        }

        void Populate()
        {
            foreach (var manifest in _hub.Catalogue.Games)
            {
                if (manifest == null) continue;
                var card = GameCardView.Create(_grid, _hub.Config);
                card.Bind(manifest, id => _hub.Flow.OpenGame(id));
                card.SetBadge("READY", _hub.Config.themePrimary);
                _cards.Add(card);
            }
        }

        void BuildUi()
        {
            _canvas = HubUi.MakeCanvas("Home Canvas", sortingOrder: 10);
            _canvas.transform.SetParent(transform, worldPositionStays: false);

            var bg = HubUi.MakePanel("Background", _canvas.transform, _hub.Config.themeBg);
            HubUi.Stretch(bg.rectTransform);

            var header = HubUi.MakeText("AppName", _canvas.transform, _hub.Config.font,
                                        72f, _hub.Config.themePrimary);
            header.text = _hub.Config.appName.ToUpperInvariant();
            header.alignment = TextAlignmentOptions.Left;
            var headerRt = header.rectTransform;
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(-2f * Margin, 120f);
            headerRt.anchoredPosition = new Vector2(0f, -Margin);

            BuildScrollGrid(headerRt.sizeDelta.y + Margin * 2f);
        }

        void BuildScrollGrid(float topInset)
        {
            // Unity's scroll view is three nested objects: ScrollRect > Viewport > Content.
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(_canvas.transform, worldPositionStays: false);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(Margin, Margin);
            scrollRt.offsetMax = new Vector2(-Margin, -topInset);

            // RectMask2D clips by rectangle — no graphic and no stencil buffer needed.
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, worldPositionStays: false);
            var viewportRt = (RectTransform)viewportGo.transform;
            HubUi.Stretch(viewportRt);

            var contentGo = new GameObject("Content", typeof(RectTransform),
                                           typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, worldPositionStays: false);
            _grid = (RectTransform)contentGo.transform;
            _grid.anchorMin = new Vector2(0f, 1f);
            _grid.anchorMax = new Vector2(1f, 1f);
            _grid.pivot = new Vector2(0.5f, 1f);
            _grid.sizeDelta = new Vector2(0f, 0f);

            float cell = (RefWidth - 2f * Margin - (Columns - 1) * Gutter) / Columns;
            var layout = contentGo.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(cell, cell * 1.25f);
            layout.spacing = new Vector2(Gutter, Gutter);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Columns;

            // Grow the content downward as cards are added, so the ScrollRect has something to scroll.
            contentGo.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRt;
            scroll.content = _grid;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
        }
    }
}
