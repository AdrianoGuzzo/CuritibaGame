namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// The enemy's internal AI state, layered on top of the shared <see cref="FighterState"/>
    /// animation machine. Positioning/Cooldown shuffle around the player keeping spacing;
    /// Engaging commits a turn (steps in to strike); Attack mirrors a live swing.
    /// </summary>
    internal enum EnemyAiState
    {
        Idle,
        Positioning,
        Engaging,
        Attack,
        Cooldown,
    }

    /// <summary>Behaviour archetype that tunes how an enemy fights.</summary>
    internal enum EnemyPersonality
    {
        Aggressive,
        Defensive,
        Balanced,
        Runner,
    }

    /// <summary>
    /// Data-driven knobs for an <see cref="EnemyPersonality"/>. A single enemy class
    /// (<see cref="PiaLocoEnemy"/>) reads these to vary its behaviour instead of needing a
    /// subclass per archetype. Tune the numbers here in one place.
    /// </summary>
    internal struct EnemyProfile
    {
        public EnemyPersonality Profile;

        /// <summary>0..1 chance to commit a turn when an opening exists (eagerness).</summary>
        public float AttackChance;

        /// <summary>Seconds between this enemy's own swings, and its post-swing recovery.</summary>
        public float AttackCooldown;

        /// <summary>Spacing a cautious enemy tries to keep from the player.</summary>
        public float PreferredDistance;

        /// <summary>Runner: gap to the player that triggers a sprint to close in.</summary>
        public float RunDistance;

        /// <summary>Runner: speed multiplier while sprinting.</summary>
        public float RunSpeedMultiplier;

        public static EnemyProfile From(EnemyPersonality personality)
        {
            switch (personality)
            {
                case EnemyPersonality.Aggressive:
                    return new EnemyProfile
                    {
                        Profile = personality,
                        AttackChance = 0.92f,
                        AttackCooldown = 0.8f,
                        PreferredDistance = 40f,
                        RunDistance = 320f,
                        RunSpeedMultiplier = 1.6f,
                    };

                case EnemyPersonality.Defensive:
                    return new EnemyProfile
                    {
                        Profile = personality,
                        AttackChance = 0.4f,
                        AttackCooldown = 1.8f,
                        PreferredDistance = 120f,
                        RunDistance = 320f,
                        RunSpeedMultiplier = 1.4f,
                    };

                case EnemyPersonality.Runner:
                    return new EnemyProfile
                    {
                        Profile = personality,
                        AttackChance = 0.72f,
                        AttackCooldown = 1.2f,
                        PreferredDistance = 60f,
                        RunDistance = 220f,
                        RunSpeedMultiplier = 1.9f,
                    };

                case EnemyPersonality.Balanced:
                default:
                    return new EnemyProfile
                    {
                        Profile = EnemyPersonality.Balanced,
                        AttackChance = 0.7f,
                        AttackCooldown = 1.3f,
                        PreferredDistance = 60f,
                        RunDistance = 300f,
                        RunSpeedMultiplier = 1.5f,
                    };
            }
        }
    }
}
