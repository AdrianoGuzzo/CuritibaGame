using System.Collections.Generic;
using Curitiba.Core.BeatEmUp;

namespace Curitiba.Core.Audio
{
    /// <summary>
    /// Which blow is audible, how loud, and the bank of impacts a punch draws from. The arena
    /// detects a hit and asks here; nothing about impact sound is decided anywhere else.
    /// </summary>
    /// <remarks>
    /// The rule reads <see cref="AttackType"/> because that is the only identity a landed blow
    /// carries out of the fighter — the <see cref="Combat.ComboMove"/> that produced it is private,
    /// so "punch1 vs punch2" never reaches the arena. That is enough: what separates a punch from
    /// the finisher kick is exactly the weight class.
    /// <para>
    /// A punch does not have <em>a</em> sound but a bank of them, spent in turn by
    /// <see cref="PunchSoundRotation"/>. One sample fired on every blow is audibly a loop, and a
    /// brawler is nothing but blows.
    /// </para>
    /// </remarks>
    internal static class CombatSounds
    {
        /// <summary>The punch impacts, as the content pipeline names them.</summary>
        /// <remarks>
        /// Order is the order they are heard in, and is not meaningful beyond that — what matters
        /// is that there are several and that they are all different.
        /// </remarks>
        public static readonly IReadOnlyList<string> PunchHits = new[]
        {
            "Sounds/PunchHit1",
            "Sounds/PunchHit2",
            "Sounds/PunchHit3",
            "Sounds/PunchHit4",
            "Sounds/PunchHit5",
        };

        /// <summary>
        /// How loud an impact lands. Deliberately well above
        /// <see cref="ArenaMusicPolicy.BackgroundVolume"/>: the music is a bed at 0.30 and a hit
        /// underneath it would not read as a hit.
        /// </summary>
        public const float PunchHitVolume = 0.7f;

        /// <summary>
        /// Whether a blow the player landed is one of the punches, which are the blows that have a
        /// sound. The kick and the jump kick are silent on purpose — they are waiting for sounds of
        /// their own, and borrowing the punch's would make the finisher land like a jab.
        /// </summary>
        internal static bool IsPunch(AttackType type) =>
            type == AttackType.Normal || type == AttackType.Heavy;
    }
}
