using System.IO;
using Curitiba.Core.BeatEmUp;
using Curitiba.Core.DevTools;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.DevTools
{
    /// <summary>
    /// The hot-reload policy: what happens between a stage file changing on disk and the arena
    /// being rebuilt.
    /// </summary>
    /// <remarks>
    /// The rule that matters is that a broken save must never take the running game down — the
    /// arena simply keeps going on the last definition that parsed. A file save is also not atomic,
    /// so a change is retried for a short budget of frames before being given up on.
    /// </remarks>
    public class StageReloadPolicyTests
    {
        /// <summary>A load that always succeeds / always fails, with a call count.</summary>
        private sealed class Loader
        {
            public bool Succeeds { get; set; }
            public int Calls { get; private set; }

            public bool TryLoad()
            {
                Calls++;
                return Succeeds;
            }
        }

        [Fact]
        public void NothingChanging_ShouldNotRebuild()
        {
            var policy = new StageReloadPolicy();
            var loader = new Loader { Succeeds = true };

            for (int i = 0; i < 10; i++)
                Assert.False(policy.ShouldRebuild(false, loader.TryLoad));

            Assert.Equal(0, loader.Calls);
        }

        [Fact]
        public void AValidSave_ShouldRebuildImmediately()
        {
            var policy = new StageReloadPolicy();
            var loader = new Loader { Succeeds = true };

            Assert.True(policy.ShouldRebuild(true, loader.TryLoad));
        }

        [Fact]
        public void AValidSave_ShouldRebuildOnlyOnce()
        {
            var policy = new StageReloadPolicy();
            var loader = new Loader { Succeeds = true };
            policy.ShouldRebuild(true, loader.TryLoad);

            for (int i = 0; i < 30; i++)
                Assert.False(policy.ShouldRebuild(false, loader.TryLoad));
        }

        [Fact]
        public void AnInvalidSave_ShouldNeverRebuild()
        {
            // The core guarantee: a broken JSON leaves the running arena exactly as it was.
            var policy = new StageReloadPolicy();
            var loader = new Loader { Succeeds = false };

            bool rebuilt = policy.ShouldRebuild(true, loader.TryLoad);
            for (int i = 0; i < 50; i++)
                rebuilt |= policy.ShouldRebuild(false, loader.TryLoad);

            Assert.False(rebuilt);
        }

        [Fact]
        public void AnInvalidSave_ShouldBeRetried_ThenGivenUpOn()
        {
            // The retry budget covers the moment an editor still holds the file mid-save.
            var policy = new StageReloadPolicy();
            var loader = new Loader { Succeeds = false };

            policy.ShouldRebuild(true, loader.TryLoad);
            for (int i = 0; i < 100; i++)
                policy.ShouldRebuild(false, loader.TryLoad);

            Assert.Equal(StageReloadPolicy.RetryBudget, loader.Calls);
            Assert.Equal(0, policy.RemainingRetries);
        }

        [Fact]
        public void AFileLockedAtFirst_ShouldStillReloadWhenItFreesUp()
        {
            var policy = new StageReloadPolicy();
            var loader = new Loader { Succeeds = false };
            policy.ShouldRebuild(true, loader.TryLoad);
            policy.ShouldRebuild(false, loader.TryLoad);

            loader.Succeeds = true;

            Assert.True(policy.ShouldRebuild(false, loader.TryLoad));
        }

        [Fact]
        public void AFreshChange_ShouldRearmTheBudget()
        {
            var policy = new StageReloadPolicy();
            var loader = new Loader { Succeeds = false };
            policy.ShouldRebuild(true, loader.TryLoad);
            for (int i = 0; i < StageReloadPolicy.RetryBudget; i++)
                policy.ShouldRebuild(false, loader.TryLoad);
            Assert.Equal(0, policy.RemainingRetries);

            policy.ShouldRebuild(true, loader.TryLoad);

            Assert.Equal(StageReloadPolicy.RetryBudget - 1, policy.RemainingRetries);
        }

        [Fact]
        public void ASecondChangeMidRetry_ShouldRestartTheBudget()
        {
            var policy = new StageReloadPolicy();
            var loader = new Loader { Succeeds = false };
            policy.ShouldRebuild(true, loader.TryLoad);
            for (int i = 0; i < 5; i++)
                policy.ShouldRebuild(false, loader.TryLoad);

            policy.ShouldRebuild(true, loader.TryLoad);

            Assert.Equal(StageReloadPolicy.RetryBudget - 1, policy.RemainingRetries);
        }

        [Fact]
        public void TheFullBrokenThenFixedCycle_ShouldEndWithARebuild()
        {
            // valid -> rebuild; invalid -> arena kept; valid again -> rebuild.
            var policy = new StageReloadPolicy();
            var loader = new Loader { Succeeds = true };
            Assert.True(policy.ShouldRebuild(true, loader.TryLoad));

            loader.Succeeds = false;
            bool rebuiltWhileBroken = policy.ShouldRebuild(true, loader.TryLoad);
            for (int i = 0; i < 40; i++)
                rebuiltWhileBroken |= policy.ShouldRebuild(false, loader.TryLoad);
            Assert.False(rebuiltWhileBroken);

            loader.Succeeds = true;
            Assert.True(policy.ShouldRebuild(true, loader.TryLoad));
        }
    }

    /// <summary>
    /// The reload policy wired to the real loader and real files — the same cycle, but with the
    /// JSON actually being parsed.
    /// </summary>
    public class StageReloadIntegrationTests
    {
        private static bool TryLoad(string path, out StageDefinition def) => StageLoader.TryLoadFile(path, out def);

        [Fact]
        public void AValidStageOnDisk_ShouldProduceANewDefinition()
        {
            using var dir = new TempDir();
            string path = dir.File("capao-raso.json");
            StageLoader.TrySaveFile(path, StageDefinition.CapaoRasoDefault());
            var policy = new StageReloadPolicy();

            StageDefinition loaded = null;
            bool rebuild = policy.ShouldRebuild(true, () => TryLoad(path, out loaded));

            Assert.True(rebuild);
            Assert.NotNull(loaded);
        }

        [Fact]
        public void ABrokenStageOnDisk_ShouldLeaveTheRunningArenaAlone()
        {
            using var dir = new TempDir();
            string path = dir.Write("capao-raso.json", "{ this is not json");
            var policy = new StageReloadPolicy();
            StageDefinition current = StageDefinition.CapaoRasoDefault();
            var arena = new CapaoRasoArena(HeadlessContent.Create(), current, 800f, 480f);

            StageDefinition loaded = null;
            bool rebuild = policy.ShouldRebuild(true, () => TryLoad(path, out loaded));
            for (int i = 0; i < 40; i++)
                rebuild |= policy.ShouldRebuild(false, () => TryLoad(path, out loaded));

            Assert.False(rebuild);
            Assert.Null(loaded);
            Assert.Equal(2, arena.SectionCount);   // still the definition it was built from
        }

        [Fact]
        public void FixingTheStage_ShouldReloadItOnTheNextSave()
        {
            using var dir = new TempDir();
            string path = dir.Write("capao-raso.json", "{ broken");
            var policy = new StageReloadPolicy();
            StageDefinition loaded = null;

            policy.ShouldRebuild(true, () => TryLoad(path, out loaded));
            for (int i = 0; i < 40; i++)
                policy.ShouldRebuild(false, () => TryLoad(path, out loaded));
            Assert.Null(loaded);

            StageDefinition fixedStage = StageDefinition.CapaoRasoDefault();
            fixedStage.Id = "repaired";
            StageLoader.TrySaveFile(path, fixedStage);

            Assert.True(policy.ShouldRebuild(true, () => TryLoad(path, out loaded)));
            Assert.Equal("repaired", loaded.Id);
        }

        [Fact]
        public void ADeletedStage_ShouldLeaveTheRunningArenaAlone()
        {
            using var dir = new TempDir();
            string path = dir.File("capao-raso.json");
            var policy = new StageReloadPolicy();
            StageDefinition loaded = null;

            bool rebuild = policy.ShouldRebuild(true, () => TryLoad(path, out loaded));
            for (int i = 0; i < 40; i++)
                rebuild |= policy.ShouldRebuild(false, () => TryLoad(path, out loaded));

            Assert.False(rebuild);
        }
    }

    /// <summary>
    /// The file watcher behind the reload. Only its creation contract is tested here: the change
    /// signal itself is inherently asynchronous, and the policy above already covers the logic
    /// that matters.
    /// </summary>
    public class StageHotReloaderTests
    {
        [Fact]
        public void TryCreate_ShouldReturnNull_ForAMissingDirectory()
        {
            Assert.Null(StageHotReloader.TryCreate(Path.Combine(Path.GetTempPath(), "curitiba-no-such-dir")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TryCreate_ShouldReturnNull_ForNoDirectoryAtAll(string directory)
        {
            Assert.Null(StageHotReloader.TryCreate(directory));
        }

        [Fact]
        public void TryCreate_ShouldWatchARealDirectory()
        {
            using var dir = new TempDir();

            using StageHotReloader reloader = StageHotReloader.TryCreate(dir.Path);

            Assert.NotNull(reloader);
            Assert.Equal(dir.Path, reloader.WatchedDirectory);
        }

        [Fact]
        public void AFreshWatcher_ShouldReportNoChange()
        {
            using var dir = new TempDir();
            using StageHotReloader reloader = StageHotReloader.TryCreate(dir.Path);

            Assert.False(reloader.TryConsume(out string path));
            Assert.Null(path);
        }
    }
}
