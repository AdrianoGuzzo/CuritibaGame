using System.Collections.Generic;
using Curitiba.Core.Audio;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// An <see cref="IMusicPlayer"/> that records what it was asked to do instead of doing it.
    /// </summary>
    /// <remarks>
    /// This is what makes the menu music rules testable with no audio device: <c>MenuMusicPolicy</c>
    /// decides when to start, how loud, and when to stop, and never reads anything back — so the
    /// recorded operations are the whole observable output of a menu session.
    /// <para>
    /// <see cref="Operations"/> exists so ordering ("volume before play") can be asserted without
    /// formatting floats, which is exactly the kind of culture-sensitive detail
    /// <c>CultureScope</c> had to be written for.
    /// </para>
    /// </remarks>
    internal sealed class RecordingMusicPlayer : IMusicPlayer
    {
        private readonly List<string> operations = new List<string>();
        private readonly List<float> volumes = new List<float>();
        private float volume = 1f;

        /// <summary>The operation names in order: "Volume", "Play" or "Stop".</summary>
        public IReadOnlyList<string> Operations => operations;

        /// <summary>Every volume the policy asked for, in order.</summary>
        public IReadOnlyList<float> Volumes => volumes;

        /// <summary>How many times a track was started.</summary>
        public int PlayCount { get; private set; }

        /// <summary>How many times playback was stopped.</summary>
        public int StopCount { get; private set; }

        /// <summary>The asset name of the last <see cref="Play"/>, or null.</summary>
        public string LastAsset { get; private set; }

        /// <summary>Whether the last <see cref="Play"/> asked for looping.</summary>
        public bool LastLoop { get; private set; }

        public float Volume
        {
            get => volume;
            set
            {
                volume = value;
                volumes.Add(value);
                operations.Add("Volume");
            }
        }

        public void Play(string assetName, bool loop)
        {
            LastAsset = assetName;
            LastLoop = loop;
            PlayCount++;
            operations.Add("Play");
        }

        public void Stop()
        {
            StopCount++;
            operations.Add("Stop");
        }
    }
}
