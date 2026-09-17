namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// How much a defeated enemy is worth to the score.
    /// </summary>
    /// <remarks>
    /// This is a <em>scoring tier</em>, not the spawn key the factory resolves — that one is the
    /// string <c>SpawnDef.Type</c> matched against <see cref="EnemyFactory.DefaultType"/>. The
    /// stage data has no tiers yet (every enemy is the one registered mook), so callers pass
    /// <see cref="Normal"/> until stronger enemies exist. The points per tier are balancing
    /// numbers and live in <see cref="ScoreConfig.EnemyPoints"/>.
    /// </remarks>
    internal enum EnemyType
    {
        /// <summary>The common mook.</summary>
        Normal,

        /// <summary>A tougher variant, worth more.</summary>
        Strong,

        /// <summary>A stage boss.</summary>
        Boss,
    }
}
