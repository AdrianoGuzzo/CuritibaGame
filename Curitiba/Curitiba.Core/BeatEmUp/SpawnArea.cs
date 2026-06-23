using Microsoft.Xna.Framework;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>One explicitly placed enemy in a wave, fully resolved (profile + tuning + position).</summary>
    internal sealed class EnemySpawn
    {
        public Vector2 Position;
        public EnemyProfile Profile;
        public FighterTuning Tuning;
    }

    /// <summary>
    /// One combat wave. In a <see cref="SectionMode.Scroll"/> section, when the camera
    /// reaches <see cref="LockCameraX"/> it locks there and the wave is spawned; the lock
    /// releases once the enemies are all defeated. In a <see cref="SectionMode.Frame"/>
    /// section <see cref="LockCameraX"/> is ignored — the wave spawns when the frame loads.
    /// When <see cref="Spawns"/> is non-empty the enemies are placed at those explicit points
    /// (what the editor authors); otherwise <see cref="EnemyCount"/> enemies are placed by the
    /// legacy procedural spread.
    /// </summary>
    internal class SpawnArea
    {
        public float LockCameraX { get; }
        public int EnemyCount { get; }

        /// <summary>Blows in a row this area's enemies absorb before being knocked down (difficulty).</summary>
        public int HitsToKnockdown { get; }

        /// <summary>Explicit enemy placements; null or empty falls back to the procedural spread.</summary>
        public EnemySpawn[] Spawns { get; }

        public SpawnArea(float lockCameraX, int enemyCount, int hitsToKnockdown, EnemySpawn[] spawns = null)
        {
            LockCameraX = lockCameraX;
            EnemyCount = enemyCount;
            HitsToKnockdown = hitsToKnockdown;
            Spawns = spawns;
        }
    }
}
