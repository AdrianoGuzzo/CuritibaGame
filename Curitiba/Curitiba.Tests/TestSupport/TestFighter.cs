using Curitiba.Core.BeatEmUp;
using Microsoft.Xna.Framework;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// A bare <see cref="Fighter"/> for exercising the base state machine on its own, without
    /// Sofia's input handling or the Pia Loco's AI on top of it.
    /// </summary>
    /// <remarks>
    /// It also promotes the protected move starters to public, so a test can drive a jump, a dash or
    /// a swing directly instead of going through a keyboard or an AI decision.
    /// </remarks>
    internal sealed class TestFighter : Fighter
    {
        private readonly FighterState defeatedState;

        public TestFighter(FighterTuning tuning = null, int hitsToKnockdown = 0,
                           FighterState defeatedState = FighterState.Dead)
        {
            this.defeatedState = defeatedState;

            ApplyTuning(tuning ?? FighterTuning.PiaLocoDefaults());
            this.hitsToKnockdown = hitsToKnockdown;

            animator = new FighterAnimator(HeadlessContent.Create(), null, "TestFighter", Color.White,
                FighterSprites.PiaLoco, null, Scale);
            Name = "Test";
        }

        protected override FighterState OnDefeatedState() => defeatedState;

        /// <summary>Starts a swing immediately, bypassing the input buffer.</summary>
        public void BeginAttack() => StartAttack();

        /// <summary>Starts a hop with the given planar velocity.</summary>
        public void BeginJump(Vector2 planarVelocity = default) => StartJump(planarVelocity);

        /// <summary>Starts a dash in the given direction (zero dashes along <see cref="Fighter.Facing"/>).</summary>
        public void BeginDash(Vector2 direction = default) => StartDash(direction);

        /// <summary>Attempts the air attack; only legal mid-hop.</summary>
        public void BeginJumpAttack() => StartJumpAttack();

        /// <summary>Attempts to cancel a dash into a jump.</summary>
        public bool TryCancelDashIntoJump() => TryDashCancelJump();

        /// <summary>Sets the idle/walk locomotion, which the base class ignores while busy.</summary>
        public void SetLocomotionState(FighterState locomotion) => SetLocomotion(locomotion);
    }
}
