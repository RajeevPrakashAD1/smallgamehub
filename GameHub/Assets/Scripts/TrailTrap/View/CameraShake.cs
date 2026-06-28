using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Pure VIEW juice: nudges the camera with a quick, decaying shake when something violent
    /// happens (a crash). Trauma model — callers add "trauma" in [0,1]; the felt shake = trauma^2
    /// so it hits hard then falls off fast. Trauma drains every frame, so the shake self-settles.
    /// Perlin noise (not pure random) keeps the motion a smooth vibration instead of harsh jitter.
    /// Listens to GameManager.OnCrash; never touches sim state.
    /// </summary>
    public sealed class CameraShake : MonoBehaviour
    {
        [SerializeField] GameManager game;   // fires OnCrash from the sim

        [Header("Feel")]
        [Tooltip("Max position offset (world units) at full trauma.")]
        [SerializeField] float maxOffset = 0.6f;
        [Tooltip("Max camera roll (degrees) at full trauma. Set 0 to disable rotation.")]
        [SerializeField] float maxRoll = 3f;
        [Tooltip("Trauma drained per second (1.6 ~= empties from full in ~0.6s).")]
        [SerializeField] float decayPerSec = 1.6f;
        [Tooltip("Trauma added by a single crash.")]
        [SerializeField] float crashTrauma = 0.7f;
        [Tooltip("Higher = faster, busier shake.")]
        [SerializeField] float frequency = 22f;

        float _trauma;        // [0,1], current shake energy
        Vector3 _restPos;     // camera's calm position, captured once
        float _seed;          // randomize the noise so each run/axis differs

        void Awake()
        {
            _restPos = transform.localPosition;
            _seed = Random.value * 100f;
        }

        // Subscribe only while enabled, and always unsubscribe — a dangling handler on a
        // destroyed camera would keep the GameManager (and this object) alive / throw.
        void OnEnable()  { if (game != null) game.OnCrash += AddCrashTrauma; }
        void OnDisable() { if (game != null) game.OnCrash -= AddCrashTrauma; }

        void AddCrashTrauma() => AddTrauma(crashTrauma);

        /// <summary>Add shake energy. Public so other juice (boost, hit) can call it later.</summary>
        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        // LateUpdate: run after gameplay/camera-follow has placed the camera this frame.
        void LateUpdate()
        {
            if (_trauma <= 0f)
            {
                transform.localPosition = _restPos;            // land exactly back at rest
                transform.localRotation = Quaternion.identity;
                return;
            }

            float shake = _trauma * _trauma;                   // square -> punchy, fast falloff
            float t = Time.time * frequency;

            // PerlinNoise returns [0,1]; *2-1 maps to [-1,1]. Offset seeds -> independent axes.
            float ox   = (Mathf.PerlinNoise(_seed,        t) * 2f - 1f) * maxOffset * shake;
            float oy   = (Mathf.PerlinNoise(_seed + 10f,  t) * 2f - 1f) * maxOffset * shake;
            float roll = (Mathf.PerlinNoise(_seed + 20f,  t) * 2f - 1f) * maxRoll   * shake;

            transform.localPosition = _restPos + new Vector3(ox, oy, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, roll);

            _trauma = Mathf.Max(0f, _trauma - decayPerSec * Time.deltaTime);
        }
    }
}
