using System;

namespace GameHub
{
    /// <summary>Which screen the hub is on. One value at a time.</summary>
    public enum HubState { Boot, Home, GamePage, Matchmaking, Loading, InGame }

    /// <summary>A hub screen. The composition root hands it everything it needs.</summary>
    public interface IHubView
    {
        void Init(HubBootstrap hub);
    }

    /// <summary>
    /// The single source of truth for "what screen are we on". Transitions set the state
    /// and raise StateChanged; views react to that event (they never track state themselves).
    /// Plain class, no Unity — the composition root (HubBootstrap) owns one instance.
    /// </summary>
    public sealed class HubFlow
    {
        public HubState State { get; private set; } = HubState.Boot;
        public string CurrentGameId { get; private set; }

        public event Action<HubState> StateChanged;

        public void GoHome()
        {
            CurrentGameId = null;
            Set(HubState.Home);
        }

        public void OpenGame(string id)
        {
            CurrentGameId = id;
            Set(HubState.GamePage);
        }

        /// <summary>Searching for an opponent. CurrentGameId is kept — we return to that
        /// game's page if the search fails or is cancelled.</summary>
        public void StartMatchmaking() => Set(HubState.Matchmaking);

        public void StartLoading() => Set(HubState.Loading);

        public void EnterGame() => Set(HubState.InGame);

        public void ExitToHub()
        {
            CurrentGameId = null;
            Set(HubState.Home);
        }

        void Set(HubState next)
        {
            State = next;
            StateChanged?.Invoke(State);
        }
    }
}
