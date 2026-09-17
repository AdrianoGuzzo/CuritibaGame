namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// The weight class of a landed blow, as the scoring rules see it. Deliberately coarser than
    /// the combo chain: a string of four <see cref="Combat.ComboMove"/>s may well be three
    /// <see cref="Normal"/>s and one <see cref="Finisher"/>.
    /// </summary>
    /// <remarks>
    /// The members carry no numeric values on purpose. What each type is worth is a balancing
    /// number and lives in <see cref="ScoreConfig.HitPoints"/>, so it can be turned without
    /// touching code — and so casting an <see cref="AttackType"/> to an int never means "points".
    /// </remarks>
    internal enum AttackType
    {
        /// <summary>A jab from the ground string: the cheapest, most repeatable blow.</summary>
        Normal,

        /// <summary>A heavier ground blow, slower to throw and worth more.</summary>
        Heavy,

        /// <summary>Landed while airborne (the jump kick).</summary>
        Air,

        /// <summary>The blow that closes a string and launches the target.</summary>
        Finisher,
    }
}
