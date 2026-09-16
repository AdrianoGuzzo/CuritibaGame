using System.Linq;
using System.Text.Json;
using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.Data
{
    /// <summary>
    /// The stage data contract. Every default here is load-bearing: a JSON file that omits a field
    /// gets these values, so changing one silently changes every stage that did not spell it out.
    /// </summary>
    public class StageDefinitionTests
    {
        [Fact]
        public void Stage_ShouldDefaultToACompleteUsableShape()
        {
            var def = new StageDefinition();

            Assert.Equal(1, def.SchemaVersion);
            Assert.Equal("capao-raso", def.Id);
            Assert.Equal("StageCapaoRaso", def.DisplayNameKey);
            Assert.NotNull(def.Corridor);
            Assert.NotNull(def.Backdrop);
            Assert.NotNull(def.Tuning);
            Assert.NotNull(def.Personalities);
            Assert.NotNull(def.Sections);
        }

        [Fact]
        public void Corridor_ShouldDefaultToTheOriginalBand()
        {
            var corridor = new CorridorDef();

            Assert.Equal(300f, corridor.Top);
            Assert.Equal(448f, corridor.Bottom);
            Assert.Equal(14f, corridor.CurbHeight);
        }

        [Fact]
        public void Backdrop_ShouldDefaultToTheStage1Parallax()
        {
            var backdrop = new BackdropDef();

            Assert.Equal("Backgrounds/Stage1/Sky", backdrop.SkyAsset);
            Assert.Equal("Backgrounds/Stage1/Buildings", backdrop.BuildingsAsset);
            Assert.Equal(300f, backdrop.HorizonY);
            Assert.Equal(0.2f, backdrop.SkyScroll);
            Assert.Equal(0.5f, backdrop.BuildingsScroll);
            Assert.Equal(360, backdrop.BuildingsHeight);
        }

        [Fact]
        public void Section_ShouldDefaultToASingleScreenWithParallax()
        {
            var section = new SectionDef();

            Assert.Null(section.BackgroundAsset);
            Assert.Equal(800f, section.FallbackWidth);
            Assert.True(section.ParallaxBackdrop);
            Assert.Equal(1, section.RepeatX);
            Assert.Equal(0f, section.CurbY);
            Assert.NotNull(section.Entry);
            Assert.Empty(section.SpawnPoints);
            Assert.Empty(section.Waves);
            Assert.Empty(section.SetPieces);
        }

        [Fact]
        public void Wave_ShouldDefaultToThreeHitsToKnockdown()
        {
            // The one non-obvious default: a wave that says nothing about difficulty is not zero.
            var wave = new WaveDef();

            Assert.Equal(3, wave.HitsToKnockdown);
            Assert.Equal(0, wave.EnemyCount);
            Assert.Equal(0f, wave.Delay);
            Assert.Equal(0f, wave.LockCameraX);
            Assert.Empty(wave.Spawns);
        }

        [Fact]
        public void Spawn_ShouldDefaultToABalancedPiaLoco()
        {
            var spawn = new SpawnDef();

            Assert.Null(spawn.Type);
            Assert.Equal("piaLoco", spawn.Template);
            Assert.Equal("Balanced", spawn.Personality);
            Assert.Null(spawn.SpawnPoint);
        }

        [Fact]
        public void Entry_ShouldDefaultToTheFixedLeftPlacement()
        {
            var entry = new EntryDef();

            Assert.Equal("Fixed", entry.Mode);
            Assert.Equal(90f, entry.X);
            Assert.Equal(0f, entry.Y);
            Assert.Equal(260f, entry.FallHeight);
            Assert.Equal(70f, entry.WalkInDistance);
            Assert.Equal("Right", entry.Facing);
            Assert.False(entry.CarryProportional);
        }

        [Fact]
        public void SpawnPoint_ShouldDefaultToACustomWorldPoint()
        {
            var point = new SpawnPointDef();

            Assert.Equal("Custom", point.Type);
        }

        [Fact]
        public void SetPiece_ShouldDefaultToADecorativeFlatProp()
        {
            var piece = new SetPieceDef();

            Assert.False(piece.DepthSortByY);
            Assert.False(piece.Solid);
        }

        [Fact]
        public void Personality_ShouldDefaultToAllZeros()
        {
            // Recorded, not endorsed: a personalities entry that omits fields makes an enemy that
            // never attacks, because EnemyProfile.From copies the definition without clamping.
            var personality = new PersonalityDef();

            Assert.Equal(0f, personality.AttackChance);
            Assert.Equal(0f, personality.AttackCooldown);
        }

        [Fact]
        public void TuningSet_ShouldDefaultToTheBuiltInFighterStats()
        {
            var tuning = new TuningSet();

            Assert.Equal(100, tuning.Sofia.MaxHealth);
            Assert.Equal(30, tuning.PiaLoco.MaxHealth);
        }

        // ---------------------------------------------------------------- the canonical default

        [Fact]
        public void CapaoRasoDefault_ShouldReproduceTheOriginalCorridor()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            Assert.Equal(300f, def.Corridor.Top);
            Assert.Equal(448f, def.Corridor.Bottom);
            Assert.Equal(14f, def.Corridor.CurbHeight);
        }

        [Fact]
        public void CapaoRasoDefault_ShouldDeclareTheFourPersonalities()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            Assert.Equal(4, def.Personalities.Count);
            Assert.True(def.Personalities.ContainsKey("Aggressive"));
            Assert.True(def.Personalities.ContainsKey("Defensive"));
            Assert.True(def.Personalities.ContainsKey("Runner"));
            Assert.True(def.Personalities.ContainsKey("Balanced"));
        }

        [Fact]
        public void CapaoRasoDefault_ShouldMatchTheBuiltInPersonalityNumbers()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            PersonalityDef aggressive = def.Personalities["Aggressive"];

            Assert.Equal(0.92f, aggressive.AttackChance, 4);
            Assert.Equal(0.8f, aggressive.AttackCooldown, 4);
            Assert.Equal(40f, aggressive.PreferredDistance, 4);
        }

        [Fact]
        public void CapaoRasoDefault_ShouldHaveTwoSections()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            Assert.Equal(2, def.Sections.Count);
            Assert.Equal("Backgrounds/Stage1/Gate", def.Sections[0].BackgroundAsset);
            Assert.Equal("Backgrounds/Stage1/WallInfinite", def.Sections[1].BackgroundAsset);
        }

        [Fact]
        public void CapaoRasoDefault_ShouldUseProceduralWavesOnly()
        {
            // Every default wave relies on enemyCount; the authored spawns path is opt-in via JSON.
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            foreach (SectionDef section in def.Sections)
            {
                foreach (WaveDef wave in section.Waves)
                {
                    Assert.Empty(wave.Spawns);
                    Assert.True(wave.EnemyCount > 0);
                }
            }
        }

        [Fact]
        public void CapaoRasoDefault_ShouldRampDifficultyAcrossItsWaves()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            int[] hits = def.Sections.SelectMany(s => s.Waves).Select(w => w.HitsToKnockdown).ToArray();

            Assert.Equal(new[] { 3, 4, 4, 5 }, hits);
        }

        [Fact]
        public void CapaoRasoDefault_ShouldBeStable()
        {
            // Two calls must not share mutable state, or the editor's "reload" would leak edits.
            StageDefinition first = StageDefinition.CapaoRasoDefault();
            StageDefinition second = StageDefinition.CapaoRasoDefault();

            first.Corridor.Top = 999f;

            Assert.Equal(300f, second.Corridor.Top);
        }

        // ---------------------------------------------------------------- deserialisation

        [Fact]
        public void MinimalJson_ShouldFillInEveryDefault()
        {
            StageLoader.TryLoadFile(Fixtures.Path_("minimal-stage.json"), out StageDefinition def);

            Assert.Equal(1, def.SchemaVersion);
            Assert.NotNull(def.Corridor);
            Assert.Equal(300f, def.Corridor.Top);
            Assert.NotNull(def.Backdrop);
            Assert.Single(def.Sections);
            Assert.Equal(800f, def.Sections[0].FallbackWidth);
            Assert.Equal(3, def.Sections[0].Waves[0].HitsToKnockdown);
        }

        [Fact]
        public void MissingTuningBlock_ShouldStillYieldFullFighterStats()
        {
            // TuningSet's property initialisers run on every new(), so omitting "tuning" entirely
            // is the same as asking for the built-in stats rather than for zeros.
            StageLoader.TryLoadFile(Fixtures.Path_("minimal-stage.json"), out StageDefinition def);

            Assert.NotNull(def.Tuning);
            Assert.Equal(100, def.Tuning.Sofia.MaxHealth);
            Assert.Equal(30, def.Tuning.PiaLoco.MaxHealth);
            Assert.NotNull(def.Tuning.Sofia.ComboChain);
            Assert.Equal(4, def.Tuning.Sofia.ComboChain.Count);
        }

        [Fact]
        public void ExplicitNull_ShouldOverrideTheDefault_NotBeIgnored()
        {
            // Worth pinning because this is exactly how a hand-edited file crashes the arena.
            StageLoader.TryLoadFile(Fixtures.Path_("null-corridor.json"), out StageDefinition def);

            Assert.NotNull(def);
            Assert.Null(def.Corridor);
        }

        [Fact]
        public void AuthoredSpawns_ShouldSurviveDeserialisation()
        {
            StageLoader.TryLoadFile(Fixtures.Path_("valid-stage.json"), out StageDefinition def);
            WaveDef wave = def.Sections[0].Waves[1];

            Assert.Equal(2, wave.Spawns.Count);
            Assert.Equal("Aggressive", wave.Spawns[0].Personality);
            Assert.Equal("left", wave.Spawns[0].SpawnPoint);
            Assert.Equal("random:Right", wave.Spawns[1].SpawnPoint);
        }

        [Fact]
        public void FighterTuning_ShouldRoundTripItsComboChain()
        {
            string json = JsonSerializer.Serialize(FighterTuning.SofiaDefaults(), StageLoader.JsonOptions);

            var reloaded = JsonSerializer.Deserialize<FighterTuning>(json, StageLoader.JsonOptions);

            Assert.Equal(4, reloaded.ComboChain.Count);
            Assert.Equal("kick", reloaded.ComboChain[3].Id);
            Assert.True(reloaded.ComboChain[3].Launches);
            Assert.Equal(22, reloaded.ComboChain[3].Damage);
        }
    }

    /// <summary>The fighter stat block, which is the balancing surface the JSON exposes.</summary>
    public class FighterTuningTests
    {
        [Fact]
        public void Tuning_ShouldDefaultToTheGenericFighter()
        {
            var tuning = new FighterTuning();

            Assert.Equal(100, tuning.MaxHealth);
            Assert.Equal(10, tuning.AttackDamage);
            Assert.Equal(46, tuning.AttackReach);
            Assert.Equal(40, tuning.BodyWidth);
            Assert.Equal(72, tuning.BodyHeight);
            Assert.Equal(1f, tuning.Scale);
            Assert.Equal(175f, tuning.MoveSpeed);
            Assert.Null(tuning.ComboChain);
        }

        [Fact]
        public void SofiaDefaults_ShouldMatchHerOriginalStats()
        {
            FighterTuning tuning = FighterTuning.SofiaDefaults();

            Assert.Equal(100, tuning.MaxHealth);
            Assert.Equal(10, tuning.AttackDamage);
            Assert.Equal(48, tuning.AttackReach);
            Assert.Equal(40, tuning.BodyWidth);
            Assert.Equal(74, tuning.BodyHeight);
            Assert.Equal(175f, tuning.MoveSpeed);
            Assert.Equal(4, tuning.ComboChain.Count);
        }

        [Fact]
        public void PiaLocoDefaults_ShouldMatchItsOriginalStats()
        {
            FighterTuning tuning = FighterTuning.PiaLocoDefaults();

            Assert.Equal(30, tuning.MaxHealth);
            Assert.Equal(5, tuning.AttackDamage);
            Assert.Equal(40, tuning.AttackReach);
            Assert.Equal(42, tuning.BodyWidth);
            Assert.Equal(72, tuning.BodyHeight);
            Assert.Equal(72f, tuning.MoveSpeed);
            Assert.Equal(0.10f, tuning.InvulnerabilityOnHit, 4);
            Assert.Single(tuning.ComboChain);
        }

        [Fact]
        public void Sofia_ShouldBeStrongerAndFasterThanTheMook()
        {
            FighterTuning sofia = FighterTuning.SofiaDefaults();
            FighterTuning mook = FighterTuning.PiaLocoDefaults();

            Assert.True(sofia.MaxHealth > mook.MaxHealth);
            Assert.True(sofia.AttackDamage > mook.AttackDamage);
            Assert.True(sofia.MoveSpeed > mook.MoveSpeed);
        }

        [Fact]
        public void ComboMoveDef_ShouldDefaultToTheLegacySwing()
        {
            var move = new ComboMoveDef();

            Assert.Equal("attack", move.Id);
            Assert.Equal("Attack", move.State);
            Assert.Equal(0.12f, move.Startup);
            Assert.Equal(0.10f, move.Active);
            Assert.Equal(0.18f, move.Recovery);
            Assert.Equal(10, move.Damage);
            Assert.Equal(46, move.Reach);
            Assert.Equal(220f, move.KnockbackX);
            Assert.Equal(-40f, move.KnockbackY);
            Assert.Equal(0f, move.CancelPoint);
            Assert.False(move.RequiresHitConfirm);
            Assert.False(move.Launches);
        }
    }
}
