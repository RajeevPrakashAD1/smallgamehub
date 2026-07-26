using System.Collections;
using NetKit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace GameHub
{
    /// <summary>
    /// Loads a game scene additively on top of the hub, hands its GameEntryPoint a launch
    /// context, and tears it back down. For multiplayer it first drives matchmaking, so the
    /// session is already live before the scene loads. The hub's only contact with a game.
    /// </summary>
    public sealed class GameLauncher : MonoBehaviour
    {
        HubBootstrap _hub;
        Camera _hubCamera;
        Scene _loaded;
        GameEntryPoint _entry;
        bool _exiting;

        HubGameManifest _pending;     // the game we're currently matchmaking for
        bool _watching;

        public bool IsGameRunning => _entry != null;

        public void Init(HubBootstrap hub)
        {
            _hub = hub;
            _hubCamera = Camera.main;
        }

        void OnDestroy() => Watch(false);

        // ---- launch --------------------------------------------------------------------

        public void Launch(HubGameManifest game, GameMode mode)
        {
            if (IsGameRunning)
            {
                Debug.LogWarning("GameLauncher: a game is already running.");
                return;
            }
            if (game == null || string.IsNullOrEmpty(game.sceneAddress))
            {
                Debug.LogError($"GameLauncher: '{game?.id}' has no sceneAddress.");
                return;
            }

            if (mode == GameMode.Multiplayer)
            {
                BeginMatchmaking(game);
                return;
            }

            // Solo: no session, one seat, and the player is always seat 0.
            _hub.Flow.StartLoading();
            StartCoroutine(LoadRoutine(game, GameMode.Solo, MatchResult.Ready(MatchRole.Host, 0, 1)));
        }

        // ---- matchmaking ---------------------------------------------------------------

        public void BeginMatchmaking(HubGameManifest game)
        {
            if (_hub.Matchmaker == null)
            {
                Debug.LogError("GameLauncher: no matchmaker — cannot start a multiplayer match.");
                _hub.Flow.GoHome();
                return;
            }

            _pending = game;
            Watch(true);
            _hub.Flow.StartMatchmaking();
            _hub.Matchmaker.BeginSearch(new MatchRequest(game.id, Mathf.Max(2, game.playersPerMatch)));
        }

        /// <summary>MatchAction.Retry — search again for the same game.</summary>
        public void RetryMatchmaking()
        {
            if (_pending != null) BeginMatchmaking(_pending);
        }

        /// <summary>MatchAction.Cancel — stop searching but stay on the screen.</summary>
        public void CancelMatchmaking() => _hub.Matchmaker?.Cancel();

        /// <summary>MatchAction.Back — leave matchmaking entirely, back to the game page.</summary>
        public void AbandonMatchmaking()
        {
            Watch(false);
            _hub.Matchmaker?.Cancel();

            string id = _pending != null ? _pending.id : null;
            _pending = null;

            if (string.IsNullOrEmpty(id)) _hub.Flow.GoHome();
            else _hub.Flow.OpenGame(id);
        }

        void Watch(bool on)
        {
            if (_hub?.Matchmaker == null || on == _watching) return;

            if (on) _hub.Matchmaker.PhaseChanged += OnMatchPhaseChanged;
            else _hub.Matchmaker.PhaseChanged -= OnMatchPhaseChanged;
            _watching = on;
        }

        // Failed and Cancelled deliberately do nothing here — the matchmaking screen shows
        // RETRY / BACK and calls back in. Only Ready moves the flow forward.
        void OnMatchPhaseChanged(MatchPhase phase)
        {
            if (phase != MatchPhase.Ready || _pending == null) return;

            Watch(false);
            var game = _pending;
            _pending = null;

            _hub.Flow.StartLoading();
            StartCoroutine(LoadRoutine(game, GameMode.Multiplayer, _hub.Matchmaker.Result));
        }

        // ---- scene load ----------------------------------------------------------------

        IEnumerator LoadRoutine(HubGameManifest game, GameMode mode, MatchResult match)
        {
            var op = SceneManager.LoadSceneAsync(game.sceneAddress, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"GameLauncher: scene '{game.sceneAddress}' is not in Build Settings.");
                AbortLoad(mode);
                yield break;
            }

            while (!op.isDone) yield return null;

            _loaded = SceneManager.GetSceneByName(game.sceneAddress);
            _entry = FindEntryPoint(_loaded);

            if (_entry == null)
            {
                Debug.LogError($"GameLauncher: no GameEntryPoint in scene '{game.sceneAddress}'.");
                yield return SceneManager.UnloadSceneAsync(_loaded);
                _loaded = default;
                AbortLoad(mode);
                yield break;
            }

            // The hub is the persistent scene, so its EventSystem is the one that lives.
            // A game scene may author its own; Unity allows exactly one active.
            DisableRivalEventSystems(_loaded);

            // The game owns the screen now: hub camera off (avoids two AudioListeners),
            // and the flow's InGame state hides every hub canvas.
            if (_hubCamera != null) _hubCamera.enabled = false;
            _hub.Flow.EnterGame();

            _entry.Launch(new GameLaunchContext(
                game.id, mode, RequestExit, match.Role, match.Seat, match.PlayerCount));
        }

        // A half-launched multiplayer match would otherwise leave a live session behind.
        void AbortLoad(GameMode mode)
        {
            if (mode == GameMode.Multiplayer && Session.IsRunning) Session.Shutdown();
            _hub.Flow.GoHome();
        }

        static void DisableRivalEventSystems(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var es in root.GetComponentsInChildren<EventSystem>(includeInactive: true))
                    es.gameObject.SetActive(false);
        }

        static GameEntryPoint FindEntryPoint(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var entry = root.GetComponentInChildren<GameEntryPoint>(includeInactive: true);
                if (entry != null) return entry;
            }
            return null;
        }

        // ---- teardown ------------------------------------------------------------------

        /// <summary>Handed to the game as GameLaunchContext.ExitToHub — its only hub capability.</summary>
        void RequestExit()
        {
            if (!IsGameRunning || _exiting) return;
            _exiting = true;
            StartCoroutine(ExitRoutine());
        }

        IEnumerator ExitRoutine()
        {
            // Tell the game to shut down while its scene is still loaded, so it can end
            // sessions and unsubscribe before its objects are destroyed under it.
            _entry.OnExitToHub();
            _entry = null;

            if (_loaded.IsValid() && _loaded.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(_loaded);
                while (op != null && !op.isDone) yield return null;
            }
            _loaded = default;

            // The game shuts its own session down in OnExitToHub; this is the safety net
            // for a game that forgets, so the hub never returns Home still hosting.
            if (Session.IsRunning) Session.Shutdown();

            if (_hubCamera != null) _hubCamera.enabled = true;
            _hub.Flow.GoHome();
            _exiting = false;
        }
    }
}
