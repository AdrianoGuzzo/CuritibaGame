using System;
using System.Collections.Generic;
using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// The notifications a HUD will subscribe to: which ones fire, what they carry, and in what
    /// order. No HUD exists yet — these fix the contract before anything depends on it.
    /// </summary>
    public class ScoreEventsTests
    {
        private const float ComboWindow = 2.0f;

        private static (ScoreSystem, ScoreRecorder) Watched(ScoreConfig config = null)
        {
            var score = new ScoreSystem(config);
            return (score, new ScoreRecorder(score));
        }

        /// <summary>A watched system with the bonus ladders off, so only the blows notify.</summary>
        private static (ScoreSystem, ScoreRecorder) WatchedWithoutBonuses() => Watched(new ScoreConfig
        {
            NoDamageBonuses = new List<NoDamageBonusDef>(),
            VarietyBonusPercent = new List<int>(),
            ComboEndBonuses = new List<ComboEndBonusDef>(),
        });

        private static void Land(ScoreSystem score, int hits, AttackType type = AttackType.Normal)
        {
            for (int i = 0; i < hits; i++)
                score.RegisterHit(type);
        }

        private static void LetTheComboLapse(ScoreSystem score)
        {
            int frames = Frames.FramesFor(ComboWindow * 2f);
            for (int i = 0; i < frames; i++)
                score.Update(Frames.DefaultStep);
        }

        // ---------- the combo

        [Fact]
        public void TheFirstHit_ShouldRaiseComboStartedThenComboChanged()
        {
            // Arrange
            var (score, recorder) = WatchedWithoutBonuses();

            // Act
            score.RegisterHit(AttackType.Normal);

            // Assert — started fires as well as changed, so a HUD subscribing only to the latter
            // is still complete.
            Assert.Equal(new[] { "ComboStarted", "ComboChanged", "ScoreChanged" }, recorder.Events);
            Assert.Equal(1, recorder.ComboStarted[0].Combo);
            Assert.Equal(1, recorder.ComboStarted[0].Multiplier);
        }

        [Fact]
        public void AComboAlreadyRunning_ShouldNotRaiseComboStartedAgain()
        {
            var (score, recorder) = WatchedWithoutBonuses();
            score.RegisterHit(AttackType.Normal);
            recorder.Clear();

            Land(score, 3);

            Assert.Empty(recorder.ComboStarted);
            Assert.Equal(3, recorder.ComboChanged.Count);
        }

        [Fact]
        public void ANewComboAfterABreak_ShouldRaiseComboStartedAgain()
        {
            var (score, recorder) = WatchedWithoutBonuses();
            score.RegisterHit(AttackType.Normal);
            LetTheComboLapse(score);
            recorder.Clear();

            score.RegisterHit(AttackType.Normal);

            Assert.Single(recorder.ComboStarted);
        }

        [Fact]
        public void ComboChanged_ShouldCarryTheComboAndItsMultiplier()
        {
            var (score, recorder) = WatchedWithoutBonuses();

            Land(score, 5);

            ComboChangedEvent last = recorder.ComboChanged[recorder.ComboChanged.Count - 1];
            Assert.Equal(5, last.Combo);
            Assert.Equal(2, last.Multiplier);
        }

        // ---------- the score

        [Fact]
        public void EveryHit_ShouldRaiseScoreChangedWithTheGainedValue()
        {
            var (score, recorder) = WatchedWithoutBonuses();

            score.RegisterHit(AttackType.Heavy);

            Assert.Single(recorder.ScoreChanged);
            Assert.Equal(200, recorder.ScoreChanged[0].Gained);
            Assert.Equal(200, recorder.ScoreChanged[0].Total);
            Assert.Equal("Hit", recorder.ScoreChanged[0].Reason.ToString());
        }

        [Fact]
        public void TheEventOrderOfAHit_ShouldPutTheStateBeforeTheMoney()
        {
            // Arrange — the 5th blow is the busiest one: it changes the combo, crosses a tier and
            // passes the first streak mark.
            var (score, recorder) = Watched(new ScoreConfig
            {
                VarietyBonusPercent = new List<int>(),
                ComboEndBonuses = new List<ComboEndBonusDef>(),
            });
            Land(score, 4);
            recorder.Clear();

            // Act
            score.RegisterHit(AttackType.Normal);

            // Assert — the multiplier must already be current when the blow is priced.
            Assert.Equal(
                new[] { "ComboChanged", "MultiplierChanged", "ScoreChanged", "ScoreChanged" },
                recorder.Events);
            Assert.Equal("Hit", recorder.ScoreChanged[0].Reason.ToString());
            Assert.Equal("NoDamageBonus", recorder.ScoreChanged[1].Reason.ToString());
        }

        [Fact]
        public void AMilestoneBonus_ShouldRaiseScoreChangedWithItsOwnReason()
        {
            var (score, recorder) = Watched(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>(),
                VarietyBonusPercent = new List<int>(),
                ComboEndBonuses = new List<ComboEndBonusDef>(),
                NoDamageBonuses = new List<NoDamageBonusDef>
                {
                    new NoDamageBonusDef { Hits = 2, Bonus = 42 },
                },
            });

            Land(score, 2);

            ScoreChangedEvent bonus = recorder.ScoreChanged[recorder.ScoreChanged.Count - 1];
            Assert.Equal(42, bonus.Gained);
            Assert.Equal("NoDamageBonus", bonus.Reason.ToString());
        }

        // ---------- the multiplier

        [Fact]
        public void TheMultiplierChanged_ShouldFireOnlyOnTheTierTransition()
        {
            // Arrange — twelve blows cross two tiers: x1 to x2 at 5, x2 to x3 at 10.
            var (score, recorder) = WatchedWithoutBonuses();

            // Act
            Land(score, 12);

            // Assert — twelve notifications would be noise; two are the news.
            Assert.Equal(2, recorder.MultiplierChanged.Count);
            Assert.Equal(1, recorder.MultiplierChanged[0].Previous);
            Assert.Equal(2, recorder.MultiplierChanged[0].Current);
            Assert.Equal(2, recorder.MultiplierChanged[1].Previous);
            Assert.Equal(3, recorder.MultiplierChanged[1].Current);
        }

        [Fact]
        public void TheMultiplierChanged_ShouldFireWhenTheComboSettles()
        {
            var (score, recorder) = WatchedWithoutBonuses();
            Land(score, 12);
            recorder.Clear();

            score.EndCombo();

            Assert.Single(recorder.MultiplierChanged);
            Assert.Equal(3, recorder.MultiplierChanged[0].Previous);
            Assert.Equal(1, recorder.MultiplierChanged[0].Current);
        }

        [Fact]
        public void AComboSettlingAtOne_ShouldNotRaiseMultiplierChanged()
        {
            var (score, recorder) = WatchedWithoutBonuses();
            score.RegisterHit(AttackType.Normal);
            recorder.Clear();

            score.EndCombo();

            Assert.Empty(recorder.MultiplierChanged);
        }

        // ---------- breaking a combo

        [Fact]
        public void ComboBroken_ShouldCarryTheFinalComboAndWhatItEarned()
        {
            // Arrange
            var (score, recorder) = Watched(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>(),
                NoDamageBonuses = new List<NoDamageBonusDef>(),
                VarietyBonusPercent = new List<int>(),
            });
            Land(score, 10);

            // Act
            score.EndCombo();

            // Assert
            ComboBrokenEvent broken = recorder.ComboBroken[0];
            Assert.Equal(10, broken.FinalCombo);
            Assert.Equal(1, broken.PeakMultiplier);
            Assert.Equal(1000, broken.ComboScore);
            Assert.Equal(500, broken.BonusScore);
        }

        [Theory]
        [InlineData("Timeout")]
        [InlineData("Damage")]
        [InlineData("Manual")]
        public void ComboBroken_ShouldCarryWhyItBroke(string expectedReason)
        {
            // The reason enum is internal, so it travels as a string and is compared as one.
            var (score, recorder) = WatchedWithoutBonuses();
            Land(score, 3);

            switch (expectedReason)
            {
                case "Timeout":
                    LetTheComboLapse(score);
                    break;
                case "Damage":
                    score.RegisterPlayerDamage();
                    break;
                default:
                    score.EndCombo();
                    break;
            }

            Assert.Single(recorder.ComboBroken);
            Assert.Equal(expectedReason, recorder.ComboBroken[0].Reason.ToString());
        }

        [Fact]
        public void AnExpiringCombo_ShouldRaiseComboBrokenOnlyOnce()
        {
            // Keep ticking well past the break: the combo is gone and must stay quiet.
            var (score, recorder) = WatchedWithoutBonuses();
            score.RegisterHit(AttackType.Normal);

            LetTheComboLapse(score);
            LetTheComboLapse(score);

            Assert.Single(recorder.ComboBroken);
        }

        [Fact]
        public void AHugeDeltaAfterAFreeze_ShouldRaiseComboBrokenOnlyOnce()
        {
            var (score, recorder) = WatchedWithoutBonuses();
            score.RegisterHit(AttackType.Normal);

            score.Update(100f);

            Assert.Single(recorder.ComboBroken);
        }

        [Fact]
        public void EndComboTwice_ShouldRaiseComboBrokenOnlyOnce()
        {
            var (score, recorder) = WatchedWithoutBonuses();
            Land(score, 3);

            score.EndCombo();
            score.EndCombo();

            Assert.Single(recorder.ComboBroken);
        }

        // ---------- damage

        [Fact]
        public void TakingDamage_ShouldRaiseDamageTakenBeforeComboBroken()
        {
            // Arrange
            var (score, recorder) = Watched(new ScoreConfig
            {
                VarietyBonusPercent = new List<int>(),
                NoDamageBonuses = new List<NoDamageBonusDef>(),
            });
            Land(score, 12);
            recorder.Clear();

            // Act
            score.RegisterPlayerDamage();

            // Assert — the cause first, then the payout, then the consequence.
            Assert.Equal(
                new[] { "DamageTaken", "ScoreChanged", "ComboBroken", "MultiplierChanged" },
                recorder.Events);
            Assert.Equal("ComboEndBonus", recorder.ScoreChanged[0].Reason.ToString());
        }

        [Fact]
        public void DamageTaken_ShouldCarryWhatTheHitCost()
        {
            var (score, recorder) = WatchedWithoutBonuses();
            Land(score, 7);

            score.RegisterPlayerDamage();

            DamageTakenEvent damage = recorder.DamageTaken[0];
            Assert.Equal(7, damage.ComboBefore);
            Assert.Equal(0, damage.ComboAfter);
            Assert.Equal(7, damage.StreakLost);
        }

        [Fact]
        public void DamageTaken_ShouldCarryTheDockedCombo_WhenTheComboOnlyShrinks()
        {
            var (score, recorder) = Watched(new ScoreConfig
            {
                ResetComboOnDamage = false,
                ComboPenaltyOnDamage = 5,
                NoDamageBonuses = new List<NoDamageBonusDef>(),
                VarietyBonusPercent = new List<int>(),
                ComboEndBonuses = new List<ComboEndBonusDef>(),
            });
            Land(score, 12);
            recorder.Clear();

            score.RegisterPlayerDamage();

            Assert.Equal(new[] { "DamageTaken", "ComboChanged", "MultiplierChanged" }, recorder.Events);
            Assert.Equal(12, recorder.DamageTaken[0].ComboBefore);
            Assert.Equal(7, recorder.DamageTaken[0].ComboAfter);
            Assert.Empty(recorder.ComboBroken);
        }

        [Fact]
        public void TakingDamageWithNoActiveCombo_ShouldRaiseOnlyDamageTaken()
        {
            var (score, recorder) = Watched();

            score.RegisterPlayerDamage();

            Assert.Equal(new[] { "DamageTaken" }, recorder.Events);
        }

        // ---------- defeating an enemy

        [Fact]
        public void DefeatingAnEnemy_ShouldRaiseScoreChangedThenEnemyDefeated()
        {
            // Arrange
            var (score, recorder) = WatchedWithoutBonuses();
            score.RegisterHit(AttackType.Normal);
            recorder.Clear();

            // Act
            score.RegisterEnemyDefeated(EnemyType.Strong);

            // Assert
            Assert.Equal(new[] { "ScoreChanged", "EnemyDefeated" }, recorder.Events);
            Assert.Equal(1000, recorder.EnemyDefeated[0].Gained);
            Assert.Equal(1, recorder.EnemyDefeated[0].DefeatedInCombo);
            Assert.Equal("Strong", recorder.EnemyDefeated[0].EnemyType.ToString());
            Assert.Equal("EnemyDefeated", recorder.ScoreChanged[0].Reason.ToString());
        }

        // ---------- quiet paths

        [Fact]
        public void UpdateWithNoActiveCombo_ShouldRaiseNothing()
        {
            var (score, recorder) = Watched();

            LetTheComboLapse(score);

            Assert.Empty(recorder.Events);
        }

        [Fact]
        public void AFrozenClock_ShouldRaiseNothing()
        {
            var (score, recorder) = WatchedWithoutBonuses();
            score.RegisterHit(AttackType.Normal);
            recorder.Clear();

            for (int i = 0; i < 10; i++)
                score.Update(0f);

            Assert.Empty(recorder.Events);
        }

        [Fact]
        public void Reset_ShouldRaiseNothing()
        {
            // Reset is teardown, not gameplay: a subscriber reacting to it would be reacting to a
            // new run being set up.
            var (score, recorder) = Watched();
            Land(score, 12);
            recorder.Clear();

            score.Reset();

            Assert.Empty(recorder.Events);
        }

        [Fact]
        public void ASystemWithNoSubscribers_ShouldNotCrash()
        {
            var score = new ScoreSystem();

            Exception thrown = Record.Exception(() =>
            {
                Land(score, 12);
                score.RegisterEnemyDefeated(EnemyType.Boss);
                score.RegisterPlayerDamage();
                score.Update(1f);
                score.EndCombo();
                score.Reset();
            });

            Assert.Null(thrown);
        }
    }
}
