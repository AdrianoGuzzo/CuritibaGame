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

            if (song == null)
            {
                try
                {
                    song = content.Load<Song>(assetName);
                }
                catch (ContentLoadException)
                {
                    return;
                }
                catch (NoAudioHardwareException)
                {
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
    }
}
