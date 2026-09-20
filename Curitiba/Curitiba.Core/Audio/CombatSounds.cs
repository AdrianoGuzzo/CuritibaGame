using System.Collections.Generic;
using Curitiba.Core.BeatEmUp;

namespace Curitiba.Core.Audio
{
    /// <summary>
    /// Which blow is audible, at which moment of the blow, how loud, the bank of impacts a landed
    /// hit draws from, and the voice a floored body lets out. The arena detects a swing, a hit and
    /// a fall and asks here; nothing about combat sound is decided anywhere else.
    /// </summary>
    /// <remarks>
    /// The rules read <see cref="AttackType"/> because that is the only identity a blow carries out
    /// of the fighter — the <see cref="Combat.ComboMove"/> that produced it is private, so "punch1
    /// vs punch2" never reaches the arena. That is enough: what separates a punch from the finisher
    /// kick is exactly the weight class.
    /// <para>
    /// A blow has two audible moments and they are not the same sound. The <em>swing</em> fires as
    /// the move opens, before anything is hit, and only the kick has one — a whoosh on every jab
    /// would fire several times a second and turn a string into noise. The <em>impact</em> fires on
    /// contact, and there the kick simply borrows the punch bank: flesh is flesh, and the kick
    /// already has its own identity from the swing that announced it.
    /// </para>
    /// <para>
    /// A punch does not have <em>a</em> sound but a bank of them, spent in turn by
    /// <see cref="SoundRotation"/>. One sample fired on every blow is audibly a loop, and a
    /// brawler is nothing but blows.
    /// </para>
    /// <para>
    /// The fall is the odd one out: it is the sound of a <em>body</em>, not of a blow, so it has
    /// no <see cref="AttackType"/> to read and fires once per floored fighter rather than once per
    /// swing. What puts a body on the floor is the fighter's business, not this class's.
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

        /// <summary>The air the finisher kick cuts on its way out, sounded as the swing opens.</summary>
        public const string KickSwing = "Sounds/KickSwing";

        /// <summary>
        /// The grunt a Pia Loco lets out as it reaches the ground — knocked down or dead.
        /// </summary>
        /// <remarks>
        /// A voice, not a body hitting the floor: should a thud ever join it, that is a second
        /// asset layered on top rather than a replacement for this one.
        /// </remarks>
        public const string EnemyFall = "Sounds/EnemyFall";

        /// <summary>
        /// How loud an impact lands. Deliberately well above
        /// <see cref="ArenaMusicPolicy.BackgroundVolume"/>: the music is a bed at 0.30 and a hit
        /// underneath it would not read as a hit.
        /// </summary>
        public const float PunchHitVolume = 0.7f;

        /// <summary>
        /// How loud the kick's windup is: over the music bed, but under the impact it announces.
        /// The whoosh is the anticipation and the contact is the payoff — a windup as loud as the
        /// hit steals it.
        /// </summary>
        public const float KickSwingVolume = 0.6f;

        /// <summary>
        /// How loud a body reaching the ground is: level with <see cref="PunchHitVolume"/>. The
        /// blow and the fall it causes are a single event to the ear, and a grunt tucked under the
        /// punch would read as its tail instead of as the payoff of the string.
        /// </summary>
        public const float EnemyFallVolume = 0.7f;

        /// <summary>
        /// Whether a blow the player landed makes a sound of contact. Every blow that reaches flesh
        /// does, except the jump kick, which is still waiting for a sound of its own — and would
        /// land as a jab if it borrowed the punch's.
        /// </summary>
        internal static bool HasImpactSound(AttackType type) =>
            type == AttackType.Normal || type == AttackType.Heavy || type == AttackType.Finisher;

        /// <summary>
        /// The sound a blow makes on its way out, or null for a blow that swings silently — which
        /// is every blow but the kick.
        /// </summary>
        internal static string SwingSoundFor(AttackType type) =>
            type == AttackType.Finisher ? KickSwing : null;
    }
}
