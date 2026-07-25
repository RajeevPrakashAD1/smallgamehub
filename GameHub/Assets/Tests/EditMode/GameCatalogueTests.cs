using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GameHub.Tests
{
    public class GameCatalogueTests
    {
        static HubGameManifest Manifest(string id)
        {
            // ScriptableObjects can't be new'd; CreateInstance makes a scene-free one for tests.
            var m = ScriptableObject.CreateInstance<HubGameManifest>();
            m.id = id;
            m.title = id.ToUpperInvariant();
            return m;
        }

        [Test]
        public void ById_FindsMatchingGame()
        {
            var c = new GameCatalogue(new List<HubGameManifest> { Manifest("a"), Manifest("b") });
            Assert.AreEqual("b", c.ById("b").id);
        }

        [Test]
        public void ById_MissReturnsNull()
        {
            var c = new GameCatalogue(new List<HubGameManifest> { Manifest("a") });
            Assert.IsNull(c.ById("nope"));
        }

        [Test]
        public void ById_NullOrEmptyReturnsNull()
        {
            var c = new GameCatalogue(new List<HubGameManifest> { Manifest("a") });
            Assert.IsNull(c.ById(null));
            Assert.IsNull(c.ById(""));
        }

        [Test]
        public void NullList_GivesEmptyCatalogue()
        {
            var c = new GameCatalogue(null);
            Assert.AreEqual(0, c.Games.Count);
            Assert.IsNull(c.ById("a"));
        }

        [Test]
        public void SkipsNullEntries()
        {
            // An unassigned slot in HubConfig.games shows up as null — must not throw.
            var c = new GameCatalogue(new List<HubGameManifest> { null, Manifest("a") });
            Assert.AreEqual("a", c.ById("a").id);
        }
    }
}
