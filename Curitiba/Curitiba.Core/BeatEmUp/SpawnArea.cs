namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// One combat area along the stage. When the camera reaches <see cref="LockCameraX"/>
    /// it locks there and a wave of <see cref="EnemyCount"/> enemies is spawned; the lock
    /// releases once they are all defeated.
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
