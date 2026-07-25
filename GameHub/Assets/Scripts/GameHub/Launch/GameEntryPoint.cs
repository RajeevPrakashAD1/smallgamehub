using UnityEngine;

namespace GameHub
{
    /// <summary>
    /// The handshake every hub game implements. The hub loads a game scene, finds the one
    /// GameEntryPoint in it, and calls Launch — it never knows the concrete type, which is
    /// what keeps GameHub free of any reference to a game.
    /// </summary>
    public abstract class GameEntryPoint : MonoBehaviour
    {
        /// <summary>Set before Launch runs, so subclasses can read it in OnLaunch.</summary>
        protected GameLaunchContext Context { get; private set; }

        /// <summary>Called by the hub once the scene is loaded. Start the game here.</summary>
        public void Launch(GameLaunchContext context)
        {
            Context = context;
            OnLaunch();
        }

        /// <summary>Start the game: configure it for Context.Mode and begin play.</summary>
        protected abstract void OnLaunch();

        /// <summary>
        /// Called by the hub just before the scene unloads. Shut down cleanly —
        /// end sessions, stop coroutines, unsubscribe.
        /// </summary>
        public virtual void OnExitToHub() { }

        /// <summary>Games call this to hand control back to the hub.</summary>
        protected void RequestExit() => Context?.ExitToHub?.Invoke();
    }
}
