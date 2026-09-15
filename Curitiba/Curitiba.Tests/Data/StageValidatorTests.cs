using System.Collections.Generic;
using System.Linq;
using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.Data
{
    /// <summary>
    /// The stage validator. Each rule corresponds to a concrete way the arena breaks on bad data,
    /// so each test names the failure it is preventing rather than the shape it is checking.
    /// </summary>
    public class StageValidatorTests
    {
        private static StageDefinition Playable()
        {
            // CapaoRasoDefault is the canonical good stage, so every rule is tested as a
            // single deviation from something known to work.
            return StageDefinition.CapaoRasoDefault();
        }

        private static IReadOnlyList<StageIssue> Validate(StageDefinition def) => StageValidator.Validate(def);

        private static bool HasError(StageDefinition def, string pathFragment) =>
            Validate(def).Any(i => i.Severity == StageSeverity.Error && i.Path.Contains(pathFragment));

        private static bool HasWarning(StageDefinition def, string pathFragment) =>
            Validate(def).Any(i => i.Severity == StageSeverity.Warning && i.Path.Contains(pathFragment));

        // ---------------------------------------------------------------- the happy path

        [Fact]
        public void TheDefaultStage_ShouldBePlayable()
        {
            StageDefinition def = Playable();

            Assert.True(StageValidator.IsPlayable(def), StageValidator.Describe(Validate(def)));
        }

        [Fact]
        public void TheDefaultStage_ShouldOnlyBeFlaggedForItsUnreachableFallbackLock()
        {
            // The built-in stage carries exactly one warning, and it is a real one: its second
            // section is 1600 wide with a wave locking at 1000, past the 800 the camera could reach
            // if the background art were missing. Pinned rather than fixed — see
            // AllStagesTests.FallbackWidths_ShouldSupportTheirOwnLocks.
            List<StageIssue> flagged = Validate(Playable())
                .Where(i => i.Severity != StageSeverity.Info)
                .ToList();

            Assert.All(flagged, i => Assert.Equal(StageSeverity.Warning, i.Severity));
            Assert.All(flagged, i => Assert.EndsWith("lockCameraX", i.Path));
        }

        [Fact]
        public void AValidAuthoredStage_ShouldBePlayable()
        {
            StageLoader.TryLoadFile(Fixtures.Path_("valid-stage.json"), out StageDefinition def);

            Assert.True(StageValidator.IsPlayable(def), StageValidator.Describe(Validate(def)));
        }

        // ---------------------------------------------------------------- crashes

        [Fact]
        public void NullStage_ShouldBeRejected()
        {
            Assert.False(StageValidator.IsPlayable(null));
        }

        [Fact]
        public void MissingCorridor_ShouldBeAnError_BecauseTheArenaDereferencesIt()
        {
            StageDefinition def = Playable();
            def.Corridor = null;

            Assert.True(HasError(def, "corridor"));
        }

        [Fact]
        public void MissingBackdrop_ShouldBeAnError_BecauseTheArenaDereferencesIt()
        {
            StageDefinition def = Playable();
            def.Backdrop = null;

            Assert.True(HasError(def, "backdrop"));
        }

        [Fact]
        public void NoSections_ShouldBeAnError_BecauseLoadSectionIndexesTheFirst()
        {
            StageDefinition def = Playable();
            def.Sections.Clear();

            Assert.True(HasError(def, "sections"));
        }

        [Fact]
        public void NullSections_ShouldBeAnError()
        {
            StageDefinition def = Playable();
            def.Sections = null;

            Assert.True(HasError(def, "sections"));
        }

        [Fact]
        public void ANullSection_ShouldBeAnError()
        {
            StageDefinition def = Playable();
            def.Sections[0] = null;

            Assert.True(HasError(def, "sections[0]"));
        }

        // ---------------------------------------------------------------- unplayable geometry

        [Theory]
        [InlineData(448f, 300f)]   // inverted
        [InlineData(400f, 400f)]   // empty band
        public void AnInvertedCorridor_ShouldBeAnError(float top, float bottom)
        {
            StageDefinition def = Playable();
            def.Corridor.Top = top;
            def.Corridor.Bottom = bottom;

            Assert.True(HasError(def, "corridor"));
        }

        [Fact]
        public void ANegativeCurb_ShouldBeAnError()
        {
            StageDefinition def = Playable();
            def.Corridor.CurbHeight = -5f;

            Assert.True(HasError(def, "corridor.curbHeight"));
        }

        [Fact]
        public void ASectionWithNoWidth_ShouldBeAnError()
        {
            StageDefinition def = Playable();
            def.Sections[0].FallbackWidth = 0f;

            Assert.True(HasError(def, "sections[0].fallbackWidth"));
        }

        // ---------------------------------------------------------------- stuck stages

        [Fact]
        public void AWaveThatSpawnsNothing_ShouldBeAnError_BecauseTheLockNeverReleases()
        {
            StageDefinition def = Playable();
            def.Sections[0].Waves[0].EnemyCount = 0;
            def.Sections[0].Waves[0].Spawns.Clear();

            Assert.True(HasError(def, "sections[0].waves[0]"));
        }

        [Fact]
        public void AWaveWithNoEnemyCountButAuthoredSpawns_ShouldBeFine()
        {
            // This is the real shape in capao-raso.json: enemyCount 0 plus explicit spawns.
            StageDefinition def = Playable();
            WaveDef wave = def.Sections[0].Waves[0];
            wave.EnemyCount = 0;
            wave.Spawns.Add(new SpawnDef { Personality = "Balanced", SpawnPoint = "left" });

            Assert.False(HasError(def, "waves[0]"));
        }

        [Fact]
        public void ASectionWithNoWaves_ShouldOnlyBeInformational()
        {
            // Legal: the arena goes straight to ExitReady and the player walks through.
            StageDefinition def = Playable();
            def.Sections[0].Waves.Clear();

            IReadOnlyList<StageIssue> issues = Validate(def);

            Assert.Contains(issues, i => i.Severity == StageSeverity.Info && i.Path.Contains("waves"));
            Assert.True(StageValidator.IsPlayable(def));
        }

        [Fact]
        public void ANullWave_ShouldBeAnError()
        {
            StageDefinition def = Playable();
            def.Sections[0].Waves[0] = null;

            Assert.True(HasError(def, "waves[0]"));
        }

        // ---------------------------------------------------------------- silent fallbacks

        [Fact]
        public void ASpawnPointingAtAnUndeclaredEntry_ShouldBeAnError()
        {
            StageDefinition def = Playable();
            def.Sections[0].Waves[0].Spawns.Add(new SpawnDef { SpawnPoint = "back-alley" });

            Assert.True(HasError(def, "spawns[0].spawnPoint"));
        }

        [Fact]
        public void ASpawnPointingAtADeclaredEntry_ShouldBeFine()
        {
            StageDefinition def = Playable();
            def.Sections[0].Waves[0].Spawns.Add(new SpawnDef { SpawnPoint = "LeftEntrance" });

            Assert.False(HasError(def, "spawnPoint"));
        }

        [Theory]
        [InlineData("random:Left")]
        [InlineData("random:Right")]
        [InlineData("random")]
        public void ARandomSpawnReference_ShouldBeFine_WhenSuchAPointExists(string reference)
        {
            StageDefinition def = Playable();
            def.Sections[0].Waves[0].Spawns.Add(new SpawnDef { SpawnPoint = reference });

            Assert.False(HasError(def, "spawnPoint"));
        }

        [Fact]
        public void ARandomSpawnReference_ShouldBeAnError_WhenNoSuchTypeExists()
        {
            StageDefinition def = Playable();
            def.Sections[0].SpawnPoints.RemoveAll(p => p.Type == "Left");
            def.Sections[0].Waves[0].Spawns.Add(new SpawnDef { SpawnPoint = "random:Left" });

            Assert.True(HasError(def, "spawnPoint"));
        }

        [Fact]
        public void ARandomSpawnReference_ShouldBeAnError_WhenTheSectionHasNoPoints()
        {
            StageDefinition def = Playable();
            def.Sections[0].SpawnPoints.Clear();
            def.Sections[0].Waves[0].Spawns.Add(new SpawnDef { SpawnPoint = "random" });

            Assert.True(HasError(def, "spawnPoint"));
        }

        [Fact]
        public void AnUnknownRandomFilter_ShouldBeAWarning()
        {
            StageDefinition def = Playable();
            def.Sections[0].Waves[0].Spawns.Add(new SpawnDef { SpawnPoint = "random:Sideways" });

            Assert.True(HasWarning(def, "spawnPoint"));
        }

        [Fact]
        public void AMisspelledPersonality_ShouldBeAWarning_BecauseTheParseIsCaseSensitive()
        {
            StageDefinition def = Playable();
            def.Sections[0].Waves[0].Spawns.Add(new SpawnDef { Personality = "aggressive" });

            Assert.True(HasWarning(def, "personality"));
        }

        [Fact]
        public void AKnownPersonality_ShouldBeFine()
        {
            StageDefinition def = Playable();
            def.Sections[0].Waves[0].Spawns.Add(new SpawnDef { Personality = "Aggressive" });

            Assert.False(HasWarning(def, "personality"));
        }

        [Fact]
        public void AnUnknownPersonalityKey_ShouldBeAWarning()
        {
            StageDefinition def = Playable();
            def.Personalities["Sneaky"] = new PersonalityDef { AttackChance = 0.5f };

            Assert.True(HasWarning(def, "Sneaky"));
        }

        [Fact]
        public void ANullPersonalityEntry_ShouldBeAWarning()
        {
            StageDefinition def = Playable();
            def.Personalities["Balanced"] = null;

            Assert.True(HasWarning(def, "Balanced"));
        }

        [Fact]
        public void AnUnknownEntryMode_ShouldBeAWarning()
        {
            StageDefinition def = Playable();
            def.Sections[0].Entry.Mode = "Teleport";

            Assert.True(HasWarning(def, "entry.mode"));
        }

        [Theory]
        [InlineData("Fixed")]
        [InlineData("Carry")]
        [InlineData("Fall")]
        [InlineData("Door")]
        public void AKnownEntryMode_ShouldBeFine(string mode)
        {
            StageDefinition def = Playable();
            def.Sections[0].Entry.Mode = mode;

            Assert.False(HasWarning(def, "entry.mode"));
        }

        [Fact]
        public void AnUnknownFacing_ShouldBeAWarning()
        {
            StageDefinition def = Playable();
            def.Sections[0].Entry.Facing = "Backwards";

            Assert.True(HasWarning(def, "entry.facing"));
        }

        [Fact]
        public void AnUnknownSpawnPointType_ShouldBeAWarning()
        {
            StageDefinition def = Playable();
            def.Sections[0].SpawnPoints[0].Type = "Ceiling";

            Assert.True(HasWarning(def, "spawnPoints[0].type"));
        }

        [Fact]
        public void ADuplicateSpawnPointId_ShouldBeAWarning()
        {
            StageDefinition def = Playable();
            def.Sections[0].SpawnPoints.Add(new SpawnPointDef { Id = "left", Type = "Right" });

            Assert.True(HasWarning(def, "id"));
        }

        [Fact]
        public void AnAnonymousSpawnPoint_ShouldBeAWarning()
        {
            StageDefinition def = Playable();
            def.Sections[0].SpawnPoints.Add(new SpawnPointDef { Type = "Left" });

            Assert.True(HasWarning(def, "spawnPoints"));
        }

        // ---------------------------------------------------------------- coerced values

        [Fact]
        public void ARepeatXBelowOne_ShouldBeAWarning_SinceItIsCoerced()
        {
            StageDefinition def = Playable();
            def.Sections[0].RepeatX = 0;

            Assert.True(HasWarning(def, "repeatX"));
            Assert.True(StageValidator.IsPlayable(def));
        }

        [Fact]
        public void AnInvertedDriveway_ShouldBeAWarning()
        {
            StageDefinition def = Playable();
            def.Sections[0].DrivewayLeft = 700f;
            def.Sections[0].DrivewayRight = 300f;

            Assert.True(HasWarning(def, "driveway"));
        }

        [Fact]
        public void ADrivewayLeftAtZero_ShouldNotBeFlagged_BecauseZeroMeansDisabled()
        {
            StageDefinition def = Playable();
            def.Sections[0].DrivewayLeft = 0f;
            def.Sections[0].DrivewayRight = 0f;

            Assert.False(HasWarning(def, "driveway"));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-50)]
        public void ANegativeEnemyCount_ShouldBeFlagged(int count)
        {
            StageDefinition def = Playable();
            def.Sections[0].Waves[0].EnemyCount = count;

            Assert.Contains(Validate(def), i => i.Path.Contains("waves[0]"));
        }

        [Fact]
        public void ANegativeDelay_ShouldBeAWarning()
        {
            StageDefinition def = Playable();
            def.Sections[0].Waves[0].Delay = -1f;

            Assert.True(HasWarning(def, "delay"));
        }

        [Fact]
        public void ANegativeHitsToKnockdown_ShouldBeAWarning()
        {
            StageDefinition def = Playable();
            def.Sections[0].Waves[0].HitsToKnockdown = -2;

            Assert.True(HasWarning(def, "hitsToKnockdown"));
        }

        [Fact]
        public void ANegativeLock_ShouldBeAWarning()
        {
            StageDefinition def = Playable();
            def.Sections[0].Waves[0].LockCameraX = -10f;

            Assert.True(HasWarning(def, "lockCameraX"));
        }

        [Fact]
        public void ALockBeyondTheFallbackWidth_ShouldOnlyWarn_WhenTheSectionHasArt()
        {
            // With a background the real width comes from the scaled texture, so the stage plays —
            // but the fallback width is what it degrades to if that art ever goes missing.
            StageDefinition def = Playable();
            def.Sections[0].BackgroundAsset = "Backgrounds/Stage1/Gate";
            def.Sections[0].Waves[0].LockCameraX = 5000f;

            Assert.True(HasWarning(def, "lockCameraX"));
            Assert.True(StageValidator.IsPlayable(def));
        }

        [Fact]
        public void ALockBeyondAnArtlessSection_ShouldBeAnError_BecauseTheWaveCanNeverArm()
        {
            StageDefinition def = Playable();
            def.Sections[0].BackgroundAsset = null;
            def.Sections[0].Waves[0].LockCameraX = 5000f;

            Assert.True(HasError(def, "lockCameraX"));
        }

        [Fact]
        public void ALockInsideTheReachableSpan_ShouldNotBeFlagged()
        {
            StageDefinition def = Playable();
            def.Sections[0].BackgroundAsset = null;
            def.Sections[0].FallbackWidth = 2400f;
            def.Sections[0].Waves[0].LockCameraX = 1000f;

            // Scoped to this section: the built-in stage's other section carries its own known warning.
            Assert.False(HasWarning(def, "sections[0].waves[0].lockCameraX"));
            Assert.False(HasError(def, "sections[0].waves[0].lockCameraX"));
        }

        // ---------------------------------------------------------------- reporting

        [Fact]
        public void Issues_ShouldPointAtWhereTheProblemIs()
        {
            StageDefinition def = Playable();
            def.Sections[1].Waves[0].Spawns.Add(new SpawnDef { SpawnPoint = "nowhere" });

            StageIssue issue = Validate(def).First(i => i.Severity == StageSeverity.Error);

            Assert.Equal("sections[1].waves[0].spawns[0].spawnPoint", issue.Path);
        }

        [Fact]
        public void Describe_ShouldSayWhenThereIsNothingToReport()
        {
            Assert.Equal("(no issues)", StageValidator.Describe(new List<StageIssue>()));
        }

        [Fact]
        public void Describe_ShouldListEveryIssue()
        {
            StageDefinition def = Playable();
            def.Corridor = null;
            def.Sections.Clear();

            string report = StageValidator.Describe(Validate(def));

            Assert.Contains("corridor", report);
            Assert.Contains("sections", report);
        }

        // ---------------------------------------------------------------- broken fixtures

        [Theory]
        [InlineData("null-corridor.json")]
        [InlineData("empty-sections.json")]
        [InlineData("invalid-wave.json")]
        [InlineData("invalid-spawn.json")]
        [InlineData("inverted-corridor.json")]
        public void ABrokenFixture_ShouldBeRejected(string fixture)
        {
            Assert.True(StageLoader.TryLoadFile(Fixtures.Path_(fixture), out StageDefinition def),
                "the fixture should still parse — it is the content that is wrong, not the syntax");

            Assert.False(StageValidator.IsPlayable(def));
        }

        [Fact]
        public void AFixtureWithAMisspelledPersonality_ShouldStillBePlayable_ButFlagged()
        {
            StageLoader.TryLoadFile(Fixtures.Path_("unknown-personality.json"), out StageDefinition def);

            Assert.True(StageValidator.IsPlayable(def));
            Assert.Contains(Validate(def), i => i.Severity == StageSeverity.Warning);
        }
    }
}
