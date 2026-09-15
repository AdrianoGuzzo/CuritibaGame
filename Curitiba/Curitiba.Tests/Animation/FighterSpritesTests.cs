using System;
using System.Collections.Generic;
using Curitiba.Core.BeatEmUp;
using Curitiba.Core.BeatEmUp.Combat;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.Animation
{
    /// <summary>
    /// The state-to-sprite-strip convention. The drawing itself needs a GPU, but which strip a state
    /// asks for is plain data, and getting it wrong means a fighter silently renders as a placeholder.
    /// </summary>
    public class FighterSpritesTests
    {
        [Theory]
        [InlineData("Idle", "Idle")]
        [InlineData("Walk", "Walk")]
        [InlineData("Dash", "Dash")]
        [InlineData("Attack", "Attack")]
        [InlineData("Attack2", "Attack2")]
        [InlineData("Attack3", "Kick")]
        [InlineData("Jump", "Jump")]
        [InlineData("JumpAttack", "JumpKick")]
        [InlineData("Hit", "Hit")]
        [InlineData("KnockedDown", "Knockdown")]
        [InlineData("Dead", "Knockdown")]
        public void SofiaState_ShouldMapToItsStrip(string state, string asset)
        {
            Assert.Equal(asset, FighterSprites.Sofia[ParseState(state)]);
        }

        [Theory]
        [InlineData("Idle", "Idle")]
        [InlineData("Walk", "Walk")]
        [InlineData("Attack", "Attack")]
        [InlineData("Hit", "Hit")]
        [InlineData("Thrown", "ThrowStrongHit")]
        [InlineData("KnockedDown", "Death")]
        [InlineData("Dead", "Death")]
        public void PiaLocoState_ShouldMapToItsStrip(string state, string asset)
        {
            Assert.Equal(asset, FighterSprites.PiaLoco[ParseState(state)]);
        }

        [Theory]
        [InlineData("Start", "JumpStart")]
        [InlineData("Rise", "JumpUp")]
        [InlineData("Apex", "JumpApex")]
        [InlineData("Fall", "JumpFall")]
        [InlineData("Land", "JumpLand")]
        public void SofiaJumpPhase_ShouldMapToItsStrip(string phase, string asset)
        {
            var parsed = (JumpPhase)Enum.Parse(typeof(JumpPhase), phase);

            Assert.Equal(asset, FighterSprites.SofiaJumpPhases[parsed]);
        }

        [Fact]
        public void SofiaJumpPhases_ShouldCoverEveryPhase()
        {
            foreach (JumpPhase phase in Enum.GetValues(typeof(JumpPhase)))
                Assert.True(FighterSprites.SofiaJumpPhases.ContainsKey(phase), $"no strip for {phase}");
        }

        [Fact]
        public void DeathAndKnockdown_ShouldShareAStrip_ForBothSets()
        {
            // The body stays on the ground either way, so one strip serves both.
            Assert.Equal(FighterSprites.Sofia[ParseState("KnockedDown")], FighterSprites.Sofia[ParseState("Dead")]);
            Assert.Equal(FighterSprites.PiaLoco[ParseState("KnockedDown")], FighterSprites.PiaLoco[ParseState("Dead")]);
        }

        [Fact]
        public void EverySofiaComboState_ShouldHaveAStrip()
        {
            // The real coupling worth guarding: adding a link to Sofia's chain with a new state, and
            // forgetting the strip, makes that swing render as a placeholder with no other symptom.
            ComboChainDef chain = CombatDefaults.BuildChain(FighterTuning.SofiaDefaults());

            for (int i = 0; i < chain.Count; i++)
            {
                Assert.True(FighterSprites.Sofia.ContainsKey(chain[i].State),
                    $"combo move '{chain[i].Id}' plays {chain[i].State}, which has no strip in FighterSprites.Sofia");
            }
        }

        [Fact]
        public void EveryPiaLocoComboState_ShouldHaveAStrip()
        {
            ComboChainDef chain = CombatDefaults.BuildChain(FighterTuning.PiaLocoDefaults());

            for (int i = 0; i < chain.Count; i++)
            {
                Assert.True(FighterSprites.PiaLoco.ContainsKey(chain[i].State),
                    $"combo move '{chain[i].Id}' plays {chain[i].State}, which has no strip in FighterSprites.PiaLoco");
            }
        }

        [Fact]
        public void SofiaMap_ShouldCoverEveryStateSheCanReach()
        {
            // Sofia has no Thrown state: only the finisher launches, and only enemies are launched.
            string[] unreachable = { "Thrown" };

            AssertCoversAllStatesExcept(FighterSprites.Sofia, unreachable, "Sofia");
        }

        [Fact]
        public void PiaLocoMap_ShouldCoverEveryStateItCanReach()
        {
            // The mook has no dash, no combo links beyond the first, and no hop.
            string[] unreachable = { "Dash", "Attack2", "Attack3", "Jump", "JumpAttack" };

            AssertCoversAllStatesExcept(FighterSprites.PiaLoco, unreachable, "PiaLoco");
        }

        [Fact]
        public void HeadlessAnimator_ShouldReportNoSprites_AndFallBackToPlaceholders()
        {
            // The graceful-degradation contract the whole test suite leans on.
            var animator = new FighterAnimator(HeadlessContent.Create(), null, "Sofia",
                Microsoft.Xna.Framework.Color.White, FighterSprites.Sofia, FighterSprites.SofiaJumpPhases);

            Assert.False(animator.HasSprites);
        }

        private static void AssertCoversAllStatesExcept(IReadOnlyDictionary<FighterState, string> map,
                                                        string[] unreachable, string setName)
        {
            foreach (FighterState state in Enum.GetValues(typeof(FighterState)))
            {
                if (Array.IndexOf(unreachable, state.ToString()) >= 0)
                    continue;

                Assert.True(map.ContainsKey(state), $"{setName} has no strip for {state}");
            }
        }

        private static FighterState ParseState(string name) =>
            (FighterState)Enum.Parse(typeof(FighterState), name);
    }
}
