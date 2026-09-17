using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace Curitiba.Core.Audio
{
    /// <summary>
    /// The real music channel: one streaming <see cref="Song"/> played through
    /// <see cref="MediaPlayer"/>.
    /// </summary>
    /// <remarks>
    /// The song is loaded from the game's root <see cref="ContentManager"/>, not from a screen's own
    /// one, so no screen unloading its content can dispose a track that is still playing — and coming
    /// back to the menu hits the content cache instead of decoding again.
    /// <para>
    /// Music that fails to load is treated exactly like a missing sprite strip: the game runs, in
    /// silence. That is also what keeps this class usable in headless tests, which never get past the
    /// load.
    /// </para>
    /// </remarks>
    internal sealed class MusicPlayer : IMusicPlayer
    {
        private readonly ContentManager content;

        private Song song;
        private string loadedAsset;
        private float volume = 1f;
        private bool isPlaying;

        public MusicPlayer(ContentManager content)
        {
            this.content = content;
        }

        /// <summary>Whether a track was successfully started and not yet stopped.</summary>
        public bool IsPlaying => isPlaying;

        public float Volume
        {
            get => volume;
            set
            {
                volume = MathHelper.Clamp(value, 0f, 1f);

                if (song != null)
                    MediaPlayer.Volume = volume;
            }
        }

        public void Play(string assetName, bool loop)
        {
            if (string.IsNullOrEmpty(assetName))
                return;

            if (song == null || NeedsReload(loadedAsset, assetName))
            {
                try
                {
                    song = content.Load<Song>(assetName);
                    loadedAsset = assetName;
                }
                catch (ContentLoadException)
                {
                    Forget();
                    return;
                }
                catch (NoAudioHardwareException)
                {
                    Forget();
                    return;
                }
            }

            MediaPlayer.IsRepeating = loop;
            MediaPlayer.Play(song);

            // After Play, so the volume lands on the song that is actually running.
            MediaPlayer.Volume = volume;

            isPlaying = true;
        }

        public void Stop()
        {
            if (song == null)
            {
                isPlaying = false;
                return;
            }

            MediaPlayer.Stop();
            isPlaying = false;
        }

        /// <summary>True when <paramref name="requestedAsset"/> is not the track already loaded.</summary>
        /// <remarks>
        /// Pulled out as a pure rule because the load itself needs an audio device and so is out of
        /// reach of the test suite. It is the whole reason the arena does not replay the menu theme:
        /// one <see cref="MusicPlayer"/> is shared by every screen, so the cached song has to be
        /// keyed by the name that asked for it.
        /// </remarks>
        internal static bool NeedsReload(string loadedAsset, string requestedAsset) =>
            !string.Equals(loadedAsset, requestedAsset, StringComparison.Ordinal);

        /// <summary>
        /// Drops the cached track after a failed load, so a missing asset plays silence rather than
        /// whatever happened to be loaded before it.
        /// </summary>
        private void Forget()
        {
            song = null;
            loadedAsset = null;
        }
    }
}
