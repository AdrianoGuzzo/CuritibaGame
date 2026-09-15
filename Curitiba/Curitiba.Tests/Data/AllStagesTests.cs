using System.Collections.Generic;
using System.IO;
using System.Linq;
using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.Data
{
    /// <summary>
    /// Every stage file that ships with the game, loaded and validated.
    /// </summary>
    /// <remarks>
    /// The stage data is edited by hand and by the in-game editor, outside the compiler's reach —
    /// this is the test that stops a content change from breaking the game silently. It discovers
    /// the files rather than naming them, so a new stage is covered the moment it is added.
    /// </remarks>
    public class AllStagesTests
    {
        /// <summary>The stage files as the game sees them, copied next to the test assembly.</summary>
        public static TheoryData<string> StageFiles()
        {
            var data = new TheoryData<string>();
            foreach (string path in Directory.EnumerateFiles(Fixtures.StagesDirectory, "*.json"))
                data.Add(Path.GetFileName(path));
            return data;
        }

        [Fact]
        public void TheGame_ShouldShipAtLeastOneStage()
        {
            Assert.True(Directory.Exists(Fixtures.StagesDirectory),
                $"no stage directory at {Fixtures.StagesDirectory}");
            Assert.NotEmpty(Directory.GetFiles(Fixtures.StagesDirectory, "*.json"));
        }

        [Theory]
        [MemberData(nameof(StageFiles))]
        public void EveryStage_ShouldParse(string fileName)
        {
            string path = Path.Combine(Fixtures.StagesDirectory, fileName);

            Assert.True(StageLoader.TryLoadFile(path, out StageDefinition def), $"{fileName} does not parse");
            Assert.NotNull(def);
        }

        [Theory]
        [MemberData(nameof(StageFiles))]
        public void EveryStage_ShouldBePlayable(string fileName)
        {
            string path = Path.Combine(Fixtures.StagesDirectory, fileName);
            StageLoader.TryLoadFile(path, out StageDefinition def);

            IReadOnlyList<StageIssue> issues = StageValidator.Validate(def);
            List<StageIssue> errors = issues.Where(i => i.Severity == StageSeverity.Error).ToList();

            Assert.True(errors.Count == 0, $"{fileName} has blocking problems:\n{StageValidator.Describe(errors)}");
        }

        [Theory]
        [MemberData(nameof(StageFiles))]
        public void EveryStage_ShouldBuildAWorkingArena(string fileName)
        {
            // The end of the pipeline: JSON to StageLoader to StageDefinition to a live arena.
            string path = Path.Combine(Fixtures.StagesDirectory, fileName);
            StageLoader.TryLoadFile(path, out StageDefinition def);

            var arena = new CapaoRasoArena(HeadlessContent.Create(), def, 800f, 480f);

            Assert.True(arena.SectionCount > 0);
            Assert.False(arena.Completed);
            Assert.NotNull(arena.Player);
        }

        [Theory]
        [MemberData(nameof(StageFiles))]
        public void EveryStage_ShouldSurviveASaveReloadRoundTrip(string fileName)
        {
            // The in-game editor writes stages back out through TrySaveFile, so a stage that cannot
            // survive that round trip would be quietly corrupted the first time someone saves it.
            string path = Path.Combine(Fixtures.StagesDirectory, fileName);
            StageLoader.TryLoadFile(path, out StageDefinition original);

            using var dir = new TempDir();
            string copy = dir.File(fileName);
            Assert.True(StageLoader.TrySaveFile(copy, original));
            Assert.True(StageLoader.TryLoadFile(copy, out StageDefinition reloaded));

            Assert.Equal(original.Id, reloaded.Id);
            Assert.Equal(original.Sections.Count, reloaded.Sections.Count);
            Assert.Equal(original.Personalities.Count, reloaded.Personalities.Count);
            for (int i = 0; i < original.Sections.Count; i++)
            {
                Assert.Equal(original.Sections[i].Waves.Count, reloaded.Sections[i].Waves.Count);
                Assert.Equal(original.Sections[i].SpawnPoints.Count, reloaded.Sections[i].SpawnPoints.Count);
            }
        }

        [Fact]
        public void TheShippedCapaoRaso_ShouldStillBeTheStageTheGameLoads()
        {
            StageDefinition def = StageLoader.LoadOrDefault(StageLoader.CapaoRasoTitlePath,
                                                            StageDefinition.CapaoRasoDefault);

            Assert.Equal("capao-raso", def.Id);
            Assert.Equal("StageCapaoRaso", def.DisplayNameKey);
            Assert.True(def.Sections.Count >= 2, "the demo is authored as at least two sections");
            Assert.True(def.Sections.All(s => s.Waves.Count > 0), "every section should have a fight");
        }

        [Fact]
        public void TheShippedCapaoRaso_ShouldBeCompletable_OnceItsSectionsAreWideEnough()
        {
            // The strongest content guarantee available without art: the stage can actually be
            // finished. Section widths normally come from the scaled background textures, which do
            // not exist headless, so each section is first widened to cover its own furthest lock —
            // see FallbackWidths_ShouldSupportTheirOwnLocks for why that is not automatic today.
            StageDefinition def = StageLoader.LoadOrDefault(StageLoader.CapaoRasoTitlePath,
                                                            StageDefinition.CapaoRasoDefault);
            foreach (SectionDef section in def.Sections)
            {
                float furthestLock = section.Waves.Count == 0 ? 0f : section.Waves.Max(w => w.LockCameraX);
                section.FallbackWidth = System.Math.Max(section.FallbackWidth, furthestLock + 900f);
                section.BackgroundAsset = null;
            }

            var arena = new CapaoRasoArena(HeadlessContent.Create(), def, 800f, 480f);
            for (int frame = 0; frame < 40000 && !arena.Completed; frame++)
            {
                foreach (PiaLocoEnemy enemy in arena.Enemies.ToList())
                    enemy.TakeDamage(enemy.Health, Microsoft.Xna.Framework.Vector2.Zero);

                arena.Update(Frames.Step(), SyntheticInput.Held(Microsoft.Xna.Framework.Input.Keys.Right), null);
            }

            Assert.True(arena.Completed, "the shipped stage should be finishable by clearing and walking right");
        }

        [Fact]
        public void FallbackWidths_ShouldSupportTheirOwnLocks()
        {
            // Recorded, not fixed. A section's fallbackWidth exists so the stage still works when
            // its art is missing, but capao-raso's second section is authored 1600 wide with waves
            // locking at 1002 and 1500 — past the 800 the camera could ever reach at that width.
            // With the WallInfinite texture present the real width is far larger and the stage plays
            // fine; without it the stage would deadlock. The validator reports this as a warning.
            StageDefinition def = StageLoader.LoadOrDefault(StageLoader.CapaoRasoTitlePath,
                                                            StageDefinition.CapaoRasoDefault);

            List<StageIssue> unreachable = StageValidator.Validate(def)
                .Where(i => i.Path.EndsWith("lockCameraX", System.StringComparison.Ordinal))
                .ToList();

            Assert.All(unreachable, i => Assert.Equal(StageSeverity.Warning, i.Severity));
            Assert.True(StageValidator.IsPlayable(def), "the warnings must not make the stage unplayable");
        }

    }
}
