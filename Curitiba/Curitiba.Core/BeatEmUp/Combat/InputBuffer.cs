namespace Curitiba.Core.BeatEmUp.Combat
{
    /// <summary>
    /// A tiny per-command input buffer: instead of firing an attack only on the exact frame the
    /// button is pressed, the press is remembered for a short time-to-live so it still fires the
    /// moment the fighter is able to act. This is what makes mashing/chaining feel responsive — a
    /// press during the previous swing's recovery is not lost. A value type living as a field on
    /// <see cref="Fighter"/>, so it never allocates on the per-frame hot path. Pushing again just
    /// refreshes the timer (the latest intent wins); no queue is needed for a single attack button.
    /// </summary>
    internal struct InputBuffer
    {
        private float attackTtl;

        /// <summary>Remembers an attack press for <paramref name="ttl"/> seconds.</summary>
        public void PushAttack(float ttl) => attackTtl = ttl;

        /// <summary>True while a buffered attack press is still live.</summary>
        public readonly bool HasAttack => attackTtl > 0f;

        /// <summary>Clears the buffered attack (call exactly once when a swing actually starts).</summary>
        public void ConsumeAttack() => attackTtl = 0f;

        /// <summary>Ages the buffered press; it lapses once its time-to-live runs out.</summary>
        public void Tick(float dt)
        {
            if (attackTtl > 0f)
                attackTtl -= dt;
        }
    }
}
