using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Pure VIEW: feeds the background-grid material from game state. Pushes UiTheme
    /// colours in (so palette truth stays in one asset) and drives _Pulse — a slow
    /// breath while Playing, a spike on crash that decays. MaterialPropertyBlock only:
    /// we never mutate the shared material asset.
    /// </summary>
    public sealed class BackgroundView : MonoBehaviour
    {
        [SerializeField] GameManager game;

        [Header("Pulse")]
        [SerializeField] float breathSpeed = 1.2f;   // breath cycles ~every 5s
        [SerializeField] float breathDepth = 0.15f;  // how visible the resting breath is
        [SerializeField] float crashSpike  = 1f;     // grid flash strength on a crash
        [SerializeField] float spikeDecay  = 1.5f;   // spike fade-out per second

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int GridColorId = Shader.PropertyToID("_GridColor");
        static readonly int PulseId     = Shader.PropertyToID("_Pulse");

        MeshRenderer _renderer;
        MaterialPropertyBlock _props;
        float _spike;

        void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _props = new MaterialPropertyBlock();
        }

        void OnEnable()  => game.OnCrash += OnCrash;
        void OnDisable() => game.OnCrash -= OnCrash;

        void OnCrash() => _spike = crashSpike;

        void Update()
        {
            _spike = Mathf.MoveTowards(_spike, 0f, spikeDecay * Time.deltaTime);

            float breath = game.MatchPhase == Phase.Playing
                ? (Mathf.Sin(Time.time * breathSpeed) * 0.5f + 0.5f) * breathDepth
                : 0f;

            _props.SetColor(BaseColorId, UiTheme.Instance.bgBase);
            _props.SetColor(GridColorId, UiTheme.Instance.grid);
            _props.SetFloat(PulseId, Mathf.Clamp01(breath + _spike));
            _renderer.SetPropertyBlock(_props);
        }
    }
}
