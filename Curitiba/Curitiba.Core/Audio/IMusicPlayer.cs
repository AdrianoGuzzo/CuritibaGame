namespace Curitiba.Core.Audio
{
    /// <summary>
    /// The game's single music channel.
    /// </summary>
    /// <remarks>
    /// Deliberately tiny: this is the seam that keeps <see cref="MenuMusicPolicy"/> free of
    /// MonoGame's Media types, which cannot be touched without an audio device and so would drag the
    /// whole menu music rule out of reach of the headless test suite.
    /// </remarks>
    internal interface IMusicPlayer
    {
        /// <summary>Playback volume, 0 (silent) to 1 (full). Values outside the range are clamped.</summary>
        float Volume { get; set; }

        /// <summary>
        /// Starts <paramref name="assetName"/>, replacing whatever was playing. A missing or unnamed
        /// asset is a silent no-op, never an exception.
        /// </summary>
        void Play(string assetName, bool loop);

        /// <summary>Stops playback. Safe to call when nothing is playing.</summary>
        void Stop();
    }
}
