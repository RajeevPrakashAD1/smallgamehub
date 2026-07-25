using System.Collections.Generic;
using NUnit.Framework;

namespace GameHub.Tests
{
    public class HubFlowTests
    {
        [Test]
        public void StartsInBoot()
        {
            Assert.AreEqual(HubState.Boot, new HubFlow().State);
        }

        [Test]
        public void OpenGame_SetsStateAndId()
        {
            var f = new HubFlow();
            f.OpenGame("trailtrap");
            Assert.AreEqual(HubState.GamePage, f.State);
            Assert.AreEqual("trailtrap", f.CurrentGameId);
        }

        [Test]
        public void GoHome_ClearsCurrentGame()
        {
            var f = new HubFlow();
            f.OpenGame("trailtrap");
            f.GoHome();
            Assert.AreEqual(HubState.Home, f.State);
            Assert.IsNull(f.CurrentGameId);
        }

        [Test]
        public void EveryTransition_RaisesStateChanged()
        {
            var f = new HubFlow();
            var seen = new List<HubState>();
            f.StateChanged += s => seen.Add(s);

            f.GoHome();
            f.OpenGame("x");
            f.StartLoading();
            f.EnterGame();
            f.ExitToHub();

            CollectionAssert.AreEqual(
                new[] { HubState.Home, HubState.GamePage, HubState.Loading,
                        HubState.InGame, HubState.Home },
                seen);
        }

        [Test]
        public void LoadingAndEnterGame_KeepCurrentGameId()
        {
            // The launch path must not lose which game it's launching.
            var f = new HubFlow();
            f.OpenGame("trailtrap");
            f.StartLoading();
            f.EnterGame();
            Assert.AreEqual("trailtrap", f.CurrentGameId);
        }
    }
}
