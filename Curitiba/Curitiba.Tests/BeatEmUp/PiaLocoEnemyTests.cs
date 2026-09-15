using System.Collections.Generic;
using Curitiba.Core;
using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// The enemy: its stats, and the AI that walks it into range, waits its turn and swings.
    /// </summary>
    /// <remarks>
    /// The AI's only randomness is the eagerness roll in <c>WantsToAttack</c>, against a static
    /// unseeded <c>Random</c>. Rather than abstract that away, these tests pin the profile's
    /// <c>AttackChance</c> to a value the roll can never fail or never pass, which makes the
    /// behaviour deterministic without changing the game.
    /// </remarks>
    public class PiaLocoEnemyTests
    {
        private static readonly Vector2 PlayerSpot = new Vector2(400f, 400f);

        private static SofiaPlayer NewSofia(ContentManager content, Vector2 position = default)
        {
            var sofia = new SofiaPlayer(content, null);
            sofia.Position = position == default ? PlayerSpot : position;
            return sofia;
        }

        // ---------------------------------------------------------------- stats

        [Fact]
        public void Enemy_ShouldStartWithItsAuthoredStats()
        {
            ContentManager content = HeadlessContent.Create();
            PiaLocoEnemy enemy = Enemies.Create(content, NewSofia(content));

            Assert.Equal(30, enemy.MaxHealth);
            Assert.Equal(30, enemy.Health);
            Assert.Equal(FighterState.Idle, enemy.State);
            Assert.Equal("Piá Loco", enemy.Name);
        }

        [Fact]
        public void Enemy_ShouldDieRatherThanStayDown()
        {
            ContentManager content = HeadlessContent.Create();
            PiaLocoEnemy enemy = Enemies.Create(content, NewSofia(content));

            enemy.TakeDamage(enemy.Health, Vector2.Zero);

            Assert.Equal(FighterState.Dead, enemy.State);
        }

        [Fact]
        public void Enemy_ShouldUseTheSuppliedTuning()
        {
            ContentManager content = HeadlessContent.Create();
            var tuning = FighterTuning.PiaLocoDefaults();
            tuning.MaxHealth = 80;
            tuning.MoveSpeed = 500f;

            PiaLocoEnemy enemy = Enemies.Create(content, NewSofia(content), tuning: tuning);

            Assert.Equal(80, enemy.MaxHealth);
        }

        [Fact]
        public void HitsToKnockdown_ShouldComeFromTheWave_NotTheTuning()
        {
            // ApplyTuning deliberately leaves hitsToKnockdown alone: difficulty is per wave.
            ContentManager content = HeadlessContent.Create();
            PiaLocoEnemy enemy = Enemies.Create(content, NewSofia(content), hitsToKnockdown: 2);

            enemy.TakeDamage(1, Vector2.Zero);
            Assert.Equal(FighterState.Hit, enemy.State);
            Frames.AdvanceSeconds(enemy, 0.4f);

            enemy.TakeDamage(1, Vector2.Zero);
            Assert.Equal(FighterState.KnockedDown, enemy.State);
        }

        // ---------------------------------------------------------------- chasing

        [Fact]
        public void Enemy_ShouldFaceThePlayer()
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            PiaLocoEnemy enemy = Enemies.Create(content, sofia, new Vector2(900f, 400f));

            enemy.Update(Frames.Step());

            Assert.Equal(FaceDirection.Left, enemy.Facing);
        }

        [Fact]
        public void Enemy_ShouldTurnAround_WhenThePlayerCrossesIt()
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            PiaLocoEnemy enemy = Enemies.Create(content, sofia, new Vector2(900f, 400f));
            enemy.Update(Frames.Step());
            Assert.Equal(FaceDirection.Left, enemy.Facing);

            sofia.Position = new Vector2(1500f, 400f);
            enemy.Update(Frames.Step());

            Assert.Equal(FaceDirection.Right, enemy.Facing);
        }

        [Fact]
        public void Enemy_ShouldCloseInOnThePlayer()
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            PiaLocoEnemy enemy = Enemies.Create(content, sofia, new Vector2(900f, 400f),
                                                profile: Enemies.ProfileWithChance(Enemies.NeverAttacks));
            float before = Vector2.Distance(enemy.Position, sofia.Position);

            Frames.AdvanceSeconds(enemy, 1f);

            Assert.True(Vector2.Distance(enemy.Position, sofia.Position) < before,
                "the enemy should have walked toward the player");
        }

        [Fact]
        public void Enemy_ShouldWalkWhileClosingIn()
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            PiaLocoEnemy enemy = Enemies.Create(content, sofia, new Vector2(900f, 400f),
                                                profile: Enemies.ProfileWithChance(Enemies.NeverAttacks));

            Frames.AdvanceSeconds(enemy, 0.2f);

            Assert.Equal(FighterState.Walk, enemy.State);
        }

        [Fact]
        public void Enemy_ShouldStopChasingADefeatedPlayer()
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            sofia.TakeDamage(sofia.Health, Vector2.Zero);
            PiaLocoEnemy enemy = Enemies.Create(content, sofia, new Vector2(900f, 400f),
                                                profile: Enemies.ProfileWithChance(Enemies.AlwaysAttacks));

            Frames.AdvanceSeconds(enemy, 1f);

            Assert.Equal(FighterState.Idle, enemy.State);
            Assert.Equal(900f, enemy.Position.X, 0);
        }

        // ---------------------------------------------------------------- entry walk

        [Fact]
        public void NewlySpawnedEnemy_ShouldBeEntering()
        {
            ContentManager content = HeadlessContent.Create();
            PiaLocoEnemy enemy = Enemies.Create(content, NewSofia(content), new Vector2(900f, 400f));

            enemy.BeginEntry(new Vector2(700f, 400f));

            Assert.True(enemy.IsEntering);
        }

        [Fact]
        public void EnteringEnemy_ShouldWalkToItsEntryTarget_ThenJoinTheFight()
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            PiaLocoEnemy enemy = Enemies.Create(content, sofia, new Vector2(900f, 400f),
                                                profile: Enemies.ProfileWithChance(Enemies.NeverAttacks));
            enemy.BeginEntry(new Vector2(700f, 400f));

            Frames.AdvanceSeconds(enemy, 4f);

            Assert.False(enemy.IsEntering);
        }

        // ---------------------------------------------------------------- attacking

        [Fact]
        public void Enemy_ShouldAttack_WhenItIsInRangeAndAligned()
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            PiaLocoEnemy enemy = Enemies.Create(content, sofia, PlayerSpot + new Vector2(40f, 0f),
                                                profile: Enemies.ProfileWithChance(Enemies.AlwaysAttacks));

            int frames = Frames.AdvanceUntil(enemy, () => enemy.State == FighterState.Attack, maxFrames: 300);

            Assert.True(frames < 300, "an eager enemy standing next to the player should swing");
        }

        [Fact]
        public void Enemy_ShouldNotAttack_WhenTheRollNeverPasses()
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            PiaLocoEnemy enemy = Enemies.Create(content, sofia, PlayerSpot + new Vector2(40f, 0f),
                                                profile: Enemies.ProfileWithChance(Enemies.NeverAttacks));

            Frames.AdvanceSeconds(enemy, 3f);

            Assert.NotEqual(FighterState.Attack, enemy.State);
        }

        [Fact]
        public void Enemy_ShouldNotAttack_WhileOnAnotherLane()
        {
            // Vertical tolerance is 16px of corridor depth; 120px away is a different lane entirely.
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            var slots = new AttackSlotManager();
            PiaLocoEnemy enemy = Enemies.Create(content, sofia, PlayerSpot + new Vector2(0f, -120f),
                                                profile: Enemies.ProfileWithChance(Enemies.AlwaysAttacks),
                                                slots: slots);

            // 120px of depth at 72px/s takes well over a second to close, so throughout this
            // window the enemy is eager, in horizontal range, and still refuses to swing.
            Frames.AdvanceSeconds(enemy, 0.5f);

            Assert.True(System.Math.Abs(enemy.Position.Y - sofia.Position.Y) > 16f, "still off-lane");
            Assert.NotEqual(FighterState.Attack, enemy.State);
        }

        [Fact]
        public void Enemy_ShouldNotAttack_WithoutAnAttackToken()
        {
            // Every token is already held, so this enemy must keep circling and wait its turn.
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            var slots = new AttackSlotManager(maxAttackers: 1);
            slots.TryAcquireAttackToken(new TestFighter());

            PiaLocoEnemy enemy = Enemies.Create(content, sofia, PlayerSpot + new Vector2(40f, 0f),
                                                profile: Enemies.ProfileWithChance(Enemies.AlwaysAttacks),
                                                slots: slots);

            Frames.AdvanceSeconds(enemy, 3f);

            Assert.NotEqual(FighterState.Attack, enemy.State);
        }

        [Fact]
        public void Enemy_ShouldAttack_OnceATokenFreesUp()
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            var slots = new AttackSlotManager(maxAttackers: 1);
            var hog = new TestFighter();
            slots.TryAcquireAttackToken(hog);
            PiaLocoEnemy enemy = Enemies.Create(content, sofia, PlayerSpot + new Vector2(40f, 0f),
                                                profile: Enemies.ProfileWithChance(Enemies.AlwaysAttacks),
                                                slots: slots);
            Frames.AdvanceSeconds(enemy, 1f);
            Assert.NotEqual(FighterState.Attack, enemy.State);

            slots.ReleaseAttackToken(hog);
            int frames = Frames.AdvanceUntil(enemy, () => enemy.State == FighterState.Attack, maxFrames: 300);

            Assert.True(frames < 300, "the waiting enemy should take its turn once a token frees up");
        }

        [Fact]
        public void EnemySwing_ShouldProduceAHitbox()
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            PiaLocoEnemy enemy = Enemies.Create(content, sofia, PlayerSpot + new Vector2(40f, 0f),
                                                profile: Enemies.ProfileWithChance(Enemies.AlwaysAttacks));

            Frames.AdvanceUntil(enemy, () => enemy.CurrentAttack.HasValue, maxFrames: 300);

            Assert.True(enemy.CurrentAttack.HasValue);
            Assert.Equal(5, enemy.CurrentAttack.Value.Damage);
        }

        [Fact]
        public void StruckEnemy_ShouldGiveUpItsAttackToken()
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            var slots = new AttackSlotManager(maxAttackers: 1);
            PiaLocoEnemy enemy = Enemies.Create(content, sofia, PlayerSpot + new Vector2(40f, 0f),
                                                profile: Enemies.ProfileWithChance(Enemies.AlwaysAttacks),
                                                slots: slots);
            Frames.AdvanceUntil(enemy, () => enemy.State == FighterState.Attack, maxFrames: 300);

            enemy.TakeDamage(1, Vector2.Zero);
            enemy.Update(Frames.Step());

            Assert.True(slots.TryAcquireAttackToken(new TestFighter()),
                "being hit should release the token so another enemy can step in");
        }

        [Fact]
        public void DefeatedEnemy_ShouldReleaseItsSlotAndToken()
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            var slots = new AttackSlotManager(maxAttackers: 1);
            PiaLocoEnemy enemy = Enemies.Create(content, sofia, PlayerSpot + new Vector2(40f, 0f),
                                                profile: Enemies.ProfileWithChance(Enemies.AlwaysAttacks),
                                                slots: slots);
            Frames.AdvanceSeconds(enemy, 0.5f);

            enemy.TakeDamage(enemy.Health, Vector2.Zero);
            enemy.Update(Frames.Step());

            Assert.True(slots.TryAcquireAttackToken(new TestFighter()));
        }

        // ---------------------------------------------------------------- personalities

        [Theory]
        [InlineData("Aggressive")]
        [InlineData("Defensive")]
        [InlineData("Balanced")]
        [InlineData("Runner")]
        public void EveryPersonality_ShouldProduceAWorkingEnemy(string personality)
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            EnemyProfile profile = Enemies.ProfileWithChance(
                Enemies.AlwaysAttacks, (EnemyPersonality)System.Enum.Parse(typeof(EnemyPersonality), personality));

            PiaLocoEnemy enemy = Enemies.Create(content, sofia, PlayerSpot + new Vector2(40f, 0f), profile: profile);
            int frames = Frames.AdvanceUntil(enemy, () => enemy.State == FighterState.Attack, maxFrames: 400);

            Assert.True(frames < 400, $"a {personality} enemy in range should eventually swing");
        }

        [Fact]
        public void Runner_ShouldCloseAGapFasterThanBalanced()
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            // Far enough away to be beyond both run distances, so only the runner sprints.
            var start = new Vector2(PlayerSpot.X + 400f, PlayerSpot.Y);

            PiaLocoEnemy runner = Enemies.Create(content, sofia, start,
                profile: Enemies.ProfileWithChance(Enemies.NeverAttacks, EnemyPersonality.Runner));
            PiaLocoEnemy balanced = Enemies.Create(content, sofia, start,
                profile: Enemies.ProfileWithChance(Enemies.NeverAttacks, EnemyPersonality.Balanced));

            Frames.AdvanceSeconds(runner, 0.5f);
            Frames.AdvanceSeconds(balanced, 0.5f);

            Assert.True(runner.Position.X < balanced.Position.X,
                "the runner's sprint multiplier should have closed more ground");
        }

        // ---------------------------------------------------------------- crowding

        [Fact]
        public void Enemies_ShouldPushApart_WhenTheyOverlap()
        {
            ContentManager content = HeadlessContent.Create();
            SofiaPlayer sofia = NewSofia(content);
            var crowd = new List<PiaLocoEnemy>();
            var slots = new AttackSlotManager();

            PiaLocoEnemy first = Enemies.Create(content, sofia, new Vector2(600f, 400f),
                profile: Enemies.ProfileWithChance(Enemies.NeverAttacks), slots: slots, neighbors: crowd);
            PiaLocoEnemy second = Enemies.Create(content, sofia, new Vector2(605f, 400f),
                profile: Enemies.ProfileWithChance(Enemies.NeverAttacks), slots: slots, neighbors: crowd);
            crowd.Add(first);
            crowd.Add(second);

            float before = Vector2.Distance(first.Position, second.Position);
            for (int i = 0; i < 30; i++)
            {
                first.Update(Frames.Step());
                second.Update(Frames.Step());
            }

            Assert.True(Vector2.Distance(first.Position, second.Position) > before,
                "separation should stop enemies stacking on one point");
        }
    }
}
