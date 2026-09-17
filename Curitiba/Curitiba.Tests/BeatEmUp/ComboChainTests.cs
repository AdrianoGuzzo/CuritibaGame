using System.Collections.Generic;
using Curitiba.Core.BeatEmUp;
using Curitiba.Core.BeatEmUp.Combat;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// Turning the JSON <c>comboChain[]</c> into the runtime chain a fighter swings, and the little
    /// input buffer that makes chaining feel responsive.
    /// </summary>
    public class ComboChainTests
    {
        [Fact]
        public void BuildChain_ShouldSynthesiseASingleSwing_WhenNoComboChainIsAuthored()
        {
            // Arrange — pre-combo-chain JSON only had the scalar timings.
            var tuning = new FighterTuning
            {
                ComboChain = null,
                AttackWindup = 0.12f,
                AttackActive = 0.10f,
                AttackRecovery = 0.18f,
                AttackDamage = 7,
                AttackReach = 55,
            };

            // Act
            ComboChainDef chain = CombatDefaults.BuildChain(tuning);

            // Assert
            Assert.Equal(1, chain.Count);
            ComboMove move = chain[0];
            Assert.Equal("attack", move.Id);
            Assert.Equal(FighterState.Attack, move.State);
            Assert.Equal(0.12f, move.Startup);
            Assert.Equal(0.10f, move.Active);
            Assert.Equal(0.18f, move.Recovery);
            Assert.Equal(7, move.Damage);
            Assert.Equal(55, move.Reach);
            Assert.False(move.RequiresHitConfirm);
            Assert.False(move.Launches);
        }

        [Fact]
        public void BuildChain_ShouldSynthesiseASingleSwing_WhenTheComboChainIsEmpty()
        {
            var tuning = new FighterTuning { ComboChain = new List<ComboMoveDef>() };

            ComboChainDef chain = CombatDefaults.BuildChain(tuning);

            Assert.Equal(1, chain.Count);
        }

        [Fact]
        public void SynthesisedSwing_ShouldNotBeCancellable()
        {
            var tuning = new FighterTuning { ComboChain = null };

            ComboMove move = CombatDefaults.BuildChain(tuning)[0];

            // A cancel point at the very end means "the swing must finish".
            Assert.Equal(move.TotalDuration, move.CancelPoint);
        }

        [Fact]
        public void BuildChain_ShouldMapEveryAuthoredMove()
        {
            ComboChainDef chain = CombatDefaults.BuildChain(FighterTuning.SofiaDefaults());

            Assert.Equal(4, chain.Count);
            Assert.Equal("punch1", chain[0].Id);
            Assert.Equal("punch2", chain[1].Id);
            Assert.Equal("punch3", chain[2].Id);
            Assert.Equal("kick", chain[3].Id);
        }

        // FighterState is internal, so the theory data names the state and the test maps it.
        [Theory]
        [InlineData(0, "Attack", 10, 48)]
        [InlineData(1, "Attack", 10, 48)]
        [InlineData(2, "Attack2", 12, 50)]
        [InlineData(3, "Attack3", 22, 62)]
        public void SofiaChain_ShouldKeepItsAuthoredStatsPerLink(int index, string state, int damage, int reach)
        {
            ComboChainDef chain = CombatDefaults.BuildChain(FighterTuning.SofiaDefaults());

            Assert.Equal(state, chain[index].State.ToString());
            Assert.Equal(damage, chain[index].Damage);
            Assert.Equal(reach, chain[index].Reach);
        }

        [Fact]
        public void SofiaChain_ShouldRequireHitConfirmOnEveryLink()
        {
            ComboChainDef chain = CombatDefaults.BuildChain(FighterTuning.SofiaDefaults());

            for (int i = 0; i < chain.Count; i++)
                Assert.True(chain[i].RequiresHitConfirm, $"link {i} should require a hit confirm");
        }

        [Fact]
        public void OnlyTheFinisher_ShouldLaunch()
        {
            ComboChainDef chain = CombatDefaults.BuildChain(FighterTuning.SofiaDefaults());

            Assert.False(chain[0].Launches);
            Assert.False(chain[1].Launches);
            Assert.False(chain[2].Launches);
            Assert.True(chain[3].Launches);
        }

        [Fact]
        public void Finisher_ShouldNotBeCancellable_BecauseItsCancelPointIsZero()
        {
            ComboChainDef chain = CombatDefaults.BuildChain(FighterTuning.SofiaDefaults());
            ComboMove kick = chain[3];

            // Authored as 0, which means "no cancel" and resolves to the full duration.
            Assert.Equal(kick.TotalDuration, kick.CancelPoint);
            Assert.Equal(0.10f + 0.08f + 0.20f, kick.TotalDuration, 5);
        }

        [Fact]
        public void CancelPoint_ShouldBeClampedUpToTheEndOfTheActiveFrames()
        {
            // Arrange — a cancel authored before the blow can even land.
            var tuning = ChainOf(new ComboMoveDef
            {
                Startup = 0.10f, Active = 0.10f, Recovery = 0.20f, CancelPoint = 0.01f,
            });

            // Act
            ComboMove move = CombatDefaults.BuildChain(tuning)[0];

            // Assert — a cancel may never cut the active frames short.
            Assert.Equal(0.20f, move.CancelPoint, 5);
        }

        [Fact]
        public void CancelPoint_ShouldBeClampedDownToTheSwingDuration()
        {
            var tuning = ChainOf(new ComboMoveDef
            {
                Startup = 0.10f, Active = 0.10f, Recovery = 0.20f, CancelPoint = 99f,
            });

            ComboMove move = CombatDefaults.BuildChain(tuning)[0];

            Assert.Equal(move.TotalDuration, move.CancelPoint);
        }

        [Fact]
        public void CancelPoint_ShouldBeKept_WhenItIsInsideTheLegalWindow()
        {
            var tuning = ChainOf(new ComboMoveDef
            {
                Startup = 0.07f, Active = 0.06f, Recovery = 0.11f, CancelPoint = 0.14f,
            });

            ComboMove move = CombatDefaults.BuildChain(tuning)[0];

            Assert.Equal(0.14f, move.CancelPoint, 5);
        }

        [Theory]
        [InlineData("Attack")]
        [InlineData("Attack2")]
        [InlineData("Attack3")]
        [InlineData("JumpAttack")]
        public void MoveState_ShouldBeParsedFromItsName(string name)
        {
            var tuning = ChainOf(new ComboMoveDef { State = name });

            Assert.Equal(name, CombatDefaults.BuildChain(tuning)[0].State.ToString());
        }

        [Theory]
        [InlineData("attack")]      // Enum.TryParse here is case-SENSITIVE, so lower case does not match.
        [InlineData("NotAState")]
        [InlineData("")]
        public void UnknownMoveState_ShouldFallBackToAttack(string name)
        {
            var tuning = ChainOf(new ComboMoveDef { State = name });

            Assert.Equal(FighterState.Attack, CombatDefaults.BuildChain(tuning)[0].State);
        }

        [Fact]
        public void TotalDuration_ShouldBeTheSumOfThePhases()
        {
            var move = new ComboMove("x", FighterState.Attack, 0.1f, 0.2f, 0.3f, 1, 1, 0f, 0f, 0f, false, false);

            Assert.Equal(0.6f, move.TotalDuration, 5);
        }

        private static FighterTuning ChainOf(ComboMoveDef move) =>
            new FighterTuning { ComboChain = new List<ComboMoveDef> { move } };

        // ---------------------------------------------------------------- scoring weight

        private static FighterTuning OneMove(string scoreType = null, bool launches = false) =>
            new FighterTuning
            {
                ComboChain = new List<ComboMoveDef>
                {
                    new ComboMoveDef { Id = "swing", ScoreType = scoreType, Launches = launches },
                },
            };

        [Fact]
        public void BuildChain_ShouldScoreAnUnmarkedMoveAsANormalBlow()
        {
            ComboChainDef chain = CombatDefaults.BuildChain(OneMove());

            Assert.Equal("Normal", chain[0].ScoreType.ToString());
        }

        [Fact]
        public void BuildChain_ShouldScoreAnUnmarkedLaunchingMoveAsAFinisher()
        {
            // The blow that ends a string is the finisher by definition; making every stage author
            // it twice would just be one more thing to keep in sync.
            ComboChainDef chain = CombatDefaults.BuildChain(OneMove(launches: true));

            Assert.Equal("Finisher", chain[0].ScoreType.ToString());
        }

        [Theory]
        [InlineData("normal", "Normal")]
        [InlineData("heavy", "Heavy")]
        [InlineData("air", "Air")]
        [InlineData("finisher", "Finisher")]
        [InlineData("Heavy", "Heavy")]
        [InlineData("HEAVY", "Heavy")]
        [InlineData(" heavy ", "Heavy")]
        public void BuildChain_ShouldParseTheAuthoredScoreType(string authored, string expected)
        {
            // The JSON is camelCase, so the lower-case spelling has to work.
            ComboChainDef chain = CombatDefaults.BuildChain(OneMove(authored));

            Assert.Equal(expected, chain[0].ScoreType.ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("gigantic")]
        [InlineData("2")]
        public void BuildChain_ShouldScoreAnUnreadableScoreTypeAsANormalBlow(string authored)
        {
            // "2" matters: a numeric string parses straight into an enum, which would silently
            // make a typo mean "air".
            ComboChainDef chain = CombatDefaults.BuildChain(OneMove(authored));

            Assert.Equal("Normal", chain[0].ScoreType.ToString());
        }

        [Fact]
        public void AnAuthoredScoreType_ShouldWinOverTheLaunchDerivedOne()
        {
            ComboChainDef chain = CombatDefaults.BuildChain(OneMove("heavy", launches: true));

            Assert.Equal("Heavy", chain[0].ScoreType.ToString());
        }

        [Fact]
        public void ASynthesisedSingleSwing_ShouldScoreAsANormalBlow()
        {
            // Pre-combo-chain JSON has no move to mark, so the fallback swing has to pick a weight.
            ComboChainDef chain = CombatDefaults.BuildChain(new FighterTuning { ComboChain = null });

            Assert.Equal("Normal", chain[0].ScoreType.ToString());
        }

        [Fact]
        public void SofiasChain_ShouldRunNormalNormalHeavyFinisher()
        {
            ComboChainDef chain = CombatDefaults.BuildChain(FighterTuning.SofiaDefaults());

            Assert.Equal("Normal", chain[0].ScoreType.ToString());
            Assert.Equal("Normal", chain[1].ScoreType.ToString());
            Assert.Equal("Heavy", chain[2].ScoreType.ToString());
            Assert.Equal("Finisher", chain[3].ScoreType.ToString());
        }

    }
}
