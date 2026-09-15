using Curitiba.Core;
using Curitiba.Core.BeatEmUp;
using Curitiba.Core.Inputs;
using Curitiba.Tests.TestSupport;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// Sofia: her stats, her key bindings, and how a frame of input turns into movement or a move.
    /// </summary>
    /// <remarks>
    /// <c>HandleInput</c> is separate from <c>Update</c> in the real loop (the arena calls them on
    /// consecutive lines), so input can be driven here from a synthetic keyboard with no window.
    /// </remarks>
    public class SofiaPlayerTests
    {
        private static SofiaPlayer NewSofia(FighterTuning tuning = null) =>
            new SofiaPlayer(HeadlessContent.Create(), null, tuning);

        private static void Handle(SofiaPlayer sofia, InputState input) => sofia.HandleInput(input, null);

        // ---------------------------------------------------------------- defaults

        [Fact]
        public void Sofia_ShouldStartWithHerAuthoredStats()
        {
            var sofia = NewSofia();

            Assert.Equal(100, sofia.MaxHealth);
            Assert.Equal(100, sofia.Health);
            Assert.Equal(FighterState.Idle, sofia.State);
            Assert.Equal("Sofia", sofia.Name);
        }

        [Fact]
        public void Sofia_ShouldBeKnockedDownRatherThanKilled()
        {
            var sofia = NewSofia();

            sofia.TakeDamage(sofia.Health, Vector2.Zero);

            Assert.Equal(FighterState.KnockedDown, sofia.State);
            Assert.True(sofia.IsDefeated);
        }

        [Fact]
        public void Sofia_ShouldHaveToJumpTheCurb()
        {
            Assert.True(NewSofia().MustJumpCurb);
        }

        [Fact]
        public void Sofia_ShouldUseTheSuppliedTuning()
        {
            var tuning = FighterTuning.SofiaDefaults();
            tuning.MaxHealth = 250;

            var sofia = NewSofia(tuning);

            Assert.Equal(250, sofia.MaxHealth);
        }

        // ---------------------------------------------------------------- movement

        [Theory]
        [InlineData("Left", -1, 0)]
        [InlineData("A", -1, 0)]
        [InlineData("Right", 1, 0)]
        [InlineData("D", 1, 0)]
        [InlineData("Up", 0, -1)]
        [InlineData("W", 0, -1)]
        [InlineData("Down", 0, 1)]
        [InlineData("S", 0, 1)]
        public void HoldingADirection_ShouldWalkThatWay(string key, int dx, int dy)
        {
            var sofia = NewSofia();
            sofia.Position = Vector2.Zero;

            Handle(sofia, SyntheticInput.Held(Key(key)));
            sofia.Update(Frames.Step());

            Assert.Equal(FighterState.Walk, sofia.State);
            Assert.Equal(dx, System.Math.Sign(sofia.Position.X));
            Assert.Equal(dy, System.Math.Sign(sofia.Position.Y));
        }

        [Fact]
        public void NoInput_ShouldLeaveSofiaIdle()
        {
            var sofia = NewSofia();
            sofia.Position = Vector2.Zero;

            Handle(sofia, SyntheticInput.None());
            sofia.Update(Frames.Step());

            Assert.Equal(FighterState.Idle, sofia.State);
            Assert.Equal(Vector2.Zero, sofia.Position);
        }

        [Fact]
        public void DiagonalMovement_ShouldNotBeFasterThanStraight()
        {
            var straight = NewSofia();
            var diagonal = NewSofia();
            straight.Position = diagonal.Position = Vector2.Zero;

            Handle(straight, SyntheticInput.Held(Keys.Right));
            Handle(diagonal, SyntheticInput.Held(Keys.Right, Keys.Down));
            straight.Update(Frames.Step());
            diagonal.Update(Frames.Step());

            // The direction is normalised, so the speed is the same in all eight directions.
            Assert.Equal(straight.Position.Length(), diagonal.Position.Length(), 3);
        }

        [Fact]
        public void WalkingLeft_ShouldTurnSofiaLeft()
        {
            var sofia = NewSofia();

            Handle(sofia, SyntheticInput.Held(Keys.Left));

            Assert.Equal(FaceDirection.Left, sofia.Facing);
        }

        [Fact]
        public void WalkingRight_ShouldTurnSofiaRight()
        {
            var sofia = NewSofia();
            sofia.Facing = FaceDirection.Left;

            Handle(sofia, SyntheticInput.Held(Keys.Right));

            Assert.Equal(FaceDirection.Right, sofia.Facing);
        }

        [Fact]
        public void WalkingStraightUp_ShouldNotChangeFacing()
        {
            var sofia = NewSofia();
            sofia.Facing = FaceDirection.Left;

            Handle(sofia, SyntheticInput.Held(Keys.Up));

            Assert.Equal(FaceDirection.Left, sofia.Facing);
        }

        // ---------------------------------------------------------------- bindings

        [Fact]
        public void AttackShouldBeBoundToJ_NotSpace()
        {
            // Recorded deliberately: the XML doc on SofiaPlayer and CLAUDE.md both still say
            // "Space / A / X" for the attack, but the code binds J. Space is the jump.
            var sofia = NewSofia();

            Handle(sofia, SyntheticInput.Pressed(Keys.J));
            sofia.Update(Frames.Step());

            Assert.Equal(FighterState.Attack, sofia.State);
        }

        [Fact]
        public void SpaceShouldJump()
        {
            var sofia = NewSofia();

            Handle(sofia, SyntheticInput.Pressed(Keys.Space));

            Assert.Equal(FighterState.Jump, sofia.State);
        }

        [Theory]
        [InlineData("LeftShift")]
        [InlineData("RightShift")]
        public void ShiftShouldDash(string key)
        {
            var sofia = NewSofia();

            Handle(sofia, SyntheticInput.Pressed(Key(key)));

            Assert.Equal(FighterState.Dash, sofia.State);
        }

        [Fact]
        public void HoldingAttack_ShouldNotSwingRepeatedly()
        {
            // Only a fresh press counts, so mashing is required rather than holding.
            var sofia = NewSofia();
            InputState held = SyntheticInput.Held(Keys.J);

            Handle(sofia, held);
            sofia.Update(Frames.Step());

            Assert.Equal(FighterState.Idle, sofia.State);
        }

        // ---------------------------------------------------------------- moves

        [Fact]
        public void Dash_ShouldCarrySofiaInTheHeldDirection()
        {
            var sofia = NewSofia();
            sofia.Position = Vector2.Zero;

            Handle(sofia, SyntheticInput.PressedWhileHolding(Keys.LeftShift, Keys.Right));
            Frames.AdvanceSeconds(sofia, 0.2f);

            Assert.True(sofia.Position.X > 0f);
        }

        [Fact]
        public void Dash_ShouldMakeSofiaBrieflyInvulnerable()
        {
            var sofia = NewSofia();

            Handle(sofia, SyntheticInput.Pressed(Keys.LeftShift));

            Assert.True(sofia.IsInvulnerable);
        }

        [Fact]
        public void Dash_ShouldEndInIdle()
        {
            var sofia = NewSofia();

            Handle(sofia, SyntheticInput.Pressed(Keys.LeftShift));
            Frames.AdvanceSeconds(sofia, 0.5f);

            Assert.Equal(FighterState.Idle, sofia.State);
        }

        [Fact]
        public void Jump_ShouldLeaveSofiaAirborne_ThenLandHer()
        {
            var sofia = NewSofia();

            Handle(sofia, SyntheticInput.Pressed(Keys.Space));
            Frames.AdvanceSeconds(sofia, 0.3f);
            Assert.True(sofia.IsAirborne);

            Frames.AdvanceSeconds(sofia, 1.5f);
            Assert.Equal(FighterState.Idle, sofia.State);
        }

        [Fact]
        public void Jump_ShouldPassThroughItsPhases()
        {
            var sofia = NewSofia();
            Handle(sofia, SyntheticInput.Pressed(Keys.Space));
            Assert.Equal(JumpPhase.Start, sofia.CurrentJumpPhase);

            Frames.AdvanceSeconds(sofia, 0.15f);
            Assert.Equal(JumpPhase.Rise, sofia.CurrentJumpPhase);

            Frames.AdvanceUntil(sofia, () => sofia.CurrentJumpPhase == JumpPhase.Fall, maxFrames: 120);
            Assert.Equal(JumpPhase.Fall, sofia.CurrentJumpPhase);
        }

        [Fact]
        public void AttackingMidJump_ShouldThrowTheAirKick()
        {
            var sofia = NewSofia();
            Handle(sofia, SyntheticInput.Pressed(Keys.Space));
            Frames.AdvanceSeconds(sofia, 0.2f);

            Handle(sofia, SyntheticInput.Pressed(Keys.J));

            Assert.Equal(FighterState.JumpAttack, sofia.State);
        }

        [Fact]
        public void AirKick_ShouldProduceAHitbox()
        {
            var sofia = NewSofia();
            Handle(sofia, SyntheticInput.Pressed(Keys.Space));
            Frames.AdvanceSeconds(sofia, 0.2f);
            Handle(sofia, SyntheticInput.Pressed(Keys.J));

            Frames.AdvanceUntil(sofia, () => sofia.CurrentAttack.HasValue, maxFrames: 30);

            Assert.True(sofia.CurrentAttack.HasValue);
        }

        [Fact]
        public void Dash_ShouldBeCancellableIntoAJump()
        {
            var sofia = NewSofia();
            Handle(sofia, SyntheticInput.Pressed(Keys.LeftShift));
            Assert.Equal(FighterState.Dash, sofia.State);

            Handle(sofia, SyntheticInput.Pressed(Keys.Space));

            Assert.Equal(FighterState.Jump, sofia.State);
        }

        // ---------------------------------------------------------------- when she cannot act

        [Fact]
        public void StaggeredSofia_ShouldNotMove()
        {
            var sofia = NewSofia();
            sofia.TakeDamage(5, Vector2.Zero);
            sofia.Position = Vector2.Zero;

            Handle(sofia, SyntheticInput.Held(Keys.Right));
            sofia.Update(Frames.Step());

            Assert.Equal(FighterState.Hit, sofia.State);
            Assert.Equal(0f, sofia.Position.X);
        }

        [Fact]
        public void DefeatedSofia_ShouldNotRespondToInput()
        {
            var sofia = NewSofia();
            sofia.TakeDamage(sofia.Health, Vector2.Zero);
            Frames.AdvanceSeconds(sofia, 2f);
            sofia.Position = Vector2.Zero;

            Handle(sofia, SyntheticInput.Held(Keys.Right));
            Handle(sofia, SyntheticInput.Pressed(Keys.J));
            Frames.AdvanceSeconds(sofia, 0.5f);

            Assert.Equal(FighterState.KnockedDown, sofia.State);
            Assert.Equal(0f, sofia.Position.X);
        }

        [Fact]
        public void MidSwingSofia_ShouldNotWalkAway()
        {
            var sofia = NewSofia();
            sofia.Position = Vector2.Zero;
            Handle(sofia, SyntheticInput.Pressed(Keys.J));
            sofia.Update(Frames.Step());

            Handle(sofia, SyntheticInput.Held(Keys.Right));
            sofia.Update(Frames.Step());

            Assert.Equal(0f, sofia.Position.X);
        }

        private static Keys Key(string name) => (Keys)System.Enum.Parse(typeof(Keys), name);
    }
}
