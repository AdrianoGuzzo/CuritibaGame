using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Microsoft.Xna.Framework;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// The shared fighter state machine: taking damage, staggering, being knocked down, dying, and
    /// the timings that move between those states.
    /// </summary>
    public class FighterStateMachineTests
    {
        private static readonly Vector2 Knockback = new Vector2(220f, -40f);

        private static TestFighter NewFighter(int maxHealth = 100, int hitsToKnockdown = 0,
                                              float invulnerabilityOnHit = 0f)
        {
            var tuning = FighterTuning.PiaLocoDefaults();
            tuning.MaxHealth = maxHealth;
            tuning.InvulnerabilityOnHit = invulnerabilityOnHit;
            return new TestFighter(tuning, hitsToKnockdown);
        }

        // ---------------------------------------------------------------- health

        [Fact]
        public void NewFighter_ShouldStartIdleAtFullHealth()
        {
            var fighter = NewFighter();

            Assert.Equal(FighterState.Idle, fighter.State);
            Assert.Equal(100, fighter.Health);
            Assert.Equal(100, fighter.MaxHealth);
            Assert.True(fighter.IsAlive);
            Assert.False(fighter.IsDefeated);
        }

        [Fact]
        public void TakeDamage_ShouldReduceHealth()
        {
            var fighter = NewFighter();

            fighter.TakeDamage(30, Knockback);

            Assert.Equal(70, fighter.Health);
        }

        [Fact]
        public void TakeDamage_ShouldNeverDriveHealthBelowZero()
        {
            var fighter = NewFighter(maxHealth: 10);

            fighter.TakeDamage(999, Knockback);

            Assert.Equal(0, fighter.Health);
        }

        [Fact]
        public void HealthReachingZero_ShouldDefeatTheFighter()
        {
            var fighter = NewFighter(maxHealth: 30);

            fighter.TakeDamage(30, Knockback);

            Assert.Equal(FighterState.Dead, fighter.State);
            Assert.True(fighter.IsDefeated);
            Assert.False(fighter.IsAlive);
        }

        [Fact]
        public void DefeatedState_ShouldBeOverridable_SoThePlayerIsKnockedDownRatherThanKilled()
        {
            var fighter = new TestFighter(defeatedState: FighterState.KnockedDown);

            fighter.TakeDamage(fighter.Health, Knockback);

            Assert.Equal(FighterState.KnockedDown, fighter.State);
        }

        [Fact]
        public void NonLethalDamage_ShouldStagger()
        {
            var fighter = NewFighter();

            fighter.TakeDamage(10, Knockback);

            Assert.Equal(FighterState.Hit, fighter.State);
        }

        // ---------------------------------------------------------------- no double-dipping

        [Fact]
        public void DeadFighter_ShouldTakeNoFurtherDamage()
        {
            var fighter = NewFighter(maxHealth: 10);
            fighter.TakeDamage(10, Knockback);
            Assert.Equal(FighterState.Dead, fighter.State);

            fighter.TakeDamage(50, Knockback);

            Assert.Equal(0, fighter.Health);
        }

        [Fact]
        public void KnockedDownFighter_ShouldTakeNoDamage_WhileItIsOnTheFloor()
        {
            var fighter = NewFighter(hitsToKnockdown: 1);
            fighter.TakeDamage(5, Knockback);
            Assert.Equal(FighterState.KnockedDown, fighter.State);
            int health = fighter.Health;

            fighter.TakeDamage(5, Knockback);

            Assert.Equal(health, fighter.Health);
        }

        [Fact]
        public void InvulnerableFighter_ShouldTakeNoDamage()
        {
            var fighter = NewFighter(invulnerabilityOnHit: 0.25f);
            fighter.TakeDamage(10, Knockback);
            Assert.True(fighter.IsInvulnerable);
            int health = fighter.Health;

            fighter.TakeDamage(10, Knockback);

            Assert.Equal(health, fighter.Health);
        }

        [Fact]
        public void Invulnerability_ShouldLapseAfterItsWindow()
        {
            var fighter = NewFighter(invulnerabilityOnHit: 0.25f);
            fighter.TakeDamage(10, Knockback);

            Frames.AdvanceSeconds(fighter, 0.3f);

            Assert.False(fighter.IsInvulnerable);
        }

        // ---------------------------------------------------------------- poise / knockdown

        [Fact]
        public void PoiseIsDisabled_WhenHitsToKnockdownIsZero()
        {
            var fighter = NewFighter(hitsToKnockdown: 0);

            for (int i = 0; i < 5; i++)
            {
                fighter.TakeDamage(1, Knockback);
                Frames.AdvanceSeconds(fighter, 0.4f);
            }

            Assert.NotEqual(FighterState.KnockedDown, fighter.State);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void Fighter_ShouldBeKnockedDown_OnTheNthBlowInARow(int hitsToKnockdown)
        {
            var fighter = NewFighter(maxHealth: 1000, hitsToKnockdown: hitsToKnockdown);

            for (int i = 1; i < hitsToKnockdown; i++)
            {
                fighter.TakeDamage(1, Knockback);
                Assert.Equal(FighterState.Hit, fighter.State);
                Frames.AdvanceSeconds(fighter, 0.4f);
            }

            fighter.TakeDamage(1, Knockback);

            Assert.Equal(FighterState.KnockedDown, fighter.State);
        }

        [Fact]
        public void PoiseCount_ShouldResetAfterTheWindow()
        {
            var fighter = NewFighter(maxHealth: 1000, hitsToKnockdown: 2);
            fighter.TakeDamage(1, Knockback);

            // Wait out the poise window, so the next blow starts a fresh count.
            Frames.AdvanceSeconds(fighter, 2f);
            fighter.TakeDamage(1, Knockback);

            Assert.Equal(FighterState.Hit, fighter.State);
        }

        [Fact]
        public void ForcedKnockdown_ShouldBypassPoiseEntirely()
        {
            var fighter = NewFighter(maxHealth: 1000, hitsToKnockdown: 0);

            fighter.TakeDamage(1, Knockback, HitReaction.Knockdown);

            Assert.Equal(FighterState.KnockedDown, fighter.State);
        }

        [Fact]
        public void KnockedDownFighter_ShouldGetBackUp_WhenItStillHasHealth()
        {
            var fighter = NewFighter(maxHealth: 1000, hitsToKnockdown: 1);
            fighter.TakeDamage(1, Knockback);
            Assert.Equal(FighterState.KnockedDown, fighter.State);

            Frames.AdvanceSeconds(fighter, 1f);

            Assert.Equal(FighterState.Idle, fighter.State);
        }

        [Fact]
        public void RisenFighter_ShouldBeBrieflyInvulnerable()
        {
            var fighter = NewFighter(maxHealth: 1000, hitsToKnockdown: 1);
            fighter.TakeDamage(1, Knockback);
            Frames.AdvanceSeconds(fighter, 1f);

            Assert.True(fighter.IsInvulnerable, "get-up invulnerability stops a wake-up trap");
        }

        [Fact]
        public void DefeatedPlayer_ShouldStayDown_Forever()
        {
            // Sofia is knocked down rather than killed, and with no health left she never rises.
            // This is what lets the arena wait 1.2s and then declare the run over.
            var fighter = new TestFighter(defeatedState: FighterState.KnockedDown);
            fighter.TakeDamage(fighter.Health, Knockback);

            Frames.AdvanceSeconds(fighter, 10f);

            Assert.Equal(FighterState.KnockedDown, fighter.State);
        }

        // ---------------------------------------------------------------- death / expiry

        [Fact]
        public void DeadFighter_ShouldNotExpireImmediately()
        {
            var fighter = NewFighter(maxHealth: 5);

            fighter.TakeDamage(5, Knockback);

            Assert.False(fighter.IsExpired);
        }

        [Fact]
        public void DeadFighter_ShouldExpire_AfterItsDeathAndBlinkWindows()
        {
            var tuning = FighterTuning.PiaLocoDefaults();
            tuning.MaxHealth = 5;
            tuning.DeathDuration = 0.7f;
            tuning.DeathBlinkDuration = 0.6f;
            var fighter = new TestFighter(tuning);
            fighter.TakeDamage(5, Knockback);

            Frames.AdvanceSeconds(fighter, 1.2f);
            Assert.False(fighter.IsExpired);

            Frames.AdvanceSeconds(fighter, 0.2f);
            Assert.True(fighter.IsExpired);
        }

        [Fact]
        public void DeadFighter_ShouldNeverLeaveTheDeadState()
        {
            var fighter = NewFighter(maxHealth: 5);
            fighter.TakeDamage(5, Knockback);

            Frames.AdvanceSeconds(fighter, 10f);

            Assert.Equal(FighterState.Dead, fighter.State);
        }

        // ---------------------------------------------------------------- reaction precedence

        [Fact]
        public void Launch_ShouldFlingTheTarget_RatherThanStaggerIt()
        {
            var fighter = NewFighter(maxHealth: 1000);

            fighter.TakeDamage(10, new Vector2(600f, -60f), HitReaction.Launch);

            Assert.Equal(FighterState.Thrown, fighter.State);
            Assert.True(fighter.IsBeingThrown);
        }

        [Fact]
        public void Launch_ShouldWinOverLethalDamage_SoTheBodyFliesBeforeItDies()
        {
            var fighter = NewFighter(maxHealth: 10);

            fighter.TakeDamage(999, new Vector2(600f, -60f), HitReaction.Launch);

            Assert.Equal(FighterState.Thrown, fighter.State);
            Assert.Equal(0, fighter.Health);
        }

        [Fact]
        public void LaunchedFighterWithNoHealth_ShouldDieOnLanding()
        {
            var fighter = NewFighter(maxHealth: 10);
            fighter.TakeDamage(999, new Vector2(600f, -60f), HitReaction.Launch);

            Frames.AdvanceSeconds(fighter, 0.6f);

            Assert.Equal(FighterState.Dead, fighter.State);
        }

        [Fact]
        public void LaunchedFighterWithHealthLeft_ShouldLandKnockedDown()
        {
            var fighter = NewFighter(maxHealth: 1000);
            fighter.TakeDamage(10, new Vector2(600f, -60f), HitReaction.Launch);

            Frames.AdvanceSeconds(fighter, 0.6f);

            Assert.Equal(FighterState.KnockedDown, fighter.State);
        }

        [Fact]
        public void LethalDamage_ShouldWinOverAForcedKnockdown()
        {
            var fighter = NewFighter(maxHealth: 10);

            fighter.TakeDamage(10, Knockback, HitReaction.Knockdown);

            Assert.Equal(FighterState.Dead, fighter.State);
        }

        [Fact]
        public void ThrownFighter_ShouldTakeNoFurtherDamage_WhileInFlight()
        {
            var fighter = NewFighter(maxHealth: 1000);
            fighter.TakeDamage(10, new Vector2(600f, -60f), HitReaction.Launch);
            int health = fighter.Health;

            fighter.TakeDamage(50, Knockback);

            Assert.Equal(health, fighter.Health);
        }

        [Fact]
        public void ThrownFighter_ShouldTravelInTheLaunchDirection()
        {
            var fighter = NewFighter(maxHealth: 1000);
            fighter.Position = new Vector2(100f, 400f);

            fighter.TakeDamage(10, new Vector2(600f, 0f), HitReaction.Launch);
            Frames.AdvanceSeconds(fighter, 0.25f);

            Assert.True(fighter.Position.X > 100f, "a launched body must actually fly backwards");
            Assert.Equal(1, fighter.ThrowDirectionX);
        }

        [Fact]
        public void DampenThrow_ShouldBleedOffMostOfTheFlight()
        {
            var fast = NewFighter(maxHealth: 1000);
            var dampened = NewFighter(maxHealth: 1000);
            fast.Position = dampened.Position = new Vector2(100f, 400f);
            fast.TakeDamage(10, new Vector2(600f, 0f), HitReaction.Launch);
            dampened.TakeDamage(10, new Vector2(600f, 0f), HitReaction.Launch);

            dampened.DampenThrow();
            Frames.AdvanceSeconds(fast, 0.2f);
            Frames.AdvanceSeconds(dampened, 0.2f);

            Assert.True(dampened.Position.X < fast.Position.X,
                "a body that bowled into someone should settle near the pile");
        }

        [Fact]
        public void DampenThrow_ShouldDoNothing_WhenTheFighterIsNotAirborne()
        {
            var fighter = NewFighter();
            Vector2 before = fighter.Position;

            fighter.DampenThrow();
            Frames.AdvanceSeconds(fighter, 0.2f);

            Assert.Equal(before, fighter.Position);
        }

        // ---------------------------------------------------------------- stagger recovery

        [Fact]
        public void StaggeredFighter_ShouldRecoverToIdle()
        {
            var tuning = FighterTuning.PiaLocoDefaults();
            tuning.MaxHealth = 1000;
            tuning.HitDuration = 0.3f;
            var fighter = new TestFighter(tuning);
            fighter.TakeDamage(10, Knockback);

            Frames.AdvanceSeconds(fighter, 0.35f);

            Assert.Equal(FighterState.Idle, fighter.State);
        }

        [Fact]
        public void StaggeredFighter_ShouldStayStaggered_UntilItsWindowEnds()
        {
            var tuning = FighterTuning.PiaLocoDefaults();
            tuning.MaxHealth = 1000;
            tuning.HitDuration = 0.3f;
            var fighter = new TestFighter(tuning);
            fighter.TakeDamage(10, Knockback);

            Frames.AdvanceSeconds(fighter, 0.2f);

            Assert.Equal(FighterState.Hit, fighter.State);
        }

        // ---------------------------------------------------------------- timers & locomotion

        [Fact]
        public void StateTimer_ShouldAdvanceWithTheFrame()
        {
            var fighter = NewFighter();

            fighter.Update(Frames.Step(0.1f));

            Assert.Equal(0.1f, fighter.StateTimer, 4);
        }

        [Fact]
        public void StateTimer_ShouldResetWhenTheStateChanges()
        {
            var fighter = NewFighter(maxHealth: 1000);
            Frames.AdvanceSeconds(fighter, 1f);

            fighter.TakeDamage(1, Knockback);

            Assert.Equal(0f, fighter.StateTimer);
        }

        [Fact]
        public void SetLocomotion_ShouldSwitchBetweenIdleAndWalk()
        {
            var fighter = NewFighter();

            fighter.SetLocomotionState(FighterState.Walk);
            Assert.Equal(FighterState.Walk, fighter.State);

            fighter.SetLocomotionState(FighterState.Idle);
            Assert.Equal(FighterState.Idle, fighter.State);
        }

        [Fact]
        public void SetLocomotion_ShouldBeIgnored_WhileTheFighterIsBusy()
        {
            var fighter = NewFighter(maxHealth: 1000);
            fighter.TakeDamage(1, Knockback);

            fighter.SetLocomotionState(FighterState.Walk);

            Assert.Equal(FighterState.Hit, fighter.State);
        }

        [Fact]
        public void Velocity_ShouldCarryTheFighterAndThenBleedOff()
        {
            var fighter = NewFighter();
            fighter.Position = Vector2.Zero;
            fighter.SetLocomotionState(FighterState.Walk);

            fighter.BeginDash(new Vector2(1f, 0f));
            Frames.AdvanceSeconds(fighter, 0.5f);
            float travelled = fighter.Position.X;
            Frames.AdvanceSeconds(fighter, 0.5f);

            Assert.True(travelled > 0f, "the dash should move the fighter");
            Assert.Equal(travelled, fighter.Position.X, 0);
        }

        // ---------------------------------------------------------------- hurtbox

        [Fact]
        public void HurtBox_ShouldBeAnchoredAtTheFeet()
        {
            var fighter = NewFighter();
            fighter.Position = new Vector2(200f, 400f);

            Rectangle box = fighter.HurtBox;

            Assert.Equal(200 - fighter.BodyWidth / 2, box.X);
            Assert.Equal(400 - fighter.BodyHeight, box.Y);
            Assert.Equal(fighter.BodyWidth, box.Width);
            Assert.Equal(fighter.BodyHeight, box.Height);
            Assert.Equal(400, box.Bottom);
        }

        [Fact]
        public void HurtBox_ShouldFollowTheFighter()
        {
            var fighter = NewFighter();
            fighter.Position = new Vector2(200f, 400f);
            Rectangle before = fighter.HurtBox;

            fighter.Position = new Vector2(260f, 400f);

            Assert.Equal(before.X + 60, fighter.HurtBox.X);
        }
    }
}
