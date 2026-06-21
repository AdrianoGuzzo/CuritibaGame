namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// One combat wave. In a <see cref="SectionMode.Scroll"/> section, when the camera
    /// reaches <see cref="LockCameraX"/> it locks there and a wave of <see cref="EnemyCount"/>
    /// enemies is spawned; the lock releases once they are all defeated. In a
    /// <see cref="SectionMode.Frame"/> section <see cref="LockCameraX"/> is ignored — the wave
    /// is spawned immediately when the frame loads.
    /// </summary>
    internal class SpawnArea
    {
        public float LockCameraX { get; }
        public int EnemyCount { get; }

        /// <summary>Blows in a row this area's enemies absorb before being knocked down (difficulty).</summary>
        public int HitsToKnockdown { get; }

        public SpawnArea(float lockCameraX, int enemyCount, int hitsToKnockdown)
        {
            LockCameraX = lockCameraX;
            EnemyCount = enemyCount;
            HitsToKnockdown = hitsToKnockdown;
        }
    }
}
