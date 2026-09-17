using Curitiba.Core.Audio;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.Audio
{
    /// <summary>
    /// The music channel's guarantees that hold with no audio device and no compiled content.
    /// </summary>
    /// <remarks>
    /// Only the paths that never reach <c>MediaPlayer</c> are exercised here, and that is the point:
    /// a track that failed to load must leave the game running silently rather than throwing, exactly
    /// as a missing sprite strip leaves a fighter on its placeholder. Actually hearing the track is a
    /// manual check — see the checklist in the test README.
    /// </remarks>
    public class MusicPlayerTests
    {
        [Fact]
        public void AMissingTrack_ShouldBeASilentNoOp()
        {
            // Arrange
            var player = new MusicPlayer(HeadlessContent.Create());

            // Act
            var exception = Record.Exception(() => player.Play("Music/NoSuchSong", true));

            // Assert
            Assert.Null(exception);
            Assert.False(player.IsPlaying);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AnUnnamedTrack_ShouldBeASilentNoOp(string assetName)
        {
            // Arrange
            var player = new MusicPlayer(HeadlessContent.Create());

            // Act
            var exception = Record.Exception(() => player.Play(assetName, true));

            // Assert
            Assert.Null(exception);
            Assert.False(player.IsPlaying);
        }

        [Fact]
        public void StoppingATrackThatNeverStarted_ShouldBeASilentNoOp()
        {
            // MainMenuScreen.UnloadContent stops the music whether or not it ever started.
            // Arrange
            var player = new MusicPlayer(HeadlessContent.Create());

            // Act
            var exception = Record.Exception(() => player.Stop());

            // Assert
            Assert.Null(exception);
            Assert.False(player.IsPlaying);
        }

        [Fact]
        public void AskingForATrackWhenNoneIsLoaded_ShouldLoadIt()
        {
            // Arrange
            // (nothing has been loaded yet)

            // Act
            bool needsReload = MusicPlayer.NeedsReload(null, "Music/A");

            // Assert
            Assert.True(needsReload);
        }

        [Fact]
        public void AskingForTheTrackAlreadyLoaded_ShouldNotReloadIt()
        {
            // The menu re-entered from "Fim da Demo" asks for the track it already has.
            // Arrange
            const string loaded = "Music/A";

            // Act
            bool needsReload = MusicPlayer.NeedsReload(loaded, "Music/A");

            // Assert
            Assert.False(needsReload);
        }

        [Fact]
        public void AskingForADifferentTrack_ShouldReloadTheSong()
        {
            // The rule the arena rests on: one shared player, two tracks. Without it the arena
            // would replay whatever the menu loaded first.
            // Arrange
            const string loaded = "Music/A";

            // Act
            bool needsReload = MusicPlayer.NeedsReload(loaded, "Music/B");

            // Assert
            Assert.True(needsReload);
        }

        [Theory]
        [InlineData(-1f, 0f)]
        [InlineData(2f, 1f)]
        [InlineData(0.5f, 0.5f)]
        public void TheVolume_ShouldBeClampedToZeroToOne(float requested, float expected)
        {
            // Arrange
            var player = new MusicPlayer(HeadlessContent.Create());

            // Act
            player.Volume = requested;

            // Assert
            Assert.Equal(expected, player.Volume);
        }
    }
}
