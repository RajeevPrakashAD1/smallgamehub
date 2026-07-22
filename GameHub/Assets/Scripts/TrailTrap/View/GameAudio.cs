using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Pure VIEW: plays all SFX by listening to GameManager's events. Works identically
    /// on host and client because the events fire on both (sim on host, RPC relays on
    /// clients). One 2D AudioSource + PlayOneShot — no spatial audio needed at arena scale.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        [SerializeField] GameManager game;

        [Header("One-shots")]
        [SerializeField] AudioClip crash;
        [SerializeField] AudioClip pickupBoost;
        [SerializeField] AudioClip pickupEraser;
        [SerializeField] AudioClip pickupPhase;
        [SerializeField] AudioClip eraseBlast;

        [Header("Match flow")]
        [SerializeField] AudioClip countdownBeep;
        [SerializeField] AudioClip countdownGo;
        [SerializeField] AudioClip win;
        [SerializeField] AudioClip lose;
        [SerializeField] AudioClip draw;

        [Header("Music")]
        [SerializeField] AudioClip music;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.35f;

        AudioSource _src;
        AudioSource _music;
        Phase _prevPhase;
        int   _prevSecs;      // last whole countdown second we beeped for

        void Awake()
        {
            _src = gameObject.AddComponent<AudioSource>();   // built in code, like the HUD
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;                          // pure 2D — no positioning

            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.spatialBlend = 0f;
            _music.loop = true;
            _music.volume = musicVolume;
            _music.clip = music;
        }

        void Start()
        {
            if (music) _music.Play();
        }

        void OnEnable()
        {
            game.OnCrash     += PlayCrash;
            game.OnCollected += PlayCollected;
            game.OnErased    += PlayErased;
        }

        void OnDisable()
        {
            game.OnCrash     -= PlayCrash;
            game.OnCollected -= PlayCollected;
            game.OnErased    -= PlayErased;
        }

        void Update()
        {
            Phase phase = game.MatchPhase;

            if (phase == Phase.Countdown)
            {
                // Beep once per displayed second: 3.0→2.01 all show "3", so ceil is the key.
                int secs = Mathf.CeilToInt(game.Countdown);
                if (secs != _prevSecs && secs > 0) _src.PlayOneShot(countdownBeep);
                _prevSecs = secs;
            }

            if (phase != _prevPhase)
            {
                if (phase == Phase.Playing && _prevPhase == Phase.Countdown)
                    _src.PlayOneShot(countdownGo);
                else if (phase == Phase.Over)
                    PlayResult();
                _prevSecs = 0;       // fresh rematch countdown re-beeps from "3"
            }
            _prevPhase = phase;
        }

        // Win/lose is from the LOCAL player's seat: offline and host = P1, joined client = P2.
        void PlayResult()
        {
            int me = (NetKit.Session.IsRunning && !NetKit.Session.IsServer) ? 2 : 1;
            int w = game.Winner;
            _src.PlayOneShot(w == 0 ? draw : (w == me ? win : lose));
        }

        void PlayCrash() => _src.PlayOneShot(crash);

        void PlayCollected(Vector2 pos, PowerUpType type) => _src.PlayOneShot(type switch
        {
            PowerUpType.Boost  => pickupBoost,
            PowerUpType.Eraser => pickupEraser,
            _                  => pickupPhase,
        });

        void PlayErased(Vector2 pos, float radius) => _src.PlayOneShot(eraseBlast);
    }
}
