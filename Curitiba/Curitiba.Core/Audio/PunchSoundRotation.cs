using System.Collections.Generic;

namespace Curitiba.Core.Audio
{
    /// <summary>
    /// Hands out the punch impacts in turn, so a string of blows never sounds the same sample twice
    /// running.
    /// </summary>
    /// <remarks>
    /// A plain cycle rather than a random pick, for two reasons. It is deterministic, so the rule
    /// is testable without dragging a <c>Random</c> seam into the arena; and it spreads the bank
    /// perfectly, where random picking would sooner or later fire the same impact twice in a row —
    /// which is the very thing that reads as "repetitive".
    /// <para>
    /// The rotation belongs to the arena, so a new stage starts the cycle over. Where in the cycle
    /// a fight happens to be is not state worth carrying between runs.
    /// </para>
    /// </remarks>
    internal sealed class PunchSoundRotation
    {
        private readonly IReadOnlyList<string> variants;

        private int next;

        /// <param name="variants">The bank to cycle; null uses <see cref="CombatSounds.PunchHits"/>.</param>
        public PunchSoundRotation(IReadOnlyList<string> variants = null)
        {
            this.variants = variants ?? CombatSounds.PunchHits;
        }

        /// <summary>The impact for the punch about to land, advancing the rotation.</summary>
        public string Advance()
        {
            string asset = variants[next];
            next = (next + 1) % variants.Count;
            return asset;
        }
    }
}
