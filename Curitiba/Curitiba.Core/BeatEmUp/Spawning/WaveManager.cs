using System;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Sequences the waves of a single section: which wave is current, its spawn <c>Delay</c>, and
    /// when the section has been cleared. It is the single source of truth for wave progression; the
    /// arena keeps the camera/phase and asks this manager <em>when</em> to spawn and <em>which</em>
    /// wave. Decoupling timing from the camera lock leaves room to grow: the spawn decision is the
    /// natural seam for future triggers (ambush on an enemy's defeat, a timed event, a boss gate) —
    /// add them around <see cref="Arm"/>/<see cref="TickReadyToSpawn"/> without touching the arena.
    /// </summary>
    internal sealed class WaveManager
    {
        private SpawnArea[] waves = Array.Empty<SpawnArea>();
        private int current;
        private float delayTimer;
        private bool armed;
        private bool spawned;

        public void Reset(SpawnArea[] sectionWaves)
        {
            waves = sectionWaves ?? Array.Empty<SpawnArea>();
            current = 0;
            armed = false;
            spawned = false;
            delayTimer = 0f;
        }

        public bool HasWaves => waves.Length > 0;
        public bool HasSpawnedCurrent => spawned;
        public SpawnArea Current => waves[current];
        public float CurrentLockX => waves[current].LockCameraX;

        /// <summary>Begins the current wave's spawn delay. Called when the arena reaches the lock point
        /// (scroll sections) or when a frame section / next frame-wave is ready.</summary>
        public void Arm()
        {
            armed = true;
            spawned = false;
            delayTimer = waves[current].Delay;
        }

        /// <summary>Ticks the armed delay; returns true exactly once, the frame the wave should spawn.</summary>
        public bool TickReadyToSpawn(float dt)
        {
            if (!armed || spawned)
                return false;

            delayTimer -= dt;
            if (delayTimer > 0f)
                return false;

            spawned = true;
            return true;
        }

        /// <summary>Advances to the next wave if there is one (resetting its arm/spawn state). Returns
        /// false when the current wave was the last — i.e. the section is finished.</summary>
        public bool Advance()
        {
            if (current + 1 >= waves.Length)
                return false;

            current++;
            armed = false;
            spawned = false;
            delayTimer = 0f;
            return true;
        }
    }
}
