using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Curitiba.Core.Audio
{
    /// <summary>
    /// The crowd's own voice: the bank a Piá Loco moans from, how loud it sits, and how long the
    /// corridor waits between moans. The arena asks here; nothing about ambience is decided
    /// anywhere else.
    /// </summary>
    /// <remarks>
    /// The sibling of <see cref="CombatSounds"/>, and deliberately a separate class rather than a
    /// few more constants on it. They are two different channels of meaning: combat sound is
    /// feedback the player acts on — it fires because <em>she</em> did something, and it has to
    /// read over the music. Ambience fires because the crowd simply <em>exists</em>, and it has to
    /// sit under everything. Mixing them into one class would put a number nobody may raise next
    /// to a number nobody may lower.
    /// <para>
    /// This is why <see cref="MoanVolume"/> is the first sound in the game under
    /// <see cref="ArenaMusicPolicy.BackgroundVolume"/>: presence is something the player notices
    /// without listening for it. A moan that read over the bed would be an event, which is the
    /// opposite of what it is for.
    /// </para>
    /// <para>
    /// The gap between moans tightens with the crowd instead of every zombie keeping its own
    /// clock. On a channel with one voice and no panning, a moan from the zombie on the left is
    /// audibly identical to one from the zombie on the right — so what the ear can actually hear
    /// as "each zombie moans" is <em>frequency proportional to how many there are</em>, which is
    /// exactly what <see cref="WindowFor"/> delivers. Per-zombie timers would only stack moans on
    /// top of each other for no audible gain.
    /// </para>
    /// </remarks>
    internal static class ZombieAmbience
    {
        /// <summary>The moans, as the content pipeline names them.</summary>
        /// <remarks>
        /// Three pitch variants of one recording. Order is the order they are heard in and is not
        /// meaningful beyond that — what matters is that there are several and that a run of them
        /// does not repeat.
        /// </remarks>
        public static readonly IReadOnlyList<string> Moans = new[]
        {
            "Sounds/ZombieMoan1",
            "Sounds/ZombieMoan2",
            "Sounds/ZombieMoan3",
        };

        /// <summary>
        /// How loud a moan sits. Deliberately <em>under</em>
        /// <see cref="ArenaMusicPolicy.BackgroundVolume"/> — see the class remarks.
        /// </summary>
        public const float MoanVolume = 0.18f;

        /// <summary>The crowd size at which the gap stops tightening.</summary>
        /// <remarks>
        /// Without a floor, a big authored wave would moan continuously, which reads as a stuck
        /// sound rather than as a lot of zombies.
        /// </remarks>
        public const int FullCrowd = 5;

        /// <summary>The gap either side of the <em>first</em> moan after a corridor fills up.</summary>
        /// <remarks>
        /// Far shorter than any steady gap, and a window of its own rather than a fraction of
        /// one: a wave the player can see should be a wave the player can hear. Several seconds
        /// of silence over visible zombies reads as the sound being broken, not as restraint.
        /// It is still not instant — a moan on the very frame the first body appears sounds
        /// triggered by the spawn rather than by the zombies.
        /// </remarks>
        public const float OpeningMoanMin = 0.5f;

        /// <inheritdoc cref="OpeningMoanMin"/>
        public const float OpeningMoanMax = 1.5f;

        /// <summary>The gap either side of a single zombie's moan, in seconds.</summary>
        public const float LonelyWindowMin = 5f;

        /// <inheritdoc cref="LonelyWindowMin"/>
        public const float LonelyWindowMax = 8f;

        /// <summary>The gap either side of a moan with <see cref="FullCrowd"/> on screen.</summary>
        /// <remarks>
        /// The shipped stage's waves hold two to four, so it is the middle of this curve that is
        /// actually heard — a wave of three lands near 3.5 to 5.75 s. The ends only bracket it.
        /// <para>
        /// These gaps are shorter than the samples themselves (~3.5 to 4.3 s), so a packed
        /// corridor will overlap moans. That is the intent at this density: overlapping voices
        /// read as more zombies. If it ever reads as mush instead, the lever is the length of the
        /// samples, not this floor.
        /// </para>
        /// </remarks>
        public const float CrowdWindowMin = 2f;

        /// <inheritdoc cref="CrowdWindowMin"/>
        public const float CrowdWindowMax = 3.5f;

        /// <summary>
        /// How long to wait before the next moan, as the range to draw from. Tightens linearly
        /// from the lonely window at one zombie to the crowd window at <see cref="FullCrowd"/>.
        /// </summary>
        /// <param name="zombiesAlive">
        /// How many are still on their feet. Zero and one share the lonely window — an empty
        /// corridor is silent anyway, and the caller, not the range, decides that.
        /// </param>
        internal static (float Min, float Max) WindowFor(int zombiesAlive)
        {
            int alive = Math.Max(1, zombiesAlive);
            if (alive >= FullCrowd) return (CrowdWindowMin, CrowdWindowMax);

            float crowded = (alive - 1) / (float)(FullCrowd - 1);
            return (MathHelper.Lerp(LonelyWindowMin, CrowdWindowMin, crowded),
                    MathHelper.Lerp(LonelyWindowMax, CrowdWindowMax, crowded));
        }
    }
}
