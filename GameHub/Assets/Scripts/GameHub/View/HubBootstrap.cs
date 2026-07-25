using UnityEngine;

namespace GameHub
{
    /// <summary>
    /// The composition root: the one object that constructs the hub's object graph.
    /// Everything else receives what it needs and never looks anything up itself.
    /// Lives on HubRoot in Hub.unity.
    /// </summary>
    public sealed class HubBootstrap : MonoBehaviour
    {
        [Tooltip("Leave empty to load 'HubConfig' from Resources. Set it to test an alternate config.")]
        [SerializeField] HubConfig config;

        [Header("Content")]
        [Tooltip("Simulated downloads. Turn off once AddressablesContentService exists.")]
        [SerializeField] bool useFakeContent = true;

        [Tooltip("Seconds a fake download takes.")]
        [SerializeField] float fakeDownloadSeconds = 3f;

        [Header("Dev")]
        [Tooltip("Log the loaded catalogue on start. Editor convenience while there's no UI yet.")]
        [SerializeField] bool logCatalogue = true;

        public HubConfig Config => config;
        public GameCatalogue Catalogue { get; private set; }
        public HubFlow Flow { get; private set; }
        public IContentService Content { get; private set; }
        public GameLauncher Launcher { get; private set; }

        void Awake()
        {
            if (config == null) config = Resources.Load<HubConfig>("HubConfig");
            if (config == null)
            {
                Debug.LogError("HubBootstrap: no HubConfig found at Resources/HubConfig — hub cannot start.");
                enabled = false;
                return;
            }

            Catalogue = new GameCatalogue(config.games);
            Flow = new HubFlow();

            // The composition root picks the implementation; nothing else knows which one.
            Content = new FakeContentService { downloadSeconds = fakeDownloadSeconds };
            if (!useFakeContent)
                Debug.LogWarning("HubBootstrap: real content service not built yet — using the fake.");
        }

        void Update() => Content?.Tick(Time.deltaTime);

        void Start()
        {
            Launcher = GetComponent<GameLauncher>();
            if (Launcher == null) Launcher = gameObject.AddComponent<GameLauncher>();
            Launcher.Init(this);

            // Push dependencies into the views before any transition fires: Awake order
            // between components is undefined, Start-after-all-Awakes is not.
            foreach (var view in GetComponentsInChildren<IHubView>(includeInactive: true))
                view.Init(this);

            Flow.GoHome();

            if (!logCatalogue) return;
            Debug.Log($"[Hub] {config.appName}: {Catalogue.Games.Count} game(s), state={Flow.State}");
            foreach (var g in Catalogue.Games)
                Debug.Log($"[Hub]   • {g.id} — {g.title}");
        }
    }
}
