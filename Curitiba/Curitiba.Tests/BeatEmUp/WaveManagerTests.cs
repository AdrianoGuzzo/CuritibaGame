using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// Wave sequencing: which wave is current, when its enemies are due, and when the section is done.
    /// This is the single source of truth the arena asks before spawning anything.
    /// </summary>
    public class WaveManagerTests
    {
        private static SpawnArea Wave(float lockX = 0f, int enemyCount = 2, float delay = 0f,
                                      int hitsToKnockdown = 3, SpawnDef[] spawns = null) =>
            new SpawnArea(lockX, enemyCount, hitsToKnockdown, delay, spawns);

        [Fact]
        public void NewManager_ShouldHaveNoWaves()
        {
            var manager = new WaveManager();

            Assert.False(manager.HasWaves);
        }

        [Fact]
        public void Reset_ShouldTolerateNull()
        {
            var manager = new WaveManager();

            manager.Reset(null);

            Assert.False(manager.HasWaves);
        }

        [Fact]
        public void Reset_ShouldStartAtTheFirstWave()
        {
            var manager = new WaveManager();

            manager.Reset(new[] { Wave(lockX: 400f), Wave(lockX: 900f) });

            Assert.True(manager.HasWaves);
            Assert.Equal(400f, manager.CurrentLockX);
            Assert.False(manager.HasSpawnedCurrent);
        }

        [Fact]
        public void TickReadyToSpawn_ShouldNotFire_BeforeTheWaveIsArmed()
        {
            var manager = new WaveManager();
            manager.Reset(new[] { Wave() });

            Assert.False(manager.TickReadyToSpawn(1f));
        }

        [Fact]
        public void ArmedWaveWithNoDelay_ShouldSpawnOnTheNextTick()
        {
            var manager = new WaveManager();
            manager.Reset(new[] { Wave(delay: 0f) });

            manager.Arm();

            Assert.True(manager.TickReadyToSpawn(Frames.DefaultStep));
            Assert.True(manager.HasSpawnedCurrent);
        }

        [Fact]
        public void ArmedWave_ShouldWaitOutItsDelay()
        {
            var manager = new WaveManager();
            manager.Reset(new[] { Wave(delay: 0.5f) });
            manager.Arm();

            // 29 frames at 1/60s is 0.4833s, still short of the delay.
            int fired = 0;
            for (int i = 0; i < 29; i++)
            {
                if (manager.TickReadyToSpawn(Frames.DefaultStep))
                    fired++;
            }
            Assert.Equal(0, fired);

            for (int i = 0; i < 3; i++)
            {
                if (manager.TickReadyToSpawn(Frames.DefaultStep))
                    fired++;
            }
            Assert.Equal(1, fired);
        }

        [Fact]
        public void TickReadyToSpawn_ShouldFireExactlyOnce()
        {
            var manager = new WaveManager();
            manager.Reset(new[] { Wave(delay: 0f) });
            manager.Arm();

            int fired = 0;
            for (int i = 0; i < 120; i++)
            {
                if (manager.TickReadyToSpawn(Frames.DefaultStep))
                    fired++;
            }

            Assert.Equal(1, fired);
        }

        [Fact]
        public void Advance_ShouldMoveToTheNextWave_AndResetItsSpawnState()
        {
            var manager = new WaveManager();
            manager.Reset(new[] { Wave(lockX: 400f), Wave(lockX: 900f) });
            manager.Arm();
            manager.TickReadyToSpawn(1f);
            Assert.True(manager.HasSpawnedCurrent);

            bool advanced = manager.Advance();

            Assert.True(advanced);
            Assert.Equal(900f, manager.CurrentLockX);
            Assert.False(manager.HasSpawnedCurrent);
        }

        [Fact]
        public void Advance_ShouldReturnFalse_OnTheLastWave()
        {
            var manager = new WaveManager();
            manager.Reset(new[] { Wave() });

            Assert.False(manager.Advance());
        }

        [Fact]
        public void AdvancedWave_ShouldNeedArmingAgain()
        {
            var manager = new WaveManager();
            manager.Reset(new[] { Wave(), Wave(delay: 0f) });
            manager.Arm();
            manager.TickReadyToSpawn(1f);
            manager.Advance();

            // Not armed yet: the arena decides when (camera lock reached, or frame section ready).
            Assert.False(manager.TickReadyToSpawn(1f));

            manager.Arm();
            Assert.True(manager.TickReadyToSpawn(1f));
        }

        [Fact]
        public void Current_ShouldExposeTheWaveBeingFought()
        {
            var manager = new WaveManager();
            SpawnArea first = Wave(enemyCount: 2);
            SpawnArea second = Wave(enemyCount: 5);
            manager.Reset(new[] { first, second });

            Assert.Same(first, manager.Current);
            manager.Advance();
            Assert.Same(second, manager.Current);
        }

        [Fact]
        public void Rearming_ShouldRestartTheDelay()
        {
            var manager = new WaveManager();
            manager.Reset(new[] { Wave(delay: 0.5f) });

            manager.Arm();
            manager.TickReadyToSpawn(0.4f);
            manager.Arm();

            // The budget restarted, so 0.4s is no longer enough.
            Assert.False(manager.TickReadyToSpawn(0.4f));
            Assert.True(manager.TickReadyToSpawn(0.2f));
        }

        [Fact]
        public void FullSectionRun_ShouldWalkEveryWaveThenReportDone()
        {
            var manager = new WaveManager();
            manager.Reset(new[] { Wave(), Wave(), Wave() });

            int spawned = 0;
            int waves = 0;
            while (true)
            {
                manager.Arm();
                if (manager.TickReadyToSpawn(1f))
                    spawned++;
                waves++;

                if (!manager.Advance())
                    break;
            }

            Assert.Equal(3, spawned);
            Assert.Equal(3, waves);
        }
    }

    /// <summary>The immutable wave data the manager sequences.</summary>
    public class SpawnAreaTests
    {
        [Fact]
        public void SpawnArea_ShouldKeepItsAuthoredValues()
        {
            var area = new SpawnArea(lockCameraX: 400f, enemyCount: 3, hitsToKnockdown: 4, delay: 0.5f);

            Assert.Equal(400f, area.LockCameraX);
            Assert.Equal(3, area.EnemyCount);
            Assert.Equal(4, area.HitsToKnockdown);
            Assert.Equal(0.5f, area.Delay);
            Assert.Null(area.SpawnDefs);
        }

        [Fact]
        public void SpawnArea_ShouldCarryAuthoredSpawns_WhenGivenThem()
        {
            var spawns = new[] { new SpawnDef(), new SpawnDef() };

            var area = new SpawnArea(0f, 0, 3, 0f, spawns);

            Assert.Equal(2, area.SpawnDefs.Length);
        }
    }
}
