namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// One combat wave at runtime. In a <see cref="SectionMode.Scroll"/> section, when the camera
    /// reaches <see cref="LockCameraX"/> it locks there and the wave is armed; after <see cref="Delay"/>
    /// the enemies spawn, and the lock releases once they are all defeated. In a
    /// <see cref="SectionMode.Frame"/> section <see cref="LockCameraX"/> is ignored — the wave is
    /// armed when the frame loads. When <see cref="SpawnDefs"/> is non-empty those authored enemies are
    /// used; otherwise <see cref="EnemyCount"/> enemies are placed by the legacy procedural spread.
    /// Position resolution (off-screen birth + walk-in target) happens in <see cref="SpawnManager"/>
    /// at spawn time, since it depends on the live camera.
    /// </summary>
    internal class SpawnArea
    {
        public float LockCameraX { get; }
        public int EnemyCount { get; }

        /// <summary>Blows in a row this area's enemies absorb before being knocked down (difficulty).</summary>
        public int HitsToKnockdown { get; }

        /// <summary>Seconds to wait, after the wave is triggered, before its enemies appear.</summary>
        public float Delay { get; }

        /// <summary>Authored enemy entries; null or empty falls back to the procedural spread.</summary>
        public SpawnDef[] SpawnDefs { get; }

        public SpawnArea(float lockCameraX, int enemyCount, int hitsToKnockdown, float delay, SpawnDef[] spawnDefs = null)
        {
            LockCameraX = lockCameraX;
            EnemyCount = enemyCount;
            HitsToKnockdown = hitsToKnockdown;
            Delay = delay;
            SpawnDefs = spawnDefs;
        }
    }
}
