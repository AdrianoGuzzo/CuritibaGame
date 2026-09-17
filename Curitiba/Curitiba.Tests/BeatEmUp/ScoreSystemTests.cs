using System.Collections.Generic;
using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// The scoring mechanic: the combo window, the multiplier ladder, what a blow is worth, and the
    /// bonuses that reward fighting variedly, aggressively and without getting hit.
    /// </summary>
    /// <remarks>
    /// Everything here is a plain rule over a float clock — no arena, no fighter, no content. Time
    /// is driven with <see cref="Frames.DefaultStep"/> through the local helpers, so no test ever
    /// touches a real clock. Score assertions are exact on purpose: the whole mechanic is integer
    /// arithmetic, so unlike the timer there is no accumulated float error to allow for.
    /// </remarks>
    public class ScoreSystemTests
    {
        private const float ComboWindow = 2.0f;

        private static void Advance(ScoreSystem score, int frames)
        {
            for (int i = 0; i < frames; i++)
                score.Update(Frames.DefaultStep);
        }

        private static void AdvanceSeconds(ScoreSystem score, float seconds) =>
            Advance(score, Frames.FramesFor(seconds));

        /// <summary>Lets the combo lapse, overshooting the window rather than counting frames.</summary>
        private static void LetTheComboLapse(ScoreSystem score) => AdvanceSeconds(score, ComboWindow * 2f);

        /// <summary>
        /// A system with every bonus ladder authored as empty, to isolate the per-blow arithmetic
        /// from the milestone, variety and combo-end bonuses.
        /// </summary>
        private static ScoreSystem WithoutBonuses() => new ScoreSystem(new ScoreConfig
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

        // ---------- a fresh system

        [Fact]
        public void AFreshScoreSystem_ShouldStartAtZero()
        {
            // Arrange / Act
            var score = new ScoreSystem();

            // Assert
            Assert.Equal(0, score.TotalScore);
            Assert.Equal(0, score.CurrentCombo);
            Assert.Equal(0, score.CurrentComboScore);
            Assert.Equal(0, score.HitsWithoutDamage);
            Assert.Equal(0, score.EnemiesDefeatedInCombo);
            Assert.Equal(0, score.DamageTakenCount);
            Assert.Equal(0f, score.ComboTimeRemaining);
            Assert.False(score.IsComboActive);

            // A run that has not started has still reached x1 — never x0.
            Assert.Equal(1, score.CurrentMultiplier);
            Assert.Equal(1, score.HighestMultiplier);
            Assert.Equal(0, score.HighestCombo);
        }

        [Fact]
        public void ANullConfig_ShouldFallBackToTheDefaults()
        {
            // A stage that ships no scoring block must still score exactly like the defaults.
            var score = new ScoreSystem(null);

            score.RegisterHit(AttackType.Normal);

            Assert.Equal(100, score.TotalScore);
        }

        // ---------- the combo

        [Fact]
        public void ARegisteredHit_ShouldStartTheComboAtOne()
        {
            // Arrange
            var score = new ScoreSystem();

            // Act
            score.RegisterHit(AttackType.Normal);

            // Assert
            Assert.Equal(1, score.CurrentCombo);
            Assert.True(score.IsComboActive);
        }

        [Fact]
        public void EachRegisteredHit_ShouldGrowTheCombo()
        {
            var score = new ScoreSystem();

            Land(score, 7);

            Assert.Equal(7, score.CurrentCombo);
        }

        [Fact]
        public void ARegisteredHit_ShouldRefreshTheComboTimer()
        {
            // Arrange — the point of the window: attacking keeps the combo alive indefinitely.
            var score = new ScoreSystem();
            score.RegisterHit(AttackType.Normal);

            // Act — nearly let it lapse, then land another blow.
            AdvanceSeconds(score, ComboWindow * 0.9f);
            score.RegisterHit(AttackType.Normal);

            // Assert
            Assert.Equal(ComboWindow, score.ComboTimeRemaining);
            Assert.Equal(2, score.CurrentCombo);
        }

        [Fact]
        public void TheComboTimer_ShouldCountDownWithTheElapsedTime()
        {
            var score = new ScoreSystem();
            score.RegisterHit(AttackType.Normal);

            AdvanceSeconds(score, 1.0f);

            // A frame either way: 1/60 s steps do not divide a second exactly.
            Assert.InRange(score.ComboTimeRemaining, 0.97f, 1.02f);
            Assert.Equal(1, score.CurrentCombo);
        }

        [Fact]
        public void AComboTimerReachingZero_ShouldBreakTheComboOnTheSameFrame()
        {
            // Arrange
            var score = new ScoreSystem();
            score.RegisterHit(AttackType.Normal);

            // Act — one single frame big enough to run the window out.
            score.Update(ComboWindow);

            // Assert — no frame of grace: the combo is gone already.
            Assert.Equal(0, score.CurrentCombo);
            Assert.False(score.IsComboActive);
        }

        [Fact]
        public void AnExpiredCombo_ShouldResetTheComboAndTheMultiplier()
        {
            // Arrange — climb to a tier above x1.
            var score = new ScoreSystem();
            Land(score, 12);
            Assert.Equal(3, score.CurrentMultiplier);

            // Act
            LetTheComboLapse(score);

            // Assert
            Assert.Equal(0, score.CurrentCombo);
            Assert.Equal(1, score.CurrentMultiplier);
            Assert.Equal(0f, score.ComboTimeRemaining);
        }

        [Fact]
        public void TheComboTimer_ShouldNeverGoNegative()
        {
            var score = new ScoreSystem();
            score.RegisterHit(AttackType.Normal);

            LetTheComboLapse(score);

            Assert.Equal(0f, score.ComboTimeRemaining);
        }

        [Fact]
        public void UpdateWithNoActiveCombo_ShouldDoNothing()
        {
            var score = new ScoreSystem();

            AdvanceSeconds(score, 10f);

            Assert.Equal(0, score.CurrentCombo);
            Assert.Equal(0, score.TotalScore);
            Assert.Equal(0f, score.ComboTimeRemaining);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(-100f)]
        public void ANonPositiveDelta_ShouldNotAgeTheCombo(float delta)
        {
            // A frozen or rewound clock must not move the window, in either direction.
            var score = new ScoreSystem();
            score.RegisterHit(AttackType.Normal);

            for (int i = 0; i < 10; i++)
                score.Update(delta);

            Assert.Equal(ComboWindow, score.ComboTimeRemaining);
            Assert.Equal(1, score.CurrentCombo);
        }

        [Fact]
        public void AHugeDeltaAfterAFreeze_ShouldBreakTheComboCleanly()
        {
            // The dev editor freezes the scene; the frame that resumes carries the whole pause.
            var score = new ScoreSystem();
            score.RegisterHit(AttackType.Normal);

            score.Update(100f);

            Assert.Equal(0, score.CurrentCombo);
            Assert.Equal(0f, score.ComboTimeRemaining);
        }

        [Fact]
        public void UpdateWithAGameTime_ShouldAgeTheComboLikeTheFloatOverload()
        {
            // The overload exists for callers holding a GameTime; it must be the same rule.
            var viaFloat = new ScoreSystem();
            var viaGameTime = new ScoreSystem();
            viaFloat.RegisterHit(AttackType.Normal);
            viaGameTime.RegisterHit(AttackType.Normal);

            viaFloat.Update(0.5f);
            viaGameTime.Update(Frames.Step(0.5f));

            Assert.Equal(viaFloat.ComboTimeRemaining, viaGameTime.ComboTimeRemaining);
            Assert.Equal(viaFloat.CurrentCombo, viaGameTime.CurrentCombo);
        }

        [Fact]
        public void ANullGameTime_ShouldNotCrashTheScoreSystem()
        {
            var score = new ScoreSystem();
            score.RegisterHit(AttackType.Normal);

            Assert.Null(Record.Exception(() => score.Update(null)));
            Assert.Equal(1, score.CurrentCombo);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-2f)]
        public void ANonPositiveComboDuration_ShouldFallBackToAUsableWindow(float duration)
        {
            // A zero window in the data would kill every combo on the next frame, silently
            // switching the whole mechanic off.
            var score = new ScoreSystem(new ScoreConfig { ComboDuration = duration });

            score.RegisterHit(AttackType.Normal);

            Assert.True(score.ComboTimeRemaining > 0f);
            Assert.Equal(1, score.CurrentCombo);
        }

        // ---------- the multiplier ladder

        [Theory]
        [InlineData(1, 1)]
        [InlineData(4, 1)]
        [InlineData(5, 2)]
        [InlineData(9, 2)]
        [InlineData(10, 3)]
        [InlineData(19, 3)]
        [InlineData(20, 4)]
        [InlineData(29, 4)]
        [InlineData(30, 5)]
        [InlineData(45, 5)]
        public void TheMultiplier_ShouldFollowTheComboTier(int hits, int expected)
        {
            var score = new ScoreSystem();

            Land(score, hits);

            Assert.Equal(expected, score.CurrentMultiplier);
        }

        [Fact]
        public void TheMultiplier_ShouldNeverExceedTheConfiguredCeiling()
        {
            // Arrange — the ladder still lists x5, but the ceiling says x2.
            var score = new ScoreSystem(new ScoreConfig { MaxMultiplier = 2 });

            // Act
            Land(score, 40);

            // Assert
            Assert.Equal(2, score.CurrentMultiplier);
        }

        [Fact]
        public void AnUnsortedMultiplierTable_ShouldStillResolveByComboTier()
        {
            // Hand-authored JSON has no reason to be in order.
            var score = new ScoreSystem(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>
                {
                    new MultiplierTierDef { MinCombo = 10, Multiplier = 3 },
                    new MultiplierTierDef { MinCombo = 0, Multiplier = 1 },
                    new MultiplierTierDef { MinCombo = 5, Multiplier = 2 },
                },
            });

            Land(score, 10);

            Assert.Equal(3, score.CurrentMultiplier);
        }

        [Fact]
        public void ANullMultiplierTable_ShouldFallBackToTheDefaultTiers()
        {
            var score = new ScoreSystem(new ScoreConfig { MultiplierThresholds = null });

            Land(score, 10);

            Assert.Equal(3, score.CurrentMultiplier);
        }

        [Fact]
        public void AnEmptyMultiplierTable_ShouldKeepEveryComboAtOne()
        {
            // An empty table is an authored choice — "no ladder" — unlike a null one, which just
            // means the block was never written.
            var score = new ScoreSystem(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>(),
            });

            Land(score, 40);

            Assert.Equal(1, score.CurrentMultiplier);
        }

        [Fact]
        public void AMultiplierTableWithNoOpeningTier_ShouldStillStartAtOne()
        {
            // The table starts at 10; combos below it have no tier to land on.
            var score = new ScoreSystem(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>
                {
                    new MultiplierTierDef { MinCombo = 10, Multiplier = 3 },
                },
            });

            Land(score, 4);

            Assert.Equal(1, score.CurrentMultiplier);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void AnInvalidMaxMultiplier_ShouldClampToOne(int ceiling)
        {
            var score = new ScoreSystem(new ScoreConfig { MaxMultiplier = ceiling });

            Land(score, 40);

            Assert.Equal(1, score.CurrentMultiplier);
        }

        // ---------- what a blow is worth

        [Theory]
        [InlineData("Normal", 100)]
        [InlineData("Heavy", 200)]
        [InlineData("Air", 250)]
        [InlineData("Finisher", 500)]
        public void ARegisteredHit_ShouldScoreItsAttackTypeBase(string attackType, int expected)
        {
            // The enum is internal, so the case travels as a string and is parsed in here.
            var type = (AttackType)System.Enum.Parse(typeof(AttackType), attackType);
            var score = WithoutBonuses();

            score.RegisterHit(type);

            Assert.Equal(expected, score.TotalScore);
        }

        [Fact]
        public void TheHitScore_ShouldBeMultipliedByTheCurrentMultiplier()
        {
            // Arrange — the combo grows before the blow is priced, so the 5th hit already earns x2.
            var score = WithoutBonuses();

            // Act
            Land(score, 5);

            // Assert — 4 x 100 at x1, then 1 x 100 at x2.
            Assert.Equal(600, score.TotalScore);
            Assert.Equal(2, score.CurrentMultiplier);
        }

        [Fact]
        public void TheCurrentComboScore_ShouldAccumulateOnlyTheCurrentCombo()
        {
            var score = WithoutBonuses();

            Land(score, 5);

            Assert.Equal(600, score.CurrentComboScore);
        }

        [Fact]
        public void BreakingTheCombo_ShouldClearTheComboScore_ButNotTheTotal()
        {
            // Arrange
            var score = WithoutBonuses();
            Land(score, 5);

            // Act
            LetTheComboLapse(score);

            // Assert — the run keeps its points; the sequence ledger starts over.
            Assert.Equal(600, score.TotalScore);
            Assert.Equal(0, score.CurrentComboScore);
        }

        [Fact]
        public void AnUnknownAttackType_ShouldScoreLikeANormalHit()
        {
            // An int cast out of range is exactly what a future data-driven attack type could send.
            var score = WithoutBonuses();

            score.RegisterHit((AttackType)99);

            Assert.Equal(100, score.TotalScore);
            Assert.Equal(1, score.CurrentCombo);
        }

        [Fact]
        public void NegativeHitPointsInTheConfig_ShouldScoreZero_NotDrainTheScore()
        {
            var score = new ScoreSystem(new ScoreConfig
            {
                HitPoints = new HitPointsDef { Normal = -500 },
                NoDamageBonuses = new List<NoDamageBonusDef>(),
            });

            Land(score, 3);

            Assert.Equal(0, score.TotalScore);
            Assert.Equal(3, score.CurrentCombo);
        }

        [Fact]
        public void AbsurdlyLargeHitPoints_ShouldNotOverflowTheScore()
        {
            // int.MaxValue x 2 overflows an int and goes negative; the arithmetic must widen first.
            var score = new ScoreSystem(new ScoreConfig
            {
                HitPoints = new HitPointsDef { Normal = int.MaxValue },
                NoDamageBonuses = new List<NoDamageBonusDef>(),
                VarietyBonusPercent = new List<int>(),
            });

            Land(score, 5);

            Assert.True(score.TotalScore > 0, "the score must never wrap into negative territory");
            Assert.Equal(6L * int.MaxValue, score.TotalScore);
        }

        // ---------- the variety bonus

        private static readonly AttackType[] EveryAttackType =
        {
            AttackType.Normal, AttackType.Heavy, AttackType.Air, AttackType.Finisher,
        };

        private static void LandDistinct(ScoreSystem score, int distinctTypes)
        {
            for (int i = 0; i < distinctTypes; i++)
                score.RegisterHit(EveryAttackType[i]);
        }

        /// <summary>
        /// A system with a flat multiplier and no milestone or combo-end payout, so the only thing
        /// that can move the score besides the blows themselves is the variety bonus.
        /// </summary>
        private static ScoreSystem VarietyOnly() => new ScoreSystem(new ScoreConfig
        {
            MultiplierThresholds = new List<MultiplierTierDef>(),
            NoDamageBonuses = new List<NoDamageBonusDef>(),
            ComboEndBonuses = new List<ComboEndBonusDef>(),
        });

        [Theory]
        [InlineData(1, 0)]
        [InlineData(2, 10)]
        [InlineData(3, 20)]
        [InlineData(4, 30)]
        public void TheVarietyBonus_ShouldFollowTheNumberOfDistinctAttackTypes(int distinctTypes, int expected)
        {
            var score = new ScoreSystem();

            LandDistinct(score, distinctTypes);

            Assert.Equal(expected, score.VarietyBonusPercent);
        }

        [Fact]
        public void RepeatingTheSameAttackType_ShouldNotGrowTheVarietyBonus()
        {
            // The whole point of the bonus: mashing one button must not pay like a varied string.
            var score = new ScoreSystem();

            Land(score, 20, AttackType.Normal);

            Assert.Equal(0, score.VarietyBonusPercent);
        }

        [Fact]
        public void TheVarietyBonus_ShouldBePaidWhenTheComboEnds_NotPerHit()
        {
            // Arrange
            var score = VarietyOnly();

            // Act — two distinct types: 100 + 200, and a 10% share owing.
            score.RegisterHit(AttackType.Normal);
            score.RegisterHit(AttackType.Heavy);

            // Assert — nothing extra yet; each blow was worth exactly its base.
            Assert.Equal(300, score.TotalScore);
            Assert.Equal(10, score.VarietyBonusPercent);

            // Act — the sequence closes and the share is paid on what it earned.
            LetTheComboLapse(score);

            // Assert
            Assert.Equal(330, score.TotalScore);
        }

        [Fact]
        public void BreakingTheCombo_ShouldClearTheVarietyBonus()
        {
            var score = new ScoreSystem();
            LandDistinct(score, 3);

            LetTheComboLapse(score);

            Assert.Equal(0, score.VarietyBonusPercent);
        }

        [Fact]
        public void TheVarietyBonus_ShouldClampAtTheLastConfiguredTier()
        {
            // A ladder shorter than the number of attack types must not read off its end.
            var score = new ScoreSystem(new ScoreConfig
            {
                VarietyBonusPercent = new List<int> { 0, 10 },
            });

            LandDistinct(score, 4);

            Assert.Equal(10, score.VarietyBonusPercent);
        }

        [Fact]
        public void AnEmptyVarietyLadder_ShouldPayNothing()
        {
            var score = new ScoreSystem(new ScoreConfig { VarietyBonusPercent = new List<int>() });

            LandDistinct(score, 4);

            Assert.Equal(0, score.VarietyBonusPercent);
        }

        // ---------- the "no damage taken" ladder

        /// <summary>
        /// A system whose only bonus is one milestone, with a flat multiplier: the score is then
        /// exactly (hits x 100) plus that milestone, which makes the arithmetic readable.
        /// </summary>
        private static ScoreSystem OneMilestone(int hits, int bonus) => new ScoreSystem(new ScoreConfig
        {
            MultiplierThresholds = new List<MultiplierTierDef>(),
            VarietyBonusPercent = new List<int>(),
            ComboEndBonuses = new List<ComboEndBonusDef>(),
            NoDamageBonuses = new List<NoDamageBonusDef>
            {
                new NoDamageBonusDef { Hits = hits, Bonus = bonus },
            },
        });

        [Theory]
        [InlineData(5, 100)]
        [InlineData(10, 300)]
        [InlineData(20, 750)]
        [InlineData(30, 1500)]
        public void ReachingANoDamageMilestone_ShouldPayItsBonus(int hits, int bonus)
        {
            // Arrange
            var score = OneMilestone(hits, bonus);

            // Act
            Land(score, hits);

            // Assert
            Assert.Equal(hits * 100 + bonus, score.TotalScore);
            Assert.Equal(hits, score.HitsWithoutDamage);
        }

        [Fact]
        public void ANoDamageMilestone_ShouldPayOnlyOnce()
        {
            var score = OneMilestone(5, 100);

            Land(score, 10);

            Assert.Equal(10 * 100 + 100, score.TotalScore);
        }

        [Fact]
        public void EveryMilestonePassed_ShouldBePaid()
        {
            // Arrange — the designed ladder, with everything else switched off.
            var score = new ScoreSystem(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>(),
                VarietyBonusPercent = new List<int>(),
                ComboEndBonuses = new List<ComboEndBonusDef>(),
            });

            // Act
            Land(score, 20);

            // Assert — 20 blows, plus the 5, 10 and 20 marks.
            Assert.Equal(20 * 100 + 100 + 300 + 750, score.TotalScore);
        }

        [Fact]
        public void ComboExpiring_ShouldNotResetTheNoDamageStreak()
        {
            // The streak measures going unhurt, which has nothing to do with the combo window:
            // otherwise the 30-blow mark would need 30 hits inside consecutive 2 s windows.
            var score = OneMilestone(5, 100);
            Land(score, 3);

            LetTheComboLapse(score);
            Land(score, 2);

            Assert.Equal(5, score.HitsWithoutDamage);
            Assert.Equal(5 * 100 + 100, score.TotalScore);
        }

        [Fact]
        public void NoDamageBonuses_ShouldBeFlat_NotScaledByTheMultiplier()
        {
            // Arrange — the designed ladder is on, so the 5th blow reaches x2 and the mark at 5.
            var score = new ScoreSystem(new ScoreConfig
            {
                VarietyBonusPercent = new List<int>(),
                ComboEndBonuses = new List<ComboEndBonusDef>(),
                NoDamageBonuses = new List<NoDamageBonusDef>
                {
                    new NoDamageBonusDef { Hits = 5, Bonus = 100 },
                },
            });

            // Act
            Land(score, 5);

            // Assert — 600 for the blows, plus a flat 100: not 200.
            Assert.Equal(700, score.TotalScore);
        }

        [Fact]
        public void UnsortedNoDamageMilestones_ShouldStillPayInOrder()
        {
            var score = new ScoreSystem(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>(),
                VarietyBonusPercent = new List<int>(),
                ComboEndBonuses = new List<ComboEndBonusDef>(),
                NoDamageBonuses = new List<NoDamageBonusDef>
                {
                    new NoDamageBonusDef { Hits = 3, Bonus = 30 },
                    new NoDamageBonusDef { Hits = 1, Bonus = 10 },
                    new NoDamageBonusDef { Hits = 2, Bonus = 20 },
                },
            });

            Land(score, 3);

            Assert.Equal(3 * 100 + 10 + 20 + 30, score.TotalScore);
        }

        [Fact]
        public void AnEmptyMilestoneLadder_ShouldPayNothing()
        {
            var score = new ScoreSystem(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>(),
                VarietyBonusPercent = new List<int>(),
                ComboEndBonuses = new List<ComboEndBonusDef>(),
                NoDamageBonuses = new List<NoDamageBonusDef>(),
            });

            Land(score, 30);

            Assert.Equal(30 * 100, score.TotalScore);
        }

        // ---------- taking damage

        [Fact]
        public void TakingDamage_ShouldCountTheHitTaken()
        {
            var score = new ScoreSystem();

            score.RegisterPlayerDamage();
            score.RegisterPlayerDamage();

            Assert.Equal(2, score.DamageTakenCount);
        }

        [Fact]
        public void TakingDamage_ShouldBreakTheCombo_ByDefault()
        {
            // Arrange
            var score = new ScoreSystem();
            Land(score, 12);

            // Act
            score.RegisterPlayerDamage();

            // Assert
            Assert.Equal(0, score.CurrentCombo);
            Assert.Equal(1, score.CurrentMultiplier);
            Assert.Equal(0f, score.ComboTimeRemaining);
        }

        [Fact]
        public void TakingDamage_ShouldResetTheNoDamageStreak()
        {
            var score = new ScoreSystem();
            Land(score, 4);

            score.RegisterPlayerDamage();

            Assert.Equal(0, score.HitsWithoutDamage);
        }

        [Fact]
        public void ANewStreakAfterDamage_ShouldPayTheMilestonesAgain()
        {
            // Arrange
            var score = OneMilestone(5, 100);
            Land(score, 5);
            Assert.Equal(5 * 100 + 100, score.TotalScore);

            // Act — get hit, then build the streak back up.
            score.RegisterPlayerDamage();
            Land(score, 5);

            // Assert — ten blows in all, and the mark earned twice.
            Assert.Equal(10 * 100 + 200, score.TotalScore);
        }

        [Fact]
        public void TakingDamage_ShouldForfeitTheVarietyBonus()
        {
            // Arrange — two types landed, so 10% is owing.
            var score = VarietyOnly();
            score.RegisterHit(AttackType.Normal);
            score.RegisterHit(AttackType.Heavy);

            // Act
            score.RegisterPlayerDamage();

            // Assert — getting hit costs the share; the blows themselves are kept.
            Assert.Equal(300, score.TotalScore);
            Assert.Equal(0, score.VarietyBonusPercent);
        }

        [Fact]
        public void TakingDamage_ShouldNotTouchTheTotalScore()
        {
            var score = WithoutBonuses();
            Land(score, 3);

            score.RegisterPlayerDamage();

            Assert.Equal(300, score.TotalScore);
        }

        [Fact]
        public void TakingDamage_ShouldOnlyReduceTheCombo_WhenResetComboOnDamageIsOff()
        {
            // Arrange
            var score = new ScoreSystem(new ScoreConfig
            {
                ResetComboOnDamage = false,
                ComboPenaltyOnDamage = 5,
            });
            Land(score, 12);

            // Act
            score.RegisterPlayerDamage();

            // Assert — docked, not broken.
            Assert.Equal(7, score.CurrentCombo);
            Assert.True(score.IsComboActive);
        }

        [Fact]
        public void AReducedCombo_ShouldRecomputeTheMultiplier()
        {
            var score = new ScoreSystem(new ScoreConfig
            {
                ResetComboOnDamage = false,
                ComboPenaltyOnDamage = 5,
            });
            Land(score, 12);
            Assert.Equal(3, score.CurrentMultiplier);

            score.RegisterPlayerDamage();

            Assert.Equal(2, score.CurrentMultiplier);
        }

        [Fact]
        public void TakingDamage_ShouldStillBreakTheCombo_WhenThePenaltyEmptiesIt()
        {
            var score = new ScoreSystem(new ScoreConfig
            {
                ResetComboOnDamage = false,
                ComboPenaltyOnDamage = 5,
            });
            Land(score, 3);

            score.RegisterPlayerDamage();

            Assert.Equal(0, score.CurrentCombo);
            Assert.False(score.IsComboActive);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void AnInvalidComboPenalty_ShouldClampToAtLeastOneHit(int penalty)
        {
            // A penalty of zero would make "reduce the combo" a no-op and the flag a lie.
            var score = new ScoreSystem(new ScoreConfig
            {
                ResetComboOnDamage = false,
                ComboPenaltyOnDamage = penalty,
            });
            Land(score, 5);

            score.RegisterPlayerDamage();

            Assert.Equal(4, score.CurrentCombo);
        }

        [Fact]
        public void TakingDamageWithNoActiveCombo_ShouldStillCountButNotScore()
        {
            var score = new ScoreSystem();

            score.RegisterPlayerDamage();

            Assert.Equal(1, score.DamageTakenCount);
            Assert.Equal(0, score.TotalScore);
            Assert.Equal(0, score.CurrentCombo);
        }

        // ---------- defeating an enemy

        [Theory]
        [InlineData("Normal", 500)]
        [InlineData("Strong", 1000)]
        [InlineData("Boss", 5000)]
        public void DefeatingAnEnemy_ShouldScoreItsTier(string enemyType, int expected)
        {
            // The enum is internal, so the tier travels as a string and is parsed in here.
            var type = (EnemyType)System.Enum.Parse(typeof(EnemyType), enemyType);
            var score = WithoutBonuses();

            score.RegisterEnemyDefeated(type);

            Assert.Equal(expected, score.TotalScore);
        }

        [Fact]
        public void TheEnemyBounty_ShouldBeMultipliedByTheCurrentMultiplier()
        {
            // Arrange — five blows reach x2 and are worth 600 between them.
            var score = WithoutBonuses();
            Land(score, 5);

            // Act
            score.RegisterEnemyDefeated(EnemyType.Normal);

            // Assert
            Assert.Equal(600 + 1000, score.TotalScore);
        }

        [Fact]
        public void DefeatingAnEnemy_ShouldCountItInTheCombo()
        {
            var score = new ScoreSystem();
            score.RegisterHit(AttackType.Normal);

            score.RegisterEnemyDefeated(EnemyType.Normal);
            score.RegisterEnemyDefeated(EnemyType.Normal);

            Assert.Equal(2, score.EnemiesDefeatedInCombo);
        }

        [Fact]
        public void DefeatingAnEnemy_ShouldNotIncrementTheCombo()
        {
            // The blow that killed already grew the combo; the bounty is not a second hit.
            var score = new ScoreSystem();
            score.RegisterHit(AttackType.Normal);

            score.RegisterEnemyDefeated(EnemyType.Normal);

            Assert.Equal(1, score.CurrentCombo);
        }

        [Fact]
        public void DefeatingAnEnemy_ShouldNotRefreshTheComboTimer()
        {
            // Arrange
            var score = new ScoreSystem();
            score.RegisterHit(AttackType.Normal);
            AdvanceSeconds(score, 1.0f);

            // Act
            score.RegisterEnemyDefeated(EnemyType.Normal);

            // Assert — still counting down from the blow, not topped back up.
            Assert.InRange(score.ComboTimeRemaining, 0.97f, 1.02f);
        }

        [Fact]
        public void EnemiesDefeatedInCombo_ShouldResetWhenTheComboBreaks()
        {
            var score = new ScoreSystem();
            score.RegisterHit(AttackType.Normal);
            score.RegisterEnemyDefeated(EnemyType.Normal);

            LetTheComboLapse(score);

            Assert.Equal(0, score.EnemiesDefeatedInCombo);
        }

        [Fact]
        public void DefeatingAnEnemyWithNoActiveCombo_ShouldScoreButNotCountInTheCombo()
        {
            // A body bowled over by a launched enemy can die with no combo running.
            var score = WithoutBonuses();

            score.RegisterEnemyDefeated(EnemyType.Normal);

            Assert.Equal(500, score.TotalScore);
            Assert.Equal(0, score.EnemiesDefeatedInCombo);
        }

        [Fact]
        public void AnUnknownEnemyType_ShouldScoreLikeANormalEnemy()
        {
            var score = WithoutBonuses();

            score.RegisterEnemyDefeated((EnemyType)42);

            Assert.Equal(500, score.TotalScore);
        }

        [Fact]
        public void ANegativeAttackType_ShouldScoreLikeANormalHit()
        {
            // An enum in C# holds any int, negative ones included: the lookup must not index below
            // the table either.
            var score = WithoutBonuses();

            score.RegisterHit((AttackType)(-1));

            Assert.Equal(100, score.TotalScore);
            Assert.Equal(1, score.CurrentCombo);
        }

        [Fact]
        public void ANegativeEnemyType_ShouldScoreLikeANormalEnemy()
        {
            var score = WithoutBonuses();

            score.RegisterEnemyDefeated((EnemyType)(-1));

            Assert.Equal(500, score.TotalScore);
        }

        [Fact]
        public void TheEnemyBounty_ShouldCountTowardsTheComboScore()
        {
            // The variety share is a percentage of what the sequence earned, bounties included.
            var score = WithoutBonuses();
            score.RegisterHit(AttackType.Normal);

            score.RegisterEnemyDefeated(EnemyType.Normal);

            Assert.Equal(600, score.CurrentComboScore);
        }

        // ---------- settling a combo

        /// <summary>
        /// A system whose only bonus is the combo-end ladder, with a flat multiplier: the score is
        /// then exactly (hits x 100) plus the payout.
        /// </summary>
        private static ScoreSystem EndBonusOnly() => new ScoreSystem(new ScoreConfig
        {
            MultiplierThresholds = new List<MultiplierTierDef>(),
            VarietyBonusPercent = new List<int>(),
            NoDamageBonuses = new List<NoDamageBonusDef>(),
        });

        [Theory]
        [InlineData(9, 0)]
        [InlineData(10, 500)]
        [InlineData(19, 500)]
        [InlineData(20, 1500)]
        [InlineData(29, 1500)]
        [InlineData(30, 4000)]
        [InlineData(45, 4000)]
        public void EndCombo_ShouldPayTheBonusOfItsComboTier(int hits, int bonus)
        {
            // Arrange
            var score = EndBonusOnly();
            Land(score, hits);

            // Act
            score.EndCombo();

            // Assert
            Assert.Equal(hits * 100 + bonus, score.TotalScore);
        }

        [Fact]
        public void EndCombo_ShouldResetTheComboAndTheMultiplier()
        {
            var score = new ScoreSystem();
            Land(score, 20);

            score.EndCombo();

            Assert.Equal(0, score.CurrentCombo);
            Assert.Equal(1, score.CurrentMultiplier);
            Assert.Equal(0, score.CurrentComboScore);
            Assert.Equal(0f, score.ComboTimeRemaining);
            Assert.False(score.IsComboActive);
        }

        [Fact]
        public void EndCombo_WithNoActiveCombo_ShouldBeANoOp()
        {
            var score = new ScoreSystem();

            score.EndCombo();

            Assert.Equal(0, score.TotalScore);
            Assert.Equal(0, score.CurrentCombo);
        }

        [Fact]
        public void EndComboTwice_ShouldOnlyPayTheBonusOnce()
        {
            // Arrange
            var score = EndBonusOnly();
            Land(score, 10);

            // Act
            score.EndCombo();
            score.EndCombo();

            // Assert
            Assert.Equal(10 * 100 + 500, score.TotalScore);
        }

        [Fact]
        public void AnExpiredCombo_ShouldPayTheSameEndBonusAsEndCombo()
        {
            // The window lapsing and settling by hand must be the same rule.
            var expired = EndBonusOnly();
            var settled = EndBonusOnly();
            Land(expired, 10);
            Land(settled, 10);

            LetTheComboLapse(expired);
            settled.EndCombo();

            Assert.Equal(settled.TotalScore, expired.TotalScore);
        }

        [Fact]
        public void TakingDamage_ShouldStillPayTheEndBonus()
        {
            // Damage forfeits the variety share, but the combo still happened and still pays out.
            var score = EndBonusOnly();
            Land(score, 10);

            score.RegisterPlayerDamage();

            Assert.Equal(10 * 100 + 500, score.TotalScore);
        }

        [Fact]
        public void EndCombo_ShouldPayTheVarietyShareAndTheTierBonusTogether()
        {
            // Arrange — a flat multiplier keeps the sum readable: 3 distinct types, 10 blows.
            var score = new ScoreSystem(new ScoreConfig
            {
                MultiplierThresholds = new List<MultiplierTierDef>(),
                NoDamageBonuses = new List<NoDamageBonusDef>(),
            });
            score.RegisterHit(AttackType.Normal);
            score.RegisterHit(AttackType.Heavy);
            score.RegisterHit(AttackType.Air);
            Land(score, 7);
            Assert.Equal(20, score.VarietyBonusPercent);

            // 100 + 200 + 250 + 7 x 100 = 1250 earned.
            Assert.Equal(1250, score.CurrentComboScore);

            // Act
            score.EndCombo();

            // Assert — 20% of 1250, plus the tier-10 payout.
            Assert.Equal(1250 + 250 + 500, score.TotalScore);
        }

        // ---------- the peaks of a run

        [Fact]
        public void HighestCombo_ShouldRecordThePeak_WhileTheComboIsStillLive()
        {
            // Banking the peak only at settle time would lose it when the run ends some other way
            // — the player being defeated, or the stage completing.
            var score = new ScoreSystem();

            Land(score, 12);

            Assert.Equal(12, score.HighestCombo);
        }

        [Fact]
        public void HighestMultiplier_ShouldRecordThePeak_WhileTheComboIsStillLive()
        {
            var score = new ScoreSystem();

            Land(score, 12);

            Assert.Equal(3, score.HighestMultiplier);
        }

        [Fact]
        public void ThePeaks_ShouldSurviveTheComboThatSetThem()
        {
            var score = new ScoreSystem();
            Land(score, 12);

            score.EndCombo();

            Assert.Equal(12, score.HighestCombo);
            Assert.Equal(3, score.HighestMultiplier);
            Assert.Equal(1, score.CurrentMultiplier);
        }

        [Fact]
        public void ThePeaks_ShouldSurviveLaterSmallerCombos()
        {
            var score = new ScoreSystem();
            Land(score, 12);
            score.EndCombo();

            Land(score, 3);

            Assert.Equal(12, score.HighestCombo);
            Assert.Equal(3, score.HighestMultiplier);
        }

        // ---------- starting a new run

        [Fact]
        public void Reset_ShouldClearEverything()
        {
            // Arrange — a run with something in every counter.
            var score = new ScoreSystem();
            LandDistinct(score, 4);
            Land(score, 20);
            score.RegisterEnemyDefeated(EnemyType.Boss);
            score.RegisterPlayerDamage();
            Land(score, 3);

            // Act
            score.Reset();

            // Assert
            Assert.Equal(0, score.TotalScore);
            Assert.Equal(0, score.CurrentCombo);
            Assert.Equal(0, score.CurrentComboScore);
            Assert.Equal(0, score.HitsWithoutDamage);
            Assert.Equal(0, score.EnemiesDefeatedInCombo);
            Assert.Equal(0, score.DamageTakenCount);
            Assert.Equal(0f, score.ComboTimeRemaining);
            Assert.Equal(0, score.VarietyBonusPercent);
            Assert.Equal(1, score.CurrentMultiplier);
            Assert.Equal(0, score.HighestCombo);
            Assert.Equal(1, score.HighestMultiplier);
            Assert.False(score.IsComboActive);
        }

        [Fact]
        public void Reset_ShouldNotPayTheEndBonusOfALiveCombo()
        {
            // Reset is a new run, not a settlement: whoever wants the payout calls EndCombo.
            var score = EndBonusOnly();
            Land(score, 20);

            score.Reset();

            Assert.Equal(0, score.TotalScore);
        }

        [Fact]
        public void AResetSystem_ShouldScoreLikeAFreshOne()
        {
            var used = WithoutBonuses();
            Land(used, 20);
            used.RegisterPlayerDamage();
            used.Reset();

            var fresh = WithoutBonuses();

            Land(used, 5);
            Land(fresh, 5);

            Assert.Equal(fresh.TotalScore, used.TotalScore);
            Assert.Equal(fresh.CurrentCombo, used.CurrentCombo);
            Assert.Equal(fresh.CurrentMultiplier, used.CurrentMultiplier);
        }

        [Fact]
        public void TheGetters_ShouldAgreeWithTheProperties()
        {
            // The requested method-style API must never be a second copy of the state.
            var score = new ScoreSystem();
            Land(score, 12);

            Assert.Equal(score.TotalScore, score.GetCurrentScore());
            Assert.Equal(score.CurrentCombo, score.GetCurrentCombo());
            Assert.Equal(score.CurrentMultiplier, score.GetCurrentMultiplier());
        }

        // ---------- the designed worked example

        [Fact]
        public void TheWorkedExampleFromTheDesign_ShouldScoreAsSpecified()
        {
            // The scenario the mechanic was specified with, checkpoint by checkpoint. Bonuses are
            // off so each figure is the one written in the design.
            var score = WithoutBonuses();

            // A first normal blow: +100, combo 1, x1.
            score.RegisterHit(AttackType.Normal);
            Assert.Equal(100, score.TotalScore);
            Assert.Equal(1, score.CurrentCombo);
            Assert.Equal(1, score.CurrentMultiplier);

            // Another: +100.
            score.RegisterHit(AttackType.Normal);
            Assert.Equal(200, score.TotalScore);

            // Keep attacking to combo 5, where the ladder reaches x2.
            Land(score, 3);
            Assert.Equal(5, score.CurrentCombo);
            Assert.Equal(2, score.CurrentMultiplier);

            // A heavy blow at x2: 200 x 2 = 400.
            long before = score.TotalScore;
            score.RegisterHit(AttackType.Heavy);
            Assert.Equal(400, score.TotalScore - before);

            // On to combo 12, where the ladder reaches x3.
            Land(score, 6);
            Assert.Equal(12, score.CurrentCombo);
            Assert.Equal(3, score.CurrentMultiplier);

            // An enemy goes down: 500 x 3 = 1500.
            before = score.TotalScore;
            score.RegisterEnemyDefeated(EnemyType.Normal);
            Assert.Equal(1500, score.TotalScore - before);

            // Still unhurt, on to combo 20 and x4.
            Land(score, 8);
            Assert.Equal(20, score.CurrentCombo);
            Assert.Equal(4, score.CurrentMultiplier);

            // Another enemy: 500 x 4 = 2000.
            before = score.TotalScore;
            score.RegisterEnemyDefeated(EnemyType.Normal);
            Assert.Equal(2000, score.TotalScore - before);

            // The player is hit: the combo is settled, the temporaries cleared, the total kept.
            long banked = score.TotalScore;
            score.RegisterPlayerDamage();
            Assert.Equal(banked, score.TotalScore);
            Assert.Equal(0, score.CurrentCombo);
            Assert.Equal(1, score.CurrentMultiplier);
            Assert.Equal(0, score.CurrentComboScore);
            Assert.Equal(20, score.HighestCombo);
            Assert.Equal(4, score.HighestMultiplier);
        }
    }
}
