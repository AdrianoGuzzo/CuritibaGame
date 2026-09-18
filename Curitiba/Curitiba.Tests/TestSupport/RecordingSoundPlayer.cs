using System.Collections.Generic;
using Curitiba.Core.Audio;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// An <see cref="ISoundPlayer"/> that records what it was asked to fire instead of firing it.
    /// </summary>
    /// <remarks>
    /// The sibling of <see cref="RecordingMusicPlayer"/>, and simpler for the same reason the
    /// interface is: a one-shot has no state to read back, so the list of effects asked for — and
    /// crucially <em>how many times</em> — is the whole observable output of a fight.
    /// </remarks>
    internal sealed class RecordingSoundPlayer : ISoundPlayer
    {
        private readonly List<string> assets = new List<string>();
        private readonly List<float> volumes = new List<float>();

        /// <summary>The asset name of every effect fired, in order, including repeats.</summary>
        public IReadOnlyList<string> Assets => assets;

        /// <summary>The volume each effect was fired at, in order.</summary>
        public IReadOnlyList<float> Volumes => volumes;

        /// <summary>How many effects were fired.</summary>
        public int PlayCount => assets.Count;

        public void Play(string assetName, float volume)
        {
            assets.Add(assetName);
            volumes.Add(volume);
        }

        /// <summary>Forgets everything fired so far, to assert on one swing in isolation.</summary>
        public void Clear()
        {
            assets.Clear();
            volumes.Clear();
        }
    }
}
