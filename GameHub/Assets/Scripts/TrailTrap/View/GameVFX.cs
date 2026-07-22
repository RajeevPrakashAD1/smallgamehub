using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Pure VIEW: particle bursts for the game's moments, driven by GameManager's events
    /// (host: sim-raised; clients: RPC-raised — same subscription works on both). Two
    /// code-built ParticleSystems: a radial dot burst (pickups, crashes) and an expanding
    /// ring shell (eraser). Dot texture is code-generated — still no image assets.
    /// </summary>
    public sealed class GameVFX : MonoBehaviour
    {
        [SerializeField] GameManager game;

        [Header("Tuning")]
        [SerializeField] int   pickupCount = 14;
        [SerializeField] int   crashCount  = 48;
        [SerializeField] int   ringCount   = 60;
        [SerializeField] float ringLife    = 0.45f;

        ParticleSystem _burst;   // dots spraying outward from a point
        ParticleSystem _ring;    // dots launched from a circle's edge, radially

        void Awake()
        {
            var dot = ProcSprites.MakeDotTexture(32);
            _burst = MakeSystem("Burst", dot, life: 0.6f, speed: 6f, size: 0.35f, edgeOnly: false);
            _ring  = MakeSystem("Ring",  dot, life: ringLife, speed: 3f, size: 0.22f, edgeOnly: true);
        }

        void OnEnable()
        {
            game.OnCollected += OnCollected;
            game.OnCrashAt   += OnCrashAt;
            game.OnErased    += OnErased;
        }

        void OnDisable()
        {
            game.OnCollected -= OnCollected;
            game.OnCrashAt   -= OnCrashAt;
            game.OnErased    -= OnErased;
        }

        void OnCollected(Vector2 pos, PowerUpType type)
            => Burst(_burst, pos, pickupCount, ColorFor(type));

        void OnCrashAt(Vector2 pos)
            => Burst(_burst, pos, crashCount, UiTheme.Instance.wall);   // HDR white-blue -> blooms

        void OnErased(Vector2 pos, float radius)
        {
            // Launch speed so the ring reaches the erase radius right as it fades out.
            var main = _ring.main;
            main.startSpeed = radius / ringLife;
            Burst(_ring, pos, ringCount, UiTheme.Instance.eraser);
        }

        static void Burst(ParticleSystem ps, Vector2 pos, int count, Color color)
        {
            ps.transform.position = pos;    // world-space sim: old particles stay put
            var main = ps.main;
            main.startColor = color;
            ps.Emit(count);
        }

        static Color ColorFor(PowerUpType type) => type switch
        {
            PowerUpType.Boost  => UiTheme.Instance.boost,
            PowerUpType.Phase  => UiTheme.Instance.phase,
            PowerUpType.Eraser => UiTheme.Instance.eraser,
            _                  => Color.white,
        };

        ParticleSystem MakeSystem(string name, Texture2D dot, float life, float speed, float size, bool edgeOnly)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = size;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 512;

            var emission = ps.emission;
            emission.enabled = false;               // nothing flows; we Emit() in bursts only

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = edgeOnly ? 0.1f : 0.05f;
            shape.radiusThickness = edgeOnly ? 0f : 1f;   // 0 = rim only -> clean ring

            var col = ps.colorOverLifetime;         // fade alpha 1 -> 0 across each life
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = new Material(Shader.Find("Sprites/Default")) { mainTexture = dot };
            r.sortingOrder = 20;                    // above trails and pickup dots
            return ps;
        }

    }
}
