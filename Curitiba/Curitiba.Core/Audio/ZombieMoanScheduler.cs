using System;
using System.Collections.Generic;

namespace Curitiba.Core.Audio
{
    /// <summary>
    /// Decides when the crowd moans. Ticked every frame with how many Piá Locos are on their
    /// feet, it hands back the moan to fire — or nothing, which is most frames.
    /// </summary>
    /// <remarks>
    /// The split against <see cref="SoundRotation"/> is deliberate and is the whole design: the
    /// <em>instant</em> is drawn at random, so the crowd never sounds like a metronome, while
    /// <em>which</em> variant comes out is a plain cycle, so a run of moans spends the bank and
    /// cannot repeat back to back. Randomising both would sooner or later moan the same sample
    /// twice running, which is exactly what reads as repetitive.
    /// <para>
    /// The <see cref="Random"/> arrives through the constructor rather than being made here, so a
    /// test can pin the seed. It is the only non-deterministic thing in the type, and everything
    /// else — the floor and ceiling of the window, the rearm, the empty corridor — is ordinary
    /// arithmetic that holds for every seed.
    /// </para>
    /// <para>
    /// The scheduler belongs to the arena, so a new stage starts its cycle and its clock over,
    /// and pausing the game pauses the ambience for free: the arena only ticks from the screen's
    /// <c>HandleInput</c>.
    /// </para>
    /// </remarks>
    internal sealed class ZombieMoanScheduler
    {
        private readonly SoundRotation rotation;
        private readonly Random rng;

        private float untilNextMoan;
        private bool armed;

        /// <param name="variants">The bank to cycle; null uses <see cref="ZombieAmbience.Moans"/>.</param>
        /// <param name="rng">The source of the gap; null makes its own.</param>
        public ZombieMoanScheduler(IReadOnlyList<string> variants = null, Random rng = null)
        {
            rotation = new SoundRotation(variants ?? ZombieAmbience.Moans);
            this.rng = rng ?? new Random();
        }

        /// <summary>
        /// The moan to fire this frame, or null. Never more than one: two copies of the same
        /// one-shot in a single frame only sum into a click.
        /// </summary>
        /// <param name="dt">Seconds since the last frame.</param>
        /// <param name="zombiesAlive">How many are still on their feet.</param>
        public string Tick(float dt, int zombiesAlive)
        {
            if (zombiesAlive <= 0)
            {
                // An empty corridor is silent, and its clock does not run either — otherwise the
                // next wave would walk in with a moan already due and fire it on arrival.
                armed = false;
                return null;
            }

            if (!armed)
            {
                // Armed on the first frame the corridor has anyone in it, off the short opening
                // window rather than the steady one: every wave announces itself, instead of
                // walking in and standing there in silence for the length of a full gap.
                Draw(ZombieAmbience.OpeningMoanMin, ZombieAmbience.OpeningMoanMax);
                armed = true;
                return null;
            }

            untilNextMoan -= dt;
            if (untilNextMoan > 0f) return null;

            Rearm(zombiesAlive);
            return rotation.Advance();
        }

        private void Rearm(int zombiesAlive)
        {
            (float Min, float Max) window = ZombieAmbience.WindowFor(zombiesAlive);
            Draw(window.Min, window.Max);
        }

        private void Draw(float min, float max)
        {
            untilNextMoan = min + ((float)rng.NextDouble() * (max - min));
        }
    }
}
