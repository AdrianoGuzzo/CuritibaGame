using Curitiba.Core.Audio;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.Audio
{
    /// <summary>
    /// The menu music lifecycle: fade in when the menu appears, hold while the player browses, fade
    /// out over the "Play" cinematic, and go silent before the arena loads.
    /// </summary>
    /// <remarks>
    /// The rule that matters is that the arena is silent: the track must be stopped on the very
    /// frame the cinematic ends, because that is the frame <c>MainMenuScreen</c> hands over to
    /// <c>LoadingScreen</c>. Everything else here protects that, or protects the player's ears from
    /// a track that punches in at full volume.
    /// </remarks>
    public class MenuMusicPolicyTests
    {
        private const float FadeIn = 1.0f;
        private const float FadeOut = 2.5f;

        /// <summary>
        /// A fresh policy plus its recorder, at the fade durations the menu really uses.
        /// </summary>
        private static (MenuMusicPolicy Policy, RecordingMusicPlayer Player) Menu(
            float fadeInSeconds = FadeIn, float fadeOutSeconds = FadeOut) =>
            (new MenuMusicPolicy(fadeInSeconds, fadeOutSeconds), new RecordingMusicPlayer());

        /// <summary>Runs <paramref name="frames"/> fixed 1/60 s frames through the policy.</summary>
        private static void Advance(MenuMusicPolicy policy, RecordingMusicPlayer player, int frames)
        {
            for (int i = 0; i < frames; i++)
                policy.Update(Frames.DefaultStep, player);
        }

        /// <summary>Runs enough fixed frames to cover <paramref name="seconds"/>.</summary>
        private static void AdvanceSeconds(MenuMusicPolicy policy, RecordingMusicPlayer player, float seconds) =>
            Advance(policy, player, Frames.FramesFor(seconds));

        /// <summary>
        /// Runs the menu until the fade-in is over and the track is settled at full volume.
        /// </summary>
        /// <remarks>
        /// Deliberately overshoots instead of counting frames exactly: the frame that starts the
        /// track does not advance the ramp, and accumulating 1/60 s in <c>float</c> sixty times lands
        /// a hair under a second. Which frame the ramp lands on is not a rule anything depends on —
        /// that it lands is.
        /// </remarks>
        private static void ReachFullVolume(MenuMusicPolicy policy, RecordingMusicPlayer player) =>
            AdvanceSeconds(policy, player, FadeIn * 2f);

        [Fact]
        public void AFreshPolicy_ShouldNotHaveTouchedThePlayer()
        {
            // Arrange
            var (policy, player) = Menu();

            // Act
            // (constructing the policy is the action)

            // Assert
            Assert.Empty(player.Operations);
            Assert.Equal(MenuMusicState.Idle, policy.State);
        }

        [Fact]
        public void TheFirstUpdate_ShouldStartTheMenuTrackLooping()
        {
            // Arrange
            var (policy, player) = Menu();

            // Act
            policy.Update(Frames.DefaultStep, player);

            // Assert
            Assert.Equal(1, player.PlayCount);
            Assert.Equal(MenuMusicPolicy.Track, player.LastAsset);
            Assert.True(player.LastLoop);
        }

        [Fact]
        public void TheFirstUpdate_ShouldSetTheStartVolumeBeforeStartingTheTrack()
        {
            // A track started before its volume is set is audible at the wrong level for a frame,
            // which is exactly what re-entering the menu after a fade-out would sound like.
            // Arrange
            var (policy, player) = Menu();

            // Act
            policy.Update(Frames.DefaultStep, player);

            // Assert
            Assert.Equal("Volume", player.Operations[0]);
            Assert.Equal("Play", player.Operations[1]);
            Assert.Equal(0f, player.Volumes[0]);
        }

        [Fact]
        public void TheTrack_ShouldFadeInOverTheFadeInDuration()
        {
            // Arrange
            var (policy, player) = Menu();

            // Act
            AdvanceSeconds(policy, player, FadeIn / 2f);

            // Assert
            Assert.Equal(MenuMusicState.FadingIn, policy.State);
            Assert.InRange(player.Volume, 0.01f, 0.99f);
        }

        [Fact]
        public void TheTrack_ShouldReachFullVolumeWhenTheFadeInEnds()
        {
            // Arrange
            var (policy, player) = Menu();

            // Act
            ReachFullVolume(policy, player);

            // Assert
            Assert.Equal(MenuMusicState.Playing, policy.State);
            Assert.Equal(1f, player.Volume);
        }

        [Fact]
        public void TheTrack_ShouldBeStartedOnlyOnce()
        {
            // Arrange
            var (policy, player) = Menu();

            // Act
            AdvanceSeconds(policy, player, 10f);

            // Assert
            Assert.Equal(1, player.PlayCount);
        }

        [Fact]
        public void PlayingAtFullVolume_ShouldNotRewriteTheVolumeEveryFrame()
        {
            // Hot-path rule: browsing the menu must not push a value at the audio backend per frame.
            // Arrange
            var (policy, player) = Menu();
            ReachFullVolume(policy, player);
            int writesOncePlaying = player.Volumes.Count;

            // Act
            Advance(policy, player, 60);

            // Assert
            Assert.Equal(writesOncePlaying, player.Volumes.Count);
        }

        [Fact]
        public void BeginExit_ShouldStartFadingOutFromFullVolume()
        {
            // Arrange
            var (policy, player) = Menu();
            ReachFullVolume(policy, player);

            // Act
            policy.BeginExit();
            AdvanceSeconds(policy, player, FadeOut / 2f);

            // Assert
            Assert.Equal(MenuMusicState.FadingOut, policy.State);
            Assert.InRange(player.Volume, 0.01f, 0.99f);
        }

        [Fact]
        public void TheFadeOut_ShouldEndExactlyWhenTheCinematicDoes()
        {
            // The frame the cinematic ends is the frame MainMenuScreen hands over to LoadingScreen,
            // so this is the assertion that keeps the arena silent.
            // Arrange
            var (policy, player) = Menu();
            ReachFullVolume(policy, player);
            policy.BeginExit();

            // Act
            AdvanceSeconds(policy, player, FadeOut + Frames.DefaultStep);

            // Assert
            Assert.Equal(MenuMusicState.Stopped, policy.State);
            Assert.Equal(0f, player.Volume);
            Assert.Equal(1, player.StopCount);
        }

        [Fact]
        public void TheFadeOut_ShouldStopTheTrackOnlyOnce()
        {
            // Arrange
            var (policy, player) = Menu();
            ReachFullVolume(policy, player);
            policy.BeginExit();
            AdvanceSeconds(policy, player, FadeOut + Frames.DefaultStep);
            int writesOnceStopped = player.Volumes.Count;

            // Act
            AdvanceSeconds(policy, player, 5f);

            // Assert
            Assert.Equal(1, player.StopCount);
            Assert.Equal(writesOnceStopped, player.Volumes.Count);
        }

        [Fact]
        public void BeginExitCalledTwice_ShouldNotRestartTheFadeOut()
        {
            // Arrange
            var (policy, player) = Menu();
            ReachFullVolume(policy, player);
            policy.BeginExit();
            AdvanceSeconds(policy, player, FadeOut / 2f);

            // Act
            policy.BeginExit();
            AdvanceSeconds(policy, player, FadeOut / 2f + Frames.DefaultStep);

            // Assert
            Assert.Equal(MenuMusicState.Stopped, policy.State);
            Assert.Equal(1, player.StopCount);
        }

        [Fact]
        public void BeginExitBeforeTheFirstUpdate_ShouldNeverStartTheTrack()
        {
            // Arrange
            var (policy, player) = Menu();

            // Act
            policy.BeginExit();
            AdvanceSeconds(policy, player, 5f);

            // Assert
            Assert.Equal(MenuMusicState.Stopped, policy.State);
            Assert.Equal(0, player.PlayCount);
            Assert.Equal(0, player.StopCount);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        public void ANonPositiveFadeIn_ShouldStartAtFullVolume(float fadeInSeconds)
        {
            // Arrange
            var (policy, player) = Menu(fadeInSeconds: fadeInSeconds);

            // Act
            policy.Update(Frames.DefaultStep, player);

            // Assert
            Assert.Equal(MenuMusicState.Playing, policy.State);
            Assert.Equal(1f, player.Volume);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-2.5f)]
        public void ANonPositiveFadeOut_ShouldStopOnTheNextFrame(float fadeOutSeconds)
        {
            // Arrange
            var (policy, player) = Menu(fadeOutSeconds: fadeOutSeconds);
            ReachFullVolume(policy, player);
            policy.BeginExit();

            // Act
            policy.Update(Frames.DefaultStep, player);

            // Assert
            Assert.Equal(MenuMusicState.Stopped, policy.State);
            Assert.Equal(0f, player.Volume);
            Assert.Equal(1, player.StopCount);
        }

        [Fact]
        public void AHugeDeltaAfterAFreeze_ShouldNotOvershootTheFadeIn()
        {
            // A breakpoint or a dragged window hands the next frame a huge delta.
            // Arrange
            var (policy, player) = Menu();
            policy.Update(Frames.DefaultStep, player);

            // Act
            policy.Update(100f, player);

            // Assert
            Assert.Equal(MenuMusicState.Playing, policy.State);
            Assert.Equal(1f, player.Volume);
        }

        [Fact]
        public void AHugeDeltaAfterAFreeze_ShouldFinishTheFadeOutCleanly()
        {
            // Arrange
            var (policy, player) = Menu();
            ReachFullVolume(policy, player);
            policy.BeginExit();

            // Act
            policy.Update(100f, player);

            // Assert
            Assert.Equal(MenuMusicState.Stopped, policy.State);
            Assert.Equal(0f, player.Volume);
            Assert.Equal(1, player.StopCount);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        public void ANonPositiveDelta_ShouldNotRewindTheFade(float elapsedSeconds)
        {
            // Arrange
            var (policy, player) = Menu();
            AdvanceSeconds(policy, player, FadeIn / 2f);
            float reached = player.Volume;

            // Act
            policy.Update(elapsedSeconds, player);

            // Assert
            Assert.Equal(reached, player.Volume);
        }

        [Fact]
        public void TheVolume_ShouldNeverLeaveTheZeroToOneRange()
        {
            // Arrange
            var (policy, player) = Menu();
            policy.Update(-5f, player);
            policy.Update(100f, player);
            AdvanceSeconds(policy, player, FadeIn);

            // Act
            policy.BeginExit();
            policy.Update(0f, player);
            AdvanceSeconds(policy, player, FadeOut + 1f);

            // Assert
            Assert.All(player.Volumes, v => Assert.InRange(v, 0f, 1f));
        }

        [Fact]
        public void ANullPlayer_ShouldNotCrashTheMenu()
        {
            // The service lookup hands back null on any platform where no player was registered.
            // Arrange
            var policy = new MenuMusicPolicy(FadeIn, FadeOut);

            // Act
            var exception = Record.Exception(() =>
            {
                policy.Update(Frames.DefaultStep, null);
                policy.BeginExit();
                policy.Update(Frames.DefaultStep, null);
                policy.Stop(null);
            });

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void Stop_ShouldSilenceTheTrackImmediately()
        {
            // Arrange
            var (policy, player) = Menu();
            ReachFullVolume(policy, player);

            // Act
            policy.Stop(player);

            // Assert
            Assert.Equal(MenuMusicState.Stopped, policy.State);
            Assert.Equal(1, player.StopCount);
            Assert.Equal(0f, player.Volume);
        }

        [Fact]
        public void Stop_ShouldBeIdempotent()
        {
            // Arrange
            var (policy, player) = Menu();
            ReachFullVolume(policy, player);

            // Act
            policy.Stop(player);
            policy.Stop(player);

            // Assert
            Assert.Equal(1, player.StopCount);
        }

        [Fact]
        public void Stop_ShouldNotTouchThePlayer_IfTheTrackNeverStarted()
        {
            // UnloadContent runs even when Update never did: a menu exited during its transition on.
            // Arrange
            var (policy, player) = Menu();

            // Act
            policy.Stop(player);

            // Assert
            Assert.Empty(player.Operations);
            Assert.Equal(MenuMusicState.Stopped, policy.State);
        }

        [Fact]
        public void AReenteredMenu_ShouldStartTheTrackAgain()
        {
            // Returning from the demo builds a new MainMenuScreen, so a new policy meets the same
            // music player. Nothing may be left over from the previous session.
            // Arrange
            var (first, player) = Menu();
            ReachFullVolume(first, player);
            first.BeginExit();
            AdvanceSeconds(first, player, FadeOut + Frames.DefaultStep);

            // Act
            var second = new MenuMusicPolicy(FadeIn, FadeOut);
            second.Update(Frames.DefaultStep, player);

            // Assert
            Assert.Equal(2, player.PlayCount);
            Assert.Equal(MenuMusicState.FadingIn, second.State);
        }
    }
}
