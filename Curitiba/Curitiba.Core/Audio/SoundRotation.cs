using System.Collections.Generic;

namespace Curitiba.Core.Audio
{
    /// <summary>
    /// Hands out a bank of interchangeable variants in turn, so a run of the same event never
    /// sounds the same sample twice running.
    /// </summary>
    /// <remarks>
    /// A plain cycle rather than a random pick, for two reasons. It is deterministic, so the rule
    /// is testable without dragging a <c>Random</c> seam into the arena; and it spreads the bank
    /// perfectly, where random picking would sooner or later fire the same sample twice in a row —
    /// which is the very thing that reads as "repetitive".
    /// <para>
    /// Two banks use it: the punch impacts (<see cref="CombatSounds.PunchHits"/>, the default) and
    /// the crowd's moans (<see cref="ZombieAmbience.Moans"/>). Both belong to the arena, so a new
    /// stage starts each cycle over. Where in the cycle a fight happens to be is not state worth
    /// carrying between runs.
    /// </para>
    /// </remarks>
    internal sealed class SoundRotation
    {
        private readonly IReadOnlyList<string> variants;

        private int next;

        /// <param name="variants">The bank to cycle; null uses <see cref="CombatSounds.PunchHits"/>.</param>
        public SoundRotation(IReadOnlyList<string> variants = null)
        {
            this.variants = variants ?? CombatSounds.PunchHits;
        }

        /// <summary>The variant for the sound about to fire, advancing the rotation.</summary>
        public string Advance()
        {
            string asset = variants[next];
            next = (next + 1) % variants.Count;
            return asset;
        }
    }
}
