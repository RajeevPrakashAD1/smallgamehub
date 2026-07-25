using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace GameHub
{
    /// <summary>
    /// Loads a game scene additively on top of the hub, hands its GameEntryPoint a launch
    /// context, and tears it back down. The hub's only contact with a running game.
    /// </summary>
    public sealed class GameLauncher : MonoBehaviour
    {
        HubBootstrap _hub;
        Camera _hubCamera;
        Scene _loaded;
        GameEntryPoint _entry;
        bool _exiting;

        public bool IsGameRunning => _entry != null;

        public void Init(HubBootstrap hub)
        {
            _hub = hub;
            _hubCamera = Camera.main;
        }

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

            _hub.Flow.StartLoading();
            StartCoroutine(LoadRoutine(game, mode));
        }

        IEnumerator LoadRoutine(HubGameManifest game, GameMode mode)
        {
            var op = SceneManager.LoadSceneAsync(game.sceneAddress, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"GameLauncher: scene '{game.sceneAddress}' is not in Build Settings.");
                _hub.Flow.GoHome();
                yield break;
            }

            while (!op.isDone) yield return null;

            _loaded = SceneManager.GetSceneByName(game.sceneAddress);
            _entry = FindEntryPoint(_loaded);

            if (_entry == null)
            {
                Debug.LogError($"GameLauncher: no GameEntryPoint in scene '{game.sceneAddress}'.");
                yield return SceneManager.UnloadSceneAsync(_loaded);
                _hub.Flow.GoHome();
                yield break;
            }

            // The hub is the persistent scene, so its EventSystem is the one that lives.
            // A game scene may author its own; Unity allows exactly one active.
            DisableRivalEventSystems(_loaded);

            // The game owns the screen now: hub camera off (avoids two AudioListeners),
            // and the flow's InGame state hides every hub canvas.
            if (_hubCamera != null) _hubCamera.enabled = false;
            _hub.Flow.EnterGame();

            _entry.Launch(new GameLaunchContext(game.id, mode, RequestExit));
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

            if (_hubCamera != null) _hubCamera.enabled = true;
            _hub.Flow.GoHome();
            _exiting = false;
        }
    }
}
