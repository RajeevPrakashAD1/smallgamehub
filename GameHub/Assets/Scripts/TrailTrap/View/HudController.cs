using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace TrailTrap
{
    /// <summary>
    /// Pure VIEW: the on-screen HUD. Builds its whole uGUI (Canvas, a big center label, and a
    /// Rematch button) in code so there's nothing fragile to wire in the Inspector, then each
    /// frame it just READS GameManager's state (phase / countdown / winner) and shows the right
    /// thing. Like CameraShake, it never touches the sim — replaces the temporary OnGUI dev HUD.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        GameManager _game;    // the sim we observe (found at Awake, never mutated)
        TMP_Text _big;        // countdown number, "GO!", and the winner headline all reuse this
        TMP_FontAsset _font;  // Orbitron SDF from Resources; null -> TMP's default font
        Button _rematch;      // shown only when the match is Over
        Phase  _prevPhase;    // last frame's phase, so we can detect Countdown -> Playing
        float  _goFlashLeft;  // seconds left to keep flashing "GO!" after the countdown ends

        [Tooltip("How long 'GO!' stays on screen once the duel starts.")]
        [SerializeField] float goFlashSeconds = 0.6f;

        void Awake()
        {
            // One GameManager in the scene; find it instead of a serialized ref (less to wire).
            // FindAny (not FindFirst): there's only one, so we don't need ordered results.
            _game = FindAnyObjectByType<GameManager>();
            _font = Resources.Load<TMP_FontAsset>("Orbitron-SDF");
            BuildUi();
            _prevPhase = Phase.Countdown;
        }

        void Update()
        {
            if (_game == null) return;
            Phase phase = _game.MatchPhase;

            // Edge-detect the countdown ending so "GO!" flashes for a moment as play begins.
            if (_prevPhase == Phase.Countdown && phase == Phase.Playing)
                _goFlashLeft = goFlashSeconds;
            _prevPhase = phase;
            if (_goFlashLeft > 0f) _goFlashLeft -= Time.deltaTime;

            switch (phase)
            {
                case Phase.Countdown:
                    // Ceil so the last fractional second still reads "1", not "0".
                    SetBig(Mathf.CeilToInt(_game.Countdown).ToString(), UiTheme.Instance.countdown);
                    _rematch.gameObject.SetActive(false);
                    break;

                case Phase.Playing:
                    SetBig(_goFlashLeft > 0f ? "GO!" : "", UiTheme.Instance.p1);   // flash, then clear
                    _rematch.gameObject.SetActive(false);
                    break;

                case Phase.Over:
                    int w = _game.Winner;                            // 0 draw, 1 = p1, 2 = p2
                    SetBig(w == 0 ? "DRAW" : $"P{w} WINS",
                           w == 0 ? UiTheme.Instance.draw
                                  : (w == 1 ? UiTheme.Instance.p1 : UiTheme.Instance.p2));
                    _rematch.gameObject.SetActive(true);
                    break;
            }
        }

        void SetBig(string msg, Color color)
        {
            _big.text = msg;
            _big.color = color;
        }

        // ---- UI construction (runs once) ------------------------------------------------

        void BuildUi()
        {
            // A Canvas is the root any UI must live under. Overlay = drawn straight onto the
            // screen after everything else, so it ignores the (shaking) camera entirely.
            var canvasGo = new GameObject("HUD Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;   // sit above any world-space sprites

            // MOBILE-FIRST: design against a fixed portrait reference and let the scaler resize
            // the UI to whatever real device resolution we get, so text stays proportional.
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;   // balance width vs height when the aspect differs

            EnsureEventSystem();                 // buttons only get clicks if one exists

            _big = MakeText("Big Label", canvas.transform, fontSize: 300,
                            anchoredPos: new Vector2(0f, 120f), size: new Vector2(1000f, 500f));

            _rematch = MakeButton("Rematch", canvas.transform, "REMATCH",
                                  anchoredPos: new Vector2(0f, -260f), size: new Vector2(460f, 150f));
            _rematch.onClick.AddListener(() => _game.Rematch());
            _rematch.gameObject.SetActive(false);
        }

        // uGUI clicks are delivered by an EventSystem; the scene may not have one, so add it once.
        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            // StandaloneInputModule reads legacy Input, which is on (Active Input Handling = Both).
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        TMP_Text MakeText(string name, Transform parent, float fontSize, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, worldPositionStays: false);

            var t = go.GetComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;                    // Orbitron; else TMP default
            t.fontSize = fontSize;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;        // don't wrap the big number
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;                              // labels never eat clicks

            CenterRect(t.rectTransform, anchoredPos, size);
            return t;
        }

        Button MakeButton(string name, Transform parent, string label, Vector2 anchoredPos, Vector2 size)
        {
            // A button is an Image (its background/hit area) plus a Button behaviour, with a
            // Text child for the caption — the same three pieces the editor's "UI > Button" makes.
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, worldPositionStays: false);
            CenterRect((RectTransform)go.transform, anchoredPos, size);

            go.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.9f);

            var text = MakeText(name + " Label", go.transform, fontSize: 60,
                                anchoredPos: Vector2.zero, size: size);
            text.text = label;

            return go.GetComponent<Button>();
        }

        // Anchor a RectTransform to the screen centre, then offset by anchoredPos — so our
        // hand-picked positions are measured from the middle, matching the reference resolution.
        static void CenterRect(RectTransform rt, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
        }
    }
}
