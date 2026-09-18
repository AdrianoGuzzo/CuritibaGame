using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

namespace Curitiba.Core.Audio
{
    /// <summary>
    /// The real one-shot channel: short <see cref="SoundEffect"/>s played straight through the
    /// mixer, with no instance kept around.
    /// </summary>
    /// <remarks>
    /// Effects are loaded from the game's root <see cref="ContentManager"/>, not a screen's own one,
    /// for the same reason the music is: no screen unloading its content can dispose a buffer the
    /// mixer is still reading.
    /// <para>
    /// A missing effect is a normal condition, exactly like a missing sprite strip — the game runs,
    /// that impact is silent. The failure is <em>remembered</em>, though, which music never had to
    /// bother with: a track is asked for once per screen, but an impact is asked for several times
    /// a second, and retrying a load that cannot succeed would hammer the disk in mid-combat.
    /// </para>
    /// </remarks>
    internal sealed class SoundPlayer : ISoundPlayer
    {
        // A null value is a tombstone: "asked for, not there, do not ask again".
        private readonly Dictionary<string, SoundEffect> cache = new Dictionary<string, SoundEffect>();

        private readonly ContentManager content;

        public SoundPlayer(ContentManager content)
        {
            this.content = content;
        }

        public void Play(string assetName, float volume)
        {
            if (string.IsNullOrEmpty(assetName))
                return;

            if (!cache.TryGetValue(assetName, out SoundEffect effect))
            {
                effect = TryLoad(assetName);
                cache[assetName] = effect;
            }

            if (effect == null)
                return;

            try
            {
                effect.Play(MathHelper.Clamp(volume, 0f, 1f), 0f, 0f);
            }
            catch (InstancePlayLimitException)
            {
                // The mixer is full. In a brawl that is a crowded moment, not a fault: the blow
                // that found no free voice simply goes unheard.
            }
        }

        /// <summary>Loads an effect, or returns null if it is not there — never throwing at a caller
        /// who is in the middle of resolving a hit.</summary>
        private SoundEffect TryLoad(string assetName)
        {
            try
            {
                return content.Load<SoundEffect>(assetName);
            }
            catch (ContentLoadException)
            {
                return null;
            }
            catch (NoAudioHardwareException)
            {
                return null;
            }
        }
    }
}
