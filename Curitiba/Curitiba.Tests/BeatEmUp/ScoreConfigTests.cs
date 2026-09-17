using System.Collections.Generic;
using Curitiba.Core.BeatEmUp;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// The scoring balance sheet: the numbers a designer turns, and what the rules do with a
    /// configuration that arrives empty, unsorted or out of range.
    /// </summary>
    public class ScoreConfigTests
    {
        [Fact]
        public void ScoreConfig_ShouldDefaultToTheDesignedValues()
        {
            // Arrange / Act
            var config = ScoreConfig.Defaults();

            // Assert
            Assert.Equal(2.0f, config.ComboDuration);
            Assert.Equal(5, config.MaxMultiplier);
            Assert.True(config.ResetComboOnDamage);
            Assert.Equal(5, config.ComboPenaltyOnDamage);
        }

        [Fact]
        public void ScoreConfig_ShouldDefaultTheHitPoints()
        {
            var config = ScoreConfig.Defaults();

            Assert.Equal(100, config.HitPoints.Normal);
            Assert.Equal(200, config.HitPoints.Heavy);
            Assert.Equal(250, config.HitPoints.Air);
            Assert.Equal(500, config.HitPoints.Finisher);
        }

        [Fact]
        public void ScoreConfig_ShouldDefaultTheEnemyPoints()
        {
            var config = ScoreConfig.Defaults();

            Assert.Equal(500, config.EnemyPoints.Normal);
            Assert.Equal(1000, config.EnemyPoints.Strong);
            Assert.Equal(5000, config.EnemyPoints.Boss);
        }

        [Fact]
        public void ScoreConfig_ShouldDefaultTheMultiplierLadder()
        {
            var config = ScoreConfig.Defaults();

            Assert.Equal(5, config.MultiplierThresholds.Count);
            Assert.Equal(0, config.MultiplierThresholds[0].MinCombo);
            Assert.Equal(1, config.MultiplierThresholds[0].Multiplier);
            Assert.Equal(30, config.MultiplierThresholds[4].MinCombo);
            Assert.Equal(5, config.MultiplierThresholds[4].Multiplier);
        }

        [Fact]
        public void ScoreConfig_ShouldDefaultTheVarietyLadder()
        {
            var config = ScoreConfig.Defaults();

            Assert.Equal(new[] { 0, 10, 20, 30 }, config.VarietyBonusPercent);
        }

        [Fact]
        public void ScoreConfig_ShouldDefaultTheNoDamageMilestones()
        {
            var config = ScoreConfig.Defaults();

            Assert.Equal(4, config.NoDamageBonuses.Count);
            Assert.Equal(5, config.NoDamageBonuses[0].Hits);
            Assert.Equal(100, config.NoDamageBonuses[0].Bonus);
            Assert.Equal(30, config.NoDamageBonuses[3].Hits);
            Assert.Equal(1500, config.NoDamageBonuses[3].Bonus);
        }

        [Fact]
        public void ScoreConfig_ShouldDefaultTheComboEndBonuses()
        {
            var config = ScoreConfig.Defaults();

            Assert.Equal(3, config.ComboEndBonuses.Count);
            Assert.Equal(10, config.ComboEndBonuses[0].MinCombo);
            Assert.Equal(500, config.ComboEndBonuses[0].Bonus);
            Assert.Equal(30, config.ComboEndBonuses[2].MinCombo);
            Assert.Equal(4000, config.ComboEndBonuses[2].Bonus);
        }

        // ---------- hostile data
        //
        // A scoring block can arrive from a stage file, a Tiled import or the in-game editor, so it
        // is external input in the sense that matters: the game must stay up and keep scoring
        // sensibly whatever is in it.

        [Fact]
        public void MutatingTheConfigAfterConstruction_ShouldNotChangeTheRules()
        {
            // The in-game editor mutates live DTOs. Resolving the config once is what keeps a
            // mid-combo edit from changing what the blows already landed were worth.
            var config = ScoreConfig.Defaults();
            var score = new ScoreSystem(config);

            config.HitPoints.Normal = 999999;
            config.ComboDuration = 900f;
            config.MaxMultiplier = 99;
            config.MultiplierThresholds.Clear();

            score.RegisterHit(AttackType.Normal);

            Assert.Equal(100, score.TotalScore);
            Assert.Equal(2.0f, score.ComboTimeRemaining);
        }

        [Fact]
        public void NegativeEnemyPoints_ShouldScoreZero_NotDrainTheScore()
        {
            var score = new ScoreSystem(new ScoreConfig
            {
                EnemyPoints = new EnemyPointsDef { Normal = -5000 },
            });

            score.RegisterEnemyDefeated(EnemyType.Normal);

            Assert.Equal(0, score.TotalScore);
        }

        [Fact]
        public void NegativeBonusValues_ShouldPayNothing()
        {
            var score = new ScoreSystem(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>(),
                VarietyBonusPercent = new List<int> { -50, -50 },
                NoDamageBonuses = new List<NoDamageBonusDef>
                {
                    new NoDamageBonusDef { Hits = 1, Bonus = -100 },
                },
                ComboEndBonuses = new List<ComboEndBonusDef>
                {
                    new ComboEndBonusDef { MinCombo = 1, Bonus = -100 },
                },
            });

            score.RegisterHit(AttackType.Normal);
            score.RegisterHit(AttackType.Heavy);
            score.EndCombo();

            Assert.Equal(300, score.TotalScore);
        }

        [Fact]
        public void ANullHitPointsBlock_ShouldFallBackToTheDefaults()
        {
            var score = new ScoreSystem(new ScoreConfig { HitPoints = null });

            score.RegisterHit(AttackType.Heavy);

            Assert.Equal(200, score.TotalScore);
        }

        [Fact]
        public void ANullEnemyPointsBlock_ShouldFallBackToTheDefaults()
        {
            var score = new ScoreSystem(new ScoreConfig { EnemyPoints = null });

            score.RegisterEnemyDefeated(EnemyType.Boss);

            Assert.Equal(5000, score.TotalScore);
        }

        [Fact]
        public void ANullMilestoneLadder_ShouldFallBackToTheDefaults()
        {
            var score = new ScoreSystem(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>(),
                VarietyBonusPercent = new List<int>(),
                ComboEndBonuses = new List<ComboEndBonusDef>(),
                NoDamageBonuses = null,
            });

            for (int i = 0; i < 5; i++)
                score.RegisterHit(AttackType.Normal);

            Assert.Equal(5 * 100 + 100, score.TotalScore);
        }

        [Fact]
        public void ANullComboEndLadder_ShouldFallBackToTheDefaults()
        {
            var score = new ScoreSystem(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>(),
                VarietyBonusPercent = new List<int>(),
                NoDamageBonuses = new List<NoDamageBonusDef>(),
                ComboEndBonuses = null,
            });

            for (int i = 0; i < 10; i++)
                score.RegisterHit(AttackType.Normal);
            score.EndCombo();

            Assert.Equal(10 * 100 + 500, score.TotalScore);
        }

        [Fact]
        public void ANullVarietyLadder_ShouldFallBackToTheDefaults()
        {
            var score = new ScoreSystem(new ScoreConfig { VarietyBonusPercent = null });

            score.RegisterHit(AttackType.Normal);
            score.RegisterHit(AttackType.Heavy);

            Assert.Equal(10, score.VarietyBonusPercent);
        }

        [Fact]
        public void ALadderWithNullEntries_ShouldIgnoreThem()
        {
            // A blank row in a hand-edited list is a typo, not a rung.
            var score = new ScoreSystem(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>
                {
                    new MultiplierTierDef { MinCombo = 0, Multiplier = 1 },
                    null,
                    new MultiplierTierDef { MinCombo = 5, Multiplier = 2 },
                },
            });

            for (int i = 0; i < 5; i++)
                score.RegisterHit(AttackType.Normal);

            Assert.Equal(2, score.CurrentMultiplier);
        }

        [Fact]
        public void DuplicateMultiplierTiers_ShouldKeepTheLastAuthored()
        {
            var score = new ScoreSystem(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>
                {
                    new MultiplierTierDef { MinCombo = 5, Multiplier = 2 },
                    new MultiplierTierDef { MinCombo = 5, Multiplier = 4 },
                },
            });

            for (int i = 0; i < 5; i++)
                score.RegisterHit(AttackType.Normal);

            Assert.Equal(4, score.CurrentMultiplier);
        }

        [Fact]
        public void AMilestoneAtZeroHits_ShouldNotPayBeforeTheFirstBlow()
        {
            // A mark at zero would otherwise pay for standing still.
            var score = new ScoreSystem(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>(),
                VarietyBonusPercent = new List<int>(),
                ComboEndBonuses = new List<ComboEndBonusDef>(),
                NoDamageBonuses = new List<NoDamageBonusDef>
                {
                    new NoDamageBonusDef { Hits = 0, Bonus = 50 },
                },
            });

            Assert.Equal(0, score.TotalScore);

            score.RegisterHit(AttackType.Normal);

            Assert.Equal(150, score.TotalScore);
        }

        /// <summary>
        /// The worst a configuration can do to the arithmetic: the highest points a field can hold,
        /// at the highest multiplier one can hold. Three such blows are more than a long fits.
        /// </summary>
        private static ScoreConfig AbsurdlyGenerous() => new ScoreConfig
        {
            MaxMultiplier = int.MaxValue,
            HitPoints = new HitPointsDef
            {
                Normal = int.MaxValue,
                Heavy = int.MaxValue,
                Air = int.MaxValue,
            },
            MultiplierThresholds = new List<MultiplierTierDef>
            {
                new MultiplierTierDef { MinCombo = 0, Multiplier = int.MaxValue },
            },
            NoDamageBonuses = new List<NoDamageBonusDef>(),
            ComboEndBonuses = new List<ComboEndBonusDef>(),
        };

        [Fact]
        public void AnAbsurdlyGenerousConfig_ShouldSaturateTheScore_NotWrapItNegative()
        {
            var score = new ScoreSystem(AbsurdlyGenerous());

            for (int i = 0; i < 3; i++)
                score.RegisterHit(AttackType.Normal);

            Assert.Equal(long.MaxValue, score.TotalScore);
            Assert.Equal(long.MaxValue, score.CurrentComboScore);
        }

        [Fact]
        public void TheVarietyShareOfASaturatedCombo_ShouldNotOverflow()
        {
            // A percentage of a score already at the ceiling: the share must be computed without
            // the multiplication wrapping on the way.
            ScoreConfig config = AbsurdlyGenerous();
            config.VarietyBonusPercent = new List<int> { 0, 10, 20, 30 };
            var score = new ScoreSystem(config);

            score.RegisterHit(AttackType.Normal);
            score.RegisterHit(AttackType.Heavy);
            score.RegisterHit(AttackType.Air);
            score.EndCombo();

            Assert.Equal(long.MaxValue, score.TotalScore);
        }
    }
}
