using System.Collections.Generic;
using System.Linq;
using Curitiba.Core.BeatEmUp;
using Curitiba.Core.Inputs;
using Curitiba.Tests.TestSupport;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// The arena, driven end to end: waves arm and spawn, the camera locks and releases, sections
    /// chain, and blows actually land between the player and the crowd.
    /// </summary>
    /// <remarks>
    /// These are integration tests — a real <c>CapaoRasoArena</c> built from a real
    /// <c>StageDefinition</c>, ticked with a real <c>GameTime</c>. Only drawing is left out, so the
    /// arena is constructed through the overload that takes the virtual screen size instead of a
    /// <c>ScreenManager</c>.
    /// </remarks>
    public class ArenaTests
    {
        private const float ViewWidth = 800f;
        private const float SceneHeight = 480f;

        private static CapaoRasoArena NewArena(StageDefinition def) =>
            new CapaoRasoArena(HeadlessContent.Create(), def, ViewWidth, SceneHeight);

        /// <summary>A stage with no art, so section widths come from the authored fallback widths.</summary>
        private static StageDefinition Stage(params SectionDef[] sections)
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            def.Sections = sections.ToList();
            return def;
        }

        private static SectionDef Section(float width, params WaveDef[] waves) => new SectionDef
        {
            BackgroundAsset = null,
            FallbackWidth = width,
            ParallaxBackdrop = false,
            CurbY = 0f,
            Waves = waves.ToList(),
        };

        private static WaveDef Wave(float lockX = 0f, int enemies = 2, float delay = 0f) => new WaveDef
        {
            LockCameraX = lockX,
            EnemyCount = enemies,
            Delay = delay,
            HitsToKnockdown = 3,
        };

        private static void Tick(CapaoRasoArena arena, InputState input, int frames = 1)
        {
            for (int i = 0; i < frames; i++)
                arena.Update(Frames.Step(), input, null);
        }

        private static void TickSeconds(CapaoRasoArena arena, InputState input, float seconds) =>
            Tick(arena, input, Frames.FramesFor(seconds));

        /// <summary>Ticks until <paramref name="predicate"/> holds, returning false if it never does.</summary>
        private static bool TickUntil(CapaoRasoArena arena, InputState input, System.Func<bool> predicate,
                                      int maxFrames = 3000)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (predicate())
                    return true;
                arena.Update(Frames.Step(), input, null);
            }
            return predicate();
        }

        /// <summary>Wipes out the current crowd, the way a very good player would.</summary>
        private static void DefeatEveryone(CapaoRasoArena arena)
        {
            foreach (PiaLocoEnemy enemy in arena.Enemies.ToList())
                enemy.TakeDamage(enemy.Health, Vector2.Zero);
        }

        private static InputState Idle => SyntheticInput.None();
        private static InputState WalkRight => SyntheticInput.Held(Keys.Right);

        // ---------------------------------------------------------------- construction

        [Fact]
        public void Arena_ShouldBuildFromAStageDefinition()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave())));

            Assert.Equal(1, arena.SectionCount);
            Assert.Equal(0, arena.CurrentSectionIndex);
            Assert.False(arena.Completed);
            Assert.False(arena.PlayerDefeated);
        }

        [Fact]
        public void Arena_ShouldFallBackToTheDefaultStage_WhenGivenNull()
        {
            CapaoRasoArena arena = NewArena(null);

            Assert.Equal(2, arena.SectionCount);
        }

        [Fact]
        public void Arena_ShouldPlaceSofiaAtTheSectionEntry()
        {
            StageDefinition def = Stage(Section(1600f, Wave()));
            def.Sections[0].Entry = new EntryDef { Mode = "Fixed", X = 120f, Y = 400f };

            CapaoRasoArena arena = NewArena(def);

            Assert.Equal(120f, arena.Player.Position.X, 0);
        }

        [Fact]
        public void Arena_ShouldStartWithNoEnemies()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(delay: 5f))));

            Assert.Empty(arena.Enemies);
        }

        // ---------------------------------------------------------------- waves

        [Fact]
        public void AWave_ShouldSpawnItsEnemies()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(enemies: 3))));

            Tick(arena, Idle, 5);

            Assert.Equal(3, arena.Enemies.Count);
        }

        [Fact]
        public void AWave_ShouldWaitOutItsDelayBeforeSpawning()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(enemies: 2, delay: 1f))));

            TickSeconds(arena, Idle, 0.5f);
            Assert.Empty(arena.Enemies);

            TickSeconds(arena, Idle, 0.7f);
            Assert.Equal(2, arena.Enemies.Count);
        }

        [Fact]
        public void ClearingAWave_ShouldBringOnTheNextOne()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(enemies: 2), Wave(enemies: 4))));
            Tick(arena, Idle, 5);
            Assert.Equal(2, arena.Enemies.Count);

            DefeatEveryone(arena);
            TickUntil(arena, Idle, () => arena.Enemies.Count == 4);

            Assert.Equal(4, arena.Enemies.Count);
        }

        [Fact]
        public void DefeatedBodies_ShouldBeClearedAway()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(enemies: 2))));
            Tick(arena, Idle, 5);

            DefeatEveryone(arena);
            TickSeconds(arena, Idle, 2f);

            Assert.Empty(arena.Enemies);
        }

        [Fact]
        public void ASectionWithNoWaves_ShouldBeWalkedStraightThrough()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f), Section(1600f, Wave())));

            TickUntil(arena, WalkRight, () => arena.CurrentSectionIndex == 1);

            Assert.Equal(1, arena.CurrentSectionIndex);
        }

        // ---------------------------------------------------------------- the camera lock

        [Fact]
        public void TheCamera_ShouldNotScrollPastTheLock_WhileTheWaveIsAlive()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(2400f, Wave(lockX: 400f, enemies: 2))));

            TickUntil(arena, WalkRight, () => arena.Enemies.Count > 0);
            TickSeconds(arena, WalkRight, 5f);

            Assert.Equal(400f, arena.CameraX, 0);
        }

        [Fact]
        public void TheCamera_ShouldAdvance_OnceTheWaveIsCleared()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(2400f, Wave(lockX: 400f, enemies: 2))));
            TickUntil(arena, WalkRight, () => arena.Enemies.Count > 0);
            TickSeconds(arena, WalkRight, 2f);
            Assert.Equal(400f, arena.CameraX, 0);

            DefeatEveryone(arena);
            TickSeconds(arena, WalkRight, 6f);

            Assert.True(arena.CameraX > 400f, "clearing the area should release the advance lock");
        }

        [Fact]
        public void TheCamera_ShouldLockAgainAtTheNextWave()
        {
            CapaoRasoArena arena = NewArena(Stage(
                Section(2400f, Wave(lockX: 400f, enemies: 1), Wave(lockX: 1000f, enemies: 1))));

            TickUntil(arena, WalkRight, () => arena.Enemies.Count > 0);
            DefeatEveryone(arena);
            TickUntil(arena, WalkRight, () => arena.Enemies.Count > 0);
            TickSeconds(arena, WalkRight, 5f);

            Assert.Equal(1000f, arena.CameraX, 0);
        }

        [Fact]
        public void TheSecondWave_ShouldOnlySpawnOnceTheCameraReachesItsLock()
        {
            CapaoRasoArena arena = NewArena(Stage(
                Section(2400f, Wave(lockX: 0f, enemies: 1), Wave(lockX: 1200f, enemies: 1))));
            TickUntil(arena, Idle, () => arena.Enemies.Count > 0);
            DefeatEveryone(arena);

            // Standing still, the camera never reaches the second lock, so nobody shows up.
            TickSeconds(arena, Idle, 4f);

            Assert.Empty(arena.Enemies);
        }

        [Fact]
        public void SofiaShouldNotWalkOffTheLockedScreen()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(2400f, Wave(lockX: 400f, enemies: 2))));

            TickUntil(arena, WalkRight, () => arena.Enemies.Count > 0);
            TickSeconds(arena, WalkRight, 6f);

            Assert.True(arena.Player.Position.X <= arena.CameraX + ViewWidth,
                "the player must stay penned inside the locked arena");
        }

        // ---------------------------------------------------------------- sections & completion

        [Fact]
        public void ReachingTheEndOfASection_ShouldLoadTheNextOne()
        {
            CapaoRasoArena arena = NewArena(Stage(
                Section(1600f, Wave(enemies: 1)),
                Section(1600f, Wave(enemies: 1))));
            TickUntil(arena, Idle, () => arena.Enemies.Count > 0);
            DefeatEveryone(arena);

            TickUntil(arena, WalkRight, () => arena.CurrentSectionIndex == 1);

            Assert.Equal(1, arena.CurrentSectionIndex);
            Assert.False(arena.Completed);
        }

        [Fact]
        public void LoadingASection_ShouldBringItsOwnWave()
        {
            CapaoRasoArena arena = NewArena(Stage(
                Section(1600f, Wave(enemies: 1)),
                Section(1600f, Wave(enemies: 4))));
            TickUntil(arena, Idle, () => arena.Enemies.Count > 0);
            DefeatEveryone(arena);
            TickUntil(arena, WalkRight, () => arena.CurrentSectionIndex == 1);

            TickUntil(arena, Idle, () => arena.Enemies.Count == 4);

            Assert.Equal(4, arena.Enemies.Count);
        }

        [Fact]
        public void ReachingTheEndOfTheLastSection_ShouldCompleteTheStage()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(enemies: 1))));
            TickUntil(arena, Idle, () => arena.Enemies.Count > 0);
            DefeatEveryone(arena);

            TickUntil(arena, WalkRight, () => arena.Completed);

            Assert.True(arena.Completed);
        }

        [Fact]
        public void TheStage_ShouldNotComplete_WhileEnemiesRemain()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(enemies: 2))));

            TickSeconds(arena, WalkRight, 10f);

            Assert.False(arena.Completed);
        }

        [Fact]
        public void AWholeTwoSectionRun_ShouldEndCompleted()
        {
            CapaoRasoArena arena = NewArena(Stage(
                Section(1600f, Wave(enemies: 2), Wave(enemies: 1)),
                Section(1600f, Wave(enemies: 3))));

            // A perfect player: clear the room whenever anyone is up, otherwise keep walking right.
            for (int frame = 0; frame < 6000 && !arena.Completed; frame++)
            {
                if (arena.Enemies.Count > 0)
                    DefeatEveryone(arena);

                arena.Update(Frames.Step(), WalkRight, null);
            }

            Assert.True(arena.Completed, "a stage that is fought and walked through should finish");
            Assert.Equal(1, arena.CurrentSectionIndex);
        }

        // ---------------------------------------------------------------- defeat

        [Fact]
        public void TheRun_ShouldNotEndTheInstantSofiaFalls()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(enemies: 1))));
            arena.Player.TakeDamage(arena.Player.Health, Vector2.Zero);

            Tick(arena, Idle, 2);

            Assert.False(arena.PlayerDefeated);
        }

        [Fact]
        public void TheRun_ShouldEnd_ShortlyAfterSofiaFalls()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(enemies: 1))));
            arena.Player.TakeDamage(arena.Player.Health, Vector2.Zero);

            TickSeconds(arena, Idle, 1.5f);

            Assert.True(arena.PlayerDefeated);
        }

        // ---------------------------------------------------------------- combat resolution

        [Fact]
        public void SofiasSwing_ShouldHurtAnEnemyStandingInFrontOfHer()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(enemies: 1))));
            TickUntil(arena, Idle, () => arena.Enemies.Count > 0);
            PiaLocoEnemy enemy = arena.Enemies[0];
            PutInFrontOfSofia(arena, enemy);
            int health = enemy.Health;

            SwingAndResolve(arena);

            Assert.True(enemy.Health < health, "the blow should have landed");
        }

        [Fact]
        public void SofiasSwing_ShouldHitEveryEnemyItOverlaps()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(enemies: 2))));
            TickUntil(arena, Idle, () => arena.Enemies.Count == 2);
            foreach (PiaLocoEnemy enemy in arena.Enemies)
                PutInFrontOfSofia(arena, enemy);

            List<int> before = arena.Enemies.Select(e => e.Health).ToList();
            SwingAndResolve(arena);

            Assert.All(arena.Enemies.Select((e, i) => (e, i)),
                pair => Assert.True(pair.e.Health < before[pair.i], "every overlapped enemy should be hit"));
        }

        [Fact]
        public void SofiasSwing_ShouldMissAnEnemyAcrossTheStreet()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(enemies: 1))));
            TickUntil(arena, Idle, () => arena.Enemies.Count > 0);
            PiaLocoEnemy enemy = arena.Enemies[0];
            enemy.Position = arena.Player.Position + new Vector2(600f, 0f);
            int health = enemy.Health;

            SwingAndResolve(arena);

            Assert.Equal(health, enemy.Health);
        }

        [Fact]
        public void OneSwing_ShouldOnlyHitAnEnemyOnce()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(enemies: 1))));
            TickUntil(arena, Idle, () => arena.Enemies.Count > 0);
            PiaLocoEnemy enemy = arena.Enemies[0];
            PutInFrontOfSofia(arena, enemy);
            int health = enemy.Health;

            // Hold the enemy in place through the whole swing, so only the dedupe stops a second hit.
            Vector2 spot = enemy.Position;
            arena.Player.RequestAttack();
            for (int i = 0; i < Frames.FramesFor(0.5f); i++)
            {
                enemy.Position = spot;
                arena.Update(Frames.Step(), Idle, null);
            }

            // Sofia's first combo link does 10; a second hit in the same swing would show as 20.
            Assert.Equal(health - 10, enemy.Health);
        }

        [Fact]
        public void EnoughSwings_ShouldDefeatAnEnemy()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(enemies: 1))));
            TickUntil(arena, Idle, () => arena.Enemies.Count > 0);
            PiaLocoEnemy enemy = arena.Enemies[0];

            for (int i = 0; i < 12 && !enemy.IsDefeated; i++)
            {
                PutInFrontOfSofia(arena, enemy);
                SwingAndResolve(arena);
                TickSeconds(arena, Idle, 0.4f);
            }

            Assert.True(enemy.IsDefeated);
        }

        [Fact]
        public void AnEnemySwing_ShouldHurtSofia()
        {
            CapaoRasoArena arena = NewArena(Stage(Section(1600f, Wave(enemies: 1))));
            TickUntil(arena, Idle, () => arena.Enemies.Count > 0);
            int health = arena.Player.Health;

            // Let the AI walk in and take its turn.
            TickUntil(arena, Idle, () => arena.Player.Health < health, maxFrames: 3000);

            Assert.True(arena.Player.Health < health, "an enemy should eventually land a blow");
        }

        /// <summary>Stands the enemy just within Sofia's reach, on her lane and in her facing.</summary>
        private static void PutInFrontOfSofia(CapaoRasoArena arena, PiaLocoEnemy enemy)
        {
            arena.Player.Facing = Curitiba.Core.FaceDirection.Right;
            enemy.Position = new Vector2(arena.Player.Position.X + 40f, arena.Player.Position.Y);
        }

        /// <summary>Swings and ticks far enough for the active frames to pass.</summary>
        private static void SwingAndResolve(CapaoRasoArena arena)
        {
            arena.Player.RequestAttack();
            TickSeconds(arena, Idle, 0.2f);
        }
    }
}
