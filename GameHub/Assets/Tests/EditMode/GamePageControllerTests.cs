using NUnit.Framework;
using UnityEngine;

namespace GameHub.Tests
{
    public class GamePageControllerTests
    {
        HubGameManifest _game;
        FakeContentService _content;

        [SetUp]
        public void SetUp()
        {
            _game = ScriptableObject.CreateInstance<HubGameManifest>();
            _game.id = "trailtrap";
            _game.title = "Trail Trap";

            _content = new FakeContentService { downloadSeconds = 2f, fakeSizeBytes = 12L * 1024 * 1024 };
        }

        [Test]
        public void BeforeRefresh_ShowsChecking()
        {
            var p = GamePageController.Describe(_game, _content);
            Assert.AreEqual(PageAction.None, p.PrimaryAction);
            Assert.AreEqual("Checking…", p.StatusLine);
        }

        [Test]
        public void NotDownloaded_OffersDownloadWithSize()
        {
            _content.Refresh(_game);
            var p = GamePageController.Describe(_game, _content);
            Assert.AreEqual(PageAction.Download, p.PrimaryAction);
            Assert.AreEqual("DOWNLOAD (12 MB)", p.PrimaryLabel);
            Assert.IsFalse(p.CanFree, "nothing to free before it's downloaded");
        }

        [Test]
        public void Downloading_ShowsProgressAndBlocksTheButton()
        {
            _content.Refresh(_game);
            _content.Download(_game);
            _content.Tick(1f);                       // half of downloadSeconds = 2

            var p = GamePageController.Describe(_game, _content);
            Assert.AreEqual(PageAction.None, p.PrimaryAction, "can't tap mid-download");
            Assert.IsTrue(p.ShowProgress);
            Assert.AreEqual(0.5f, p.Progress, 0.01f);
        }

        [Test]
        public void Downloaded_OffersPlayAndFree()
        {
            _content.Refresh(_game);
            _content.Download(_game);
            _content.Tick(5f);                       // past the end

            var p = GamePageController.Describe(_game, _content);
            Assert.AreEqual(PageAction.Play, p.PrimaryAction);
            Assert.AreEqual("PLAY", p.PrimaryLabel);
            Assert.IsTrue(p.CanFree);
        }

        [Test]
        public void FailedDownload_OffersRetry()
        {
            _content.Refresh(_game);
            _content.failNextDownload = true;
            _content.Download(_game);
            _content.Tick(5f);

            var p = GamePageController.Describe(_game, _content);
            Assert.AreEqual(PageAction.Retry, p.PrimaryAction);
            Assert.AreEqual("Download failed", p.StatusLine);
        }

        [Test]
        public void RetryAfterFailure_CanSucceed()
        {
            _content.Refresh(_game);
            _content.failNextDownload = true;
            _content.Download(_game);
            _content.Tick(5f);

            _content.Download(_game);                // the retry — failNextDownload self-cleared
            _content.Tick(5f);

            Assert.AreEqual(PageAction.Play, GamePageController.Describe(_game, _content).PrimaryAction);
        }

        [Test]
        public void Free_ReturnsToDownloadWithSizeRestored()
        {
            _content.Refresh(_game);
            _content.Download(_game);
            _content.Tick(5f);
            _content.Free(_game);

            var p = GamePageController.Describe(_game, _content);
            Assert.AreEqual(PageAction.Download, p.PrimaryAction);
            Assert.AreEqual("DOWNLOAD (12 MB)", p.PrimaryLabel, "size must come back, not read 0 MB");
        }

        [Test]
        public void NullGame_IsInert()
        {
            var p = GamePageController.Describe(null, _content);
            Assert.AreEqual(PageAction.None, p.PrimaryAction);
            Assert.IsFalse(p.ShowProgress);
        }

        [Test]
        public void NullService_IsInert()
        {
            var p = GamePageController.Describe(_game, null);
            Assert.AreEqual(PageAction.None, p.PrimaryAction);
        }

        [Test]
        public void FormatBytes_SwitchesUnits()
        {
            Assert.AreEqual("0 MB", GamePageController.FormatBytes(0L));
            Assert.AreEqual("512 KB", GamePageController.FormatBytes(512L * 1024));
            Assert.AreEqual("1.5 MB", GamePageController.FormatBytes(1536L * 1024));
        }

        [Test]
        public void Changed_FiresOnceOnDownloadCompletion()
        {
            _content.Refresh(_game);

            int fired = 0;
            _content.Changed += id => { if (id == _game.id) fired++; };

            _content.Download(_game);        // -> Downloading
            _content.Tick(0.5f);             // still downloading: no event
            _content.Tick(0.5f);
            Assert.AreEqual(1, fired, "ticking mid-download must not spam Changed");

            _content.Tick(5f);               // -> Ready
            Assert.AreEqual(2, fired);
        }
    }
}
