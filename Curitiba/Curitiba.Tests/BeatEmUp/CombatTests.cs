using System.Collections.Generic;
using Curitiba.Core;
using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Microsoft.Xna.Framework;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// What a swing actually does: when the hitbox exists, where it sits, who it can hit and how often.
    /// </summary>
    /// <remarks>
    /// The arena owns the intersection loop, but it is a thin one — it asks the attacker for
    /// <c>CurrentAttack</c>, tests it against each <c>HurtBox</c> and dedupes through
    /// <c>AttackHitTargets</c>. These tests drive that same contract directly against two fighters,
    /// which keeps them on observable behaviour (health, state) rather than on private internals.
    /// </remarks>
    public class CombatTests
    {
        /// <summary>A tuning with one swing whose phases are easy to land exactly on a frame.</summary>
        private static FighterTuning SingleSwing(int damage = 10, int reach = 46, bool launches = false,
                                                 float startup = 0.10f, float active = 0.10f, float recovery = 0.20f)
        {
            var tuning = FighterTuning.PiaLocoDefaults();
            tuning.MaxHealth = 1000;
            tuning.ComboChain = new List<ComboMoveDef>
            {
                new ComboMoveDef
                {
                    Id = "swing", State = "Attack",
                    Startup = startup, Active = active, Recovery = recovery,
                    Damage = damage, Reach = reach,
                    KnockbackX = 220f, KnockbackY = -40f,
                    Launches = launches,
                },
            };
            return tuning;
        }

        /// <summary>Runs the arena's hit resolution for one attacker against a set of targets.</summary>
        private static int ResolveHits(Fighter attacker, IEnumerable<Fighter> targets)
        {
            if (!attacker.CurrentAttack.HasValue)
                return 0;

            AttackData attack = attacker.CurrentAttack.Value;
            int landed = 0;
            foreach (Fighter target in targets)
            {
                if (!target.IsAlive || attacker.AttackHitTargets.Contains(target))
                    continue;

                if (!attack.Hitbox.Intersects(target.HurtBox))
                    continue;

                target.TakeDamage(attack.Damage, attack.Knockback,
                    attack.Launches ? HitReaction.Launch : HitReaction.Normal);
                attacker.AttackHitTargets.Add(target);
                landed++;
            }
            return landed;
        }

        // ---------------------------------------------------------------- the active window

        [Fact]
        public void Fighter_ShouldHaveNoHitbox_WhileIdle()
        {
            var fighter = new TestFighter(SingleSwing());

            Assert.False(fighter.CurrentAttack.HasValue);
        }

        [Fact]
        public void Hitbox_ShouldNotExist_DuringStartup()
        {
            var fighter = new TestFighter(SingleSwing(startup: 0.10f));
            fighter.BeginAttack();

            // 0.05s in: the swing has begun but the blow has not come out yet.
            Frames.AdvanceSeconds(fighter, 0.05f);

            Assert.Equal(FighterState.Attack, fighter.State);
            Assert.False(fighter.CurrentAttack.HasValue);
        }

        [Fact]
        public void Hitbox_ShouldExist_DuringTheActiveFrames()
        {
            var fighter = new TestFighter(SingleSwing(startup: 0.10f, active: 0.10f));
            fighter.BeginAttack();

            Frames.AdvanceSeconds(fighter, 0.15f);

            Assert.True(fighter.CurrentAttack.HasValue);
        }

        [Fact]
        public void Hitbox_ShouldBeGone_DuringRecovery()
        {
            var fighter = new TestFighter(SingleSwing(startup: 0.10f, active: 0.10f, recovery: 0.20f));
            fighter.BeginAttack();

            Frames.AdvanceSeconds(fighter, 0.25f);

            Assert.Equal(FighterState.Attack, fighter.State);
            Assert.False(fighter.CurrentAttack.HasValue);
        }

        [Fact]
        public void Swing_ShouldReturnToIdle_WhenItIsOver()
        {
            var fighter = new TestFighter(SingleSwing());
            fighter.BeginAttack();

            Frames.AdvanceSeconds(fighter, 0.45f);

            Assert.Equal(FighterState.Idle, fighter.State);
            Assert.False(fighter.CurrentAttack.HasValue);
        }

        [Fact]
        public void Hitbox_ShouldOnlyDealDamage_DuringItsWindow()
        {
            // The whole point of the active window, expressed as a hit count over a whole swing.
            var attacker = new TestFighter(SingleSwing(damage: 1, reach: 200));
            var target = new TestFighter(SingleSwing());
            attacker.Position = new Vector2(100f, 400f);
            target.Position = new Vector2(160f, 400f);

            attacker.BeginAttack();
            int framesWithHitbox = 0;
            for (int i = 0; i < Frames.FramesFor(0.4f); i++)
            {
                attacker.Update(Frames.Step());
                if (attacker.CurrentAttack.HasValue)
                    framesWithHitbox++;
            }

            // 0.10s of active frames at 1/60s: a handful, not the whole 0.40s swing.
            Assert.InRange(framesWithHitbox, 5, 8);
        }

        // ---------------------------------------------------------------- hitbox geometry

        [Fact]
        public void Hitbox_ShouldExtendInFrontOfTheFighter_WhenFacingRight()
        {
            var fighter = FacingFighter(FaceDirection.Right, new Vector2(100f, 400f));

            Rectangle hitbox = ActiveHitbox(fighter).Hitbox;

            Assert.True(hitbox.X >= fighter.Position.X - fighter.BodyWidth / 2f,
                "a right-facing blow must not reach out behind the fighter");
            Assert.True(hitbox.Right > fighter.Position.X);
        }

        [Fact]
        public void Hitbox_ShouldExtendInFrontOfTheFighter_WhenFacingLeft()
        {
            var fighter = FacingFighter(FaceDirection.Left, new Vector2(100f, 400f));

            Rectangle hitbox = ActiveHitbox(fighter).Hitbox;

            Assert.True(hitbox.X < fighter.Position.X);
            Assert.True(hitbox.Right <= fighter.Position.X + fighter.BodyWidth / 2f);
        }

        [Fact]
        public void Hitbox_ShouldBeAsWideAsTheMoveReach()
        {
            var fighter = new TestFighter(SingleSwing(reach: 62));
            fighter.Position = new Vector2(100f, 400f);
            fighter.BeginAttack();
            Frames.AdvanceSeconds(fighter, 0.15f);

            Assert.Equal(62, fighter.CurrentAttack.Value.Hitbox.Width);
        }

        [Fact]
        public void Knockback_ShouldPointAwayFromTheAttacker()
        {
            var right = FacingFighter(FaceDirection.Right, new Vector2(100f, 400f));
            var left = FacingFighter(FaceDirection.Left, new Vector2(100f, 400f));

            Assert.True(ActiveHitbox(right).Knockback.X > 0f);
            Assert.True(ActiveHitbox(left).Knockback.X < 0f);
        }

        [Fact]
        public void Hitbox_ShouldCarryTheMoveDamage()
        {
            var fighter = new TestFighter(SingleSwing(damage: 22));
            fighter.BeginAttack();
            Frames.AdvanceSeconds(fighter, 0.15f);

            Assert.Equal(22, fighter.CurrentAttack.Value.Damage);
        }

        [Fact]
        public void Hitbox_ShouldTrackTheFighterWhileActive()
        {
            var fighter = new TestFighter(SingleSwing());
            fighter.Position = new Vector2(100f, 400f);
            fighter.BeginAttack();
            Frames.AdvanceSeconds(fighter, 0.12f);
            int firstX = fighter.CurrentAttack.Value.Hitbox.X;

            fighter.Position = new Vector2(300f, 400f);
            fighter.Update(Frames.Step());

            Assert.Equal(firstX + 200, fighter.CurrentAttack.Value.Hitbox.X);
        }

        // ---------------------------------------------------------------- landing blows

        [Fact]
        public void Attack_ShouldHitATargetInsideTheHitbox()
        {
            var attacker = Attacker(new Vector2(100f, 400f));
            var target = Target(new Vector2(140f, 400f));

            int landed = ResolveHits(attacker, new[] { target });

            Assert.Equal(1, landed);
            Assert.Equal(990, target.Health);
        }

        [Fact]
        public void Attack_ShouldMissATargetOutOfReach()
        {
            var attacker = Attacker(new Vector2(100f, 400f));
            var target = Target(new Vector2(600f, 400f));

            int landed = ResolveHits(attacker, new[] { target });

            Assert.Equal(0, landed);
            Assert.Equal(1000, target.Health);
        }

        [Fact]
        public void Attack_ShouldMissATargetBehindTheAttacker()
        {
            var attacker = Attacker(new Vector2(300f, 400f));
            var target = Target(new Vector2(240f, 400f));

            Assert.Equal(0, ResolveHits(attacker, new[] { target }));
        }

        [Fact]
        public void Attack_ShouldMissATargetOnAnotherLane()
        {
            // The corridor is depth: a target far enough up the screen is out of the blow's band.
            var attacker = Attacker(new Vector2(100f, 400f));
            var target = Target(new Vector2(140f, 200f));

            Assert.Equal(0, ResolveHits(attacker, new[] { target }));
        }

        [Fact]
        public void Attack_ShouldHitEveryTargetItOverlaps()
        {
            var attacker = Attacker(new Vector2(100f, 400f));
            var first = Target(new Vector2(130f, 400f));
            var second = Target(new Vector2(150f, 400f));

            int landed = ResolveHits(attacker, new[] { first, second });

            Assert.Equal(2, landed);
            Assert.Equal(990, first.Health);
            Assert.Equal(990, second.Health);
        }

        [Fact]
        public void Attack_ShouldHitEachTargetOnlyOnce_AcrossItsWholeActiveWindow()
        {
            var attacker = Attacker(new Vector2(100f, 400f));
            var target = Target(new Vector2(140f, 400f));

            // Resolve on every frame of the swing, the way the arena does.
            int landed = 0;
            for (int i = 0; i < Frames.FramesFor(0.4f); i++)
            {
                landed += ResolveHits(attacker, new[] { target });
                attacker.Update(Frames.Step());
            }

            Assert.Equal(1, landed);
            Assert.Equal(990, target.Health);
        }

        [Fact]
        public void NextSwing_ShouldBeAbleToHitTheSameTargetAgain()
        {
            var attacker = Attacker(new Vector2(100f, 400f));
            var target = Target(new Vector2(140f, 400f));
            ResolveHits(attacker, new[] { target });

            // A fresh swing clears the per-swing hit list.
            Frames.AdvanceSeconds(attacker, 0.5f);
            attacker.BeginAttack();
            Frames.AdvanceSeconds(attacker, 0.15f);
            int landed = ResolveHits(attacker, new[] { target });

            Assert.Equal(1, landed);
            Assert.Equal(980, target.Health);
        }

        [Fact]
        public void Attack_ShouldNotHitADeadTarget()
        {
            var attacker = Attacker(new Vector2(100f, 400f));
            var target = Target(new Vector2(140f, 400f));
            target.TakeDamage(target.Health, Vector2.Zero);
            Assert.Equal(FighterState.Dead, target.State);

            Assert.Equal(0, ResolveHits(attacker, new[] { target }));
        }

        [Fact]
        public void Attack_ShouldNotHitAKnockedDownTarget()
        {
            var attacker = Attacker(new Vector2(100f, 400f));
            var target = Target(new Vector2(140f, 400f));
            target.TakeDamage(1, Vector2.Zero, HitReaction.Knockdown);

            Assert.Equal(0, ResolveHits(attacker, new[] { target }));
        }

        [Fact]
        public void LaunchingAttack_ShouldThrowItsTarget()
        {
            var attacker = new TestFighter(SingleSwing(launches: true));
            attacker.Position = new Vector2(100f, 400f);
            attacker.BeginAttack();
            Frames.AdvanceSeconds(attacker, 0.15f);
            var target = Target(new Vector2(140f, 400f));

            ResolveHits(attacker, new[] { target });

            Assert.Equal(FighterState.Thrown, target.State);
        }

        [Fact]
        public void NonLaunchingAttack_ShouldOnlyStagger()
        {
            var attacker = Attacker(new Vector2(100f, 400f));
            var target = Target(new Vector2(140f, 400f));

            ResolveHits(attacker, new[] { target });

            Assert.Equal(FighterState.Hit, target.State);
        }

        [Fact]
        public void Attack_ShouldDefeatATargetOnTheLastPointOfHealth()
        {
            var attacker = Attacker(new Vector2(100f, 400f));
            var target = Target(new Vector2(140f, 400f));
            target.TakeDamage(target.Health - 10, Vector2.Zero);
            Frames.AdvanceSeconds(target, 0.5f);

            ResolveHits(attacker, new[] { target });

            Assert.Equal(0, target.Health);
            Assert.True(target.IsDefeated);
        }

        // ---------------------------------------------------------------- attack request buffering

        [Fact]
        public void RequestAttack_ShouldStartASwingOnTheNextUpdate()
        {
            var fighter = new TestFighter(SingleSwing());

            fighter.RequestAttack();
            fighter.Update(Frames.Step());

            Assert.Equal(FighterState.Attack, fighter.State);
        }

        [Fact]
        public void OnePress_ShouldStartExactlyOneSwing()
        {
            var fighter = new TestFighter(SingleSwing());

            fighter.RequestAttack();
            Frames.AdvanceSeconds(fighter, 0.5f);

            // The buffered press was consumed by the first swing, so the fighter is idle again.
            Assert.Equal(FighterState.Idle, fighter.State);
        }

        [Fact]
        public void BufferedPress_ShouldLapse_IfTheFighterCannotActInTime()
        {
            var tuning = SingleSwing();
            tuning.AttackBufferDuration = 0.05f;
            var fighter = new TestFighter(tuning);
            fighter.TakeDamage(1, Vector2.Zero);

            fighter.RequestAttack();
            Frames.AdvanceSeconds(fighter, 0.5f);

            Assert.Equal(FighterState.Idle, fighter.State);
        }

        [Fact]
        public void BufferedPress_ShouldSurviveUntilTheFighterCanAct()
        {
            var tuning = SingleSwing();
            tuning.AttackBufferDuration = 1.0f;
            tuning.HitDuration = 0.10f;
            var fighter = new TestFighter(tuning);
            fighter.TakeDamage(1, Vector2.Zero);

            // Pressed mid-stagger: the swing must still come out once the stagger ends.
            fighter.RequestAttack();
            Frames.AdvanceSeconds(fighter, 0.15f);

            Assert.Equal(FighterState.Attack, fighter.State);
        }

        [Fact]
        public void TakingDamage_ShouldCancelALiveHitbox()
        {
            var fighter = new TestFighter(SingleSwing());
            fighter.BeginAttack();
            Frames.AdvanceSeconds(fighter, 0.15f);
            Assert.True(fighter.CurrentAttack.HasValue);

            fighter.TakeDamage(1, Vector2.Zero);

            Assert.False(fighter.CurrentAttack.HasValue);
        }

        // ---------------------------------------------------------------- helpers

        private static TestFighter Attacker(Vector2 position)
        {
            var attacker = new TestFighter(SingleSwing());
            attacker.Position = position;
            attacker.BeginAttack();
            Frames.AdvanceSeconds(attacker, 0.15f);
            return attacker;
        }

        private static TestFighter Target(Vector2 position)
        {
            var tuning = FighterTuning.PiaLocoDefaults();
            tuning.MaxHealth = 1000;
            tuning.InvulnerabilityOnHit = 0f;
            return new TestFighter(tuning) { Position = position };
        }

        private static TestFighter FacingFighter(FaceDirection facing, Vector2 position)
        {
            var fighter = new TestFighter(SingleSwing());
            fighter.Position = position;
            fighter.Facing = facing;
            fighter.BeginAttack();
            Frames.AdvanceSeconds(fighter, 0.15f);
            return fighter;
        }

        private static AttackData ActiveHitbox(Fighter fighter)
        {
            Assert.True(fighter.CurrentAttack.HasValue, "expected the fighter to be in its active frames");
            return fighter.CurrentAttack.Value;
        }

        // ---------------------------------------------------------------- scoring weight

        [Fact]
        public void TheHitbox_ShouldCarryTheScoreTypeOfTheMoveThatMadeIt()
        {
            // Arrange — the arena reads the weight off the hitbox, because the move that produced
            // it is private to the fighter.
            FighterTuning tuning = SingleSwing();
            tuning.ComboChain[0].ScoreType = "heavy";
            var fighter = new TestFighter(tuning);

            // Act
            fighter.BeginAttack();
            Frames.AdvanceSeconds(fighter, 0.12f);

            // Assert
            Assert.True(fighter.CurrentAttack.HasValue);
            Assert.Equal("Heavy", fighter.CurrentAttack.Value.Type.ToString());
        }

        [Fact]
        public void AJumpAttackHitbox_ShouldCarryTheAirScoreType()
        {
            // The air kick has no ComboMove at all — it is built from the scalar stats — so its
            // weight cannot come from data.
            var fighter = new TestFighter(SingleSwing());
            fighter.BeginJump();
            Frames.AdvanceSeconds(fighter, 0.15f);
            fighter.BeginJumpAttack();

            Frames.AdvanceSeconds(fighter, 0.15f);

            Assert.Equal(FighterState.JumpAttack, fighter.State);
            Assert.True(fighter.CurrentAttack.HasValue);
            Assert.Equal("Air", fighter.CurrentAttack.Value.Type.ToString());
        }

    }
}
