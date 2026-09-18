namespace Curitiba.Core.Audio
{
    /// <summary>
    /// The game's one-shot sound channel: short effects fired by something that just happened.
    /// </summary>
    /// <remarks>
    /// The sibling of <see cref="IMusicPlayer"/>, and tiny for the same reason — it keeps the rules
    /// that decide <em>when</em> a sound fires (<see cref="CombatSounds"/>, and the arena's hit
    /// loop) free of <c>SoundEffect</c>, which cannot be touched without an audio device and so
    /// would drag them out of reach of the headless test suite.
    /// <para>
    /// Deliberately fire-and-forget: there is no handle to stop or fade an impact. Anything that
    /// needs to be steered over time is music, and belongs on the other interface.
    /// </para>
    /// </remarks>
    internal interface ISoundPlayer
    {
        /// <summary>
        /// Fires <paramref name="assetName"/> once at <paramref name="volume"/> (0 to 1, clamped).
        /// A missing or unnamed effect is silence, never an exception.
        /// </summary>
        void Play(string assetName, float volume);
    }
}
