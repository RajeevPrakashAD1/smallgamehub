using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace GameHub
{
    /// <summary>
    /// Shared uGUI builders for the hub's code-built screens, so Home, the game page and the
    /// cards don't each grow their own copy of the same five calls.
    /// </summary>
    public static class HubUi
    {
        public static Canvas MakeCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            return canvas;
        }

        // Legacy module: the new input backend delivers no UI events in this editor.
        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static TMP_Text MakeText(string name, Transform parent, TMP_FontAsset font,
                                        float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, worldPositionStays: false);

            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        public static Image MakePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, worldPositionStays: false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        public static Button MakeButton(string name, Transform parent, Color bg)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, worldPositionStays: false);
            go.GetComponent<Image>().color = bg;
            return go.GetComponent<Button>();
        }

        /// <summary>Fill the parent, inset by padding on all sides.</summary>
        public static void Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>Anchor to the parent's centre, then offset.</summary>
        public static void Center(RectTransform rt, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
        }
    }
}
