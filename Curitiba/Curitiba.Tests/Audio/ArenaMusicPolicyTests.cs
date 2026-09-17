using Curitiba.Core.Audio;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.Audio
{
    /// <summary>
    /// The arena music lifecycle: fade in when the stage starts, sit behind the action, duck while
    /// the pause menu is up, and fade out as the screen hands over.
    /// </summary>
    /// <remarks>
    /// The rule that matters is the volume ceiling. This is the only music the beat 'em up has, and
    /// it is meant to stay under the fight rather than compete with it, so no state may ever put the
    /// track above <see cref="ArenaMusicPolicy.BackgroundVolume"/> — not the fade-in, not unpausing,
    /// and least of all the way out.
    /// </remarks>
    public class ArenaMusicPolicyTests
    {
        private const float FadeIn = 1.0f;
        private const float FadeOut = 0.5f;

        private const float Background = ArenaMusicPolicy.BackgroundVolume;
        private const float Ducked = ArenaMusicPolicy.DuckedVolume;

        /// <summary>
        /// A fresh policy plus its recorder, at the fade durations the beat 'em up screen really uses.
        /// </summary>
        private static (ArenaMusicPolicy Policy, RecordingMusicPlayer Player) Arena(
            float fadeInSeconds = FadeIn, float fadeOutSeconds = FadeOut) =>
            (new ArenaMusicPolicy(fadeInSeconds, fadeOutSeconds), new RecordingMusicPlayer());

        /// <summary>Runs <paramref name="frames"/> fixed 1/60 s frames through the policy.</summary>
        private static void Advance(ArenaMusicPolicy policy, RecordingMusicPlayer player, int frames,
            bool paused = false)
        {
            for (int i = 0; i < frames; i++)
                policy.Update(Frames.DefaultStep, player, paused);
        }

        /// <summary>Runs enough fixed frames to cover <paramref name="seconds"/>.</summary>
        private static void AdvanceSeconds(ArenaMusicPolicy policy, RecordingMusicPlayer player,
            float seconds, bool paused = false) =>
            Advance(policy, player, Frames.FramesFor(seconds), paused);

        /// <summary>
        /// Runs the stage until the fade-in is over and the track is settled behind the action.
        /// </summary>
        /// <remarks>
        /// Deliberately overshoots instead of counting frames exactly: the frame that starts the
        /// track does not advance the ramp, and accumulating 1/60 s in <c>float</c> sixty times lands
        /// a hair under a second. Which frame the ramp lands on is not a rule anything depends on —
        /// that it lands is.
        /// </remarks>
        private static void ReachBackgroundVolume(ArenaMusicPolicy policy, RecordingMusicPlayer player) =>
            AdvanceSeconds(policy, player, FadeIn * 2f);

        [Fact]
        public void AFreshPolicy_ShouldNotHaveTouchedThePlayer()
        {
            // Arrange
            var (policy, player) = Arena();

            // Act
            // (constructing the policy is the action)

            // Assert
            Assert.Empty(player.Operations);
            Assert.Equal(ArenaMusicState.Idle, policy.State);
        }

        [Fact]
        public void TheFirstUpdate_ShouldStartTheArenaTrackLooping()
        {
            // Arrange
            var (policy, player) = Arena();

            // Act
            policy.Update(Frames.DefaultStep, player, false);

            // Assert
            Assert.Equal(1, player.PlayCount);
            Assert.Equal(ArenaMusicPolicy.Track, player.LastAsset);
            Assert.True(player.LastLoop);
        }

        [Fact]
        public void TheFirstUpdate_ShouldSetTheStartVolumeBeforeStartingTheTrack()
        {
            // A track started before its volume is set is audible at the wrong level for a frame,
            // which right after the menu's fade-out would be a stab of full-volume music.
            // Arrange
            var (policy, player) = Arena();

            // Act
            policy.Update(Frames.DefaultStep, player, false);

            // Assert
            Assert.Equal("Volume", player.Operations[0]);
            Assert.Equal("Play", player.Operations[1]);
            Assert.Equal(0f, player.Volumes[0]);
        }

        [Fact]
        public void TheTrack_ShouldFadeInOverTheFadeInDuration()
        {
            // Arrange
            var (policy, player) = Arena();

            // Act
            AdvanceSeconds(policy, player, FadeIn / 2f);

            // Assert
            Assert.Equal(ArenaMusicState.FadingIn, policy.State);
            Assert.InRange(player.Volume, 0.01f, Background - 0.01f);
        }

        [Fact]
        public void TheTrack_ShouldSettleAtTheBackgroundVolume_NotFullVolume()
        {
            // The whole point of the track: one background music, kept under the fight.
            // Arrange
            var (policy, player) = Arena();

            // Act
            ReachBackgroundVolume(policy, player);

            // Assert
            Assert.Equal(ArenaMusicState.Playing, policy.State);
            Assert.Equal(Background, player.Volume);
        }

        [Fact]
        public void TheTrack_ShouldBeStartedOnlyOnce()
        {
            // Arrange
            var (policy, player) = Arena();

            // Act
            AdvanceSeconds(policy, player, 10f);

            // Assert
            Assert.Equal(1, player.PlayCount);
        }

        [Fact]
        public void PlayingAtTheBackgroundVolume_ShouldNotRewriteTheVolumeEveryFrame()
        {
            // Hot-path rule: a running stage must not push a value at the audio backend per frame.
            // Arrange
            var (policy, player) = Arena();
            ReachBackgroundVolume(policy, player);
            int writesOncePlaying = player.Volumes.Count;

            // Act
            Advance(policy, player, 60);

            // Assert
            Assert.Equal(writesOncePlaying, player.Volumes.Count);
        }

        [Fact]
        public void Pausing_ShouldDuckTheTrack()
        {
            // Arrange
            var (policy, player) = Arena();
            ReachBackgroundVolume(policy, player);

            // Act
            policy.Update(Frames.DefaultStep, player, true);

            // Assert
            Assert.Equal(ArenaMusicState.Ducked, policy.State);
            Assert.Equal(Ducked, player.Volume);
        }

        [Fact]
        public void Pausing_ShouldNotRestartTheTrack()
        {
            // Ducking is a volume change, not a new song: the stage resumes mid-track.
            // Arrange
            var (policy, player) = Arena();
            ReachBackgroundVolume(policy, player);

            // Act
            AdvanceSeconds(policy, player, 2f, paused: true);

            // Assert
            Assert.Equal(1, player.PlayCount);
            Assert.Equal(0, player.StopCount);
        }

        [Fact]
        public void StayingPaused_ShouldNotRewriteTheVolumeEveryFrame()
        {
            // Arrange
            var (policy, player) = Arena();
            ReachBackgroundVolume(policy, player);
            policy.Update(Frames.DefaultStep, player, true);
            int writesOnceDucked = player.Volumes.Count;

            // Act
            Advance(policy, player, 60, paused: true);

            // Assert
            Assert.Equal(writesOnceDucked, player.Volumes.Count);
        }

        [Fact]
        public void Unpausing_ShouldRestoreTheBackgroundVolume()
        {
            // Arrange
            var (policy, player) = Arena();
            ReachBackgroundVolume(policy, player);
            AdvanceSeconds(policy, player, 1f, paused: true);

            // Act
            policy.Update(Frames.DefaultStep, player, false);

            // Assert
            Assert.Equal(ArenaMusicState.Playing, policy.State);
            Assert.Equal(Background, player.Volume);
        }

        [Fact]
        public void BeginExit_ShouldStartFadingOutFromTheBackgroundVolume()
        {
            // Arrange
            var (policy, player) = Arena();
            ReachBackgroundVolume(policy, player);

            // Act
            policy.BeginExit();
            AdvanceSeconds(policy, player, FadeOut / 2f);

            // Assert
            Assert.Equal(ArenaMusicState.FadingOut, policy.State);
            Assert.InRange(player.Volume, 0.01f, Background - 0.01f);
        }

        [Fact]
        public void TheFadeOut_ShouldEndWithinTheScreenTransition()
        {
            // The fade shares the screen's transition off, so "Fim da Demo" arrives in silence.
            // Arrange
            var (policy, player) = Arena();
            ReachBackgroundVolume(policy, player);
            policy.BeginExit();

            // Act
            AdvanceSeconds(policy, player, FadeOut + Frames.DefaultStep);

            // Assert
            Assert.Equal(ArenaMusicState.Stopped, policy.State);
            Assert.Equal(0f, player.Volume);
            Assert.Equal(1, player.StopCount);
        }

        [Fact]
        public void TheFadeOut_ShouldStopTheTrackOnlyOnce()
        {
            // Arrange
            var (policy, player) = Arena();
            ReachBackgroundVolume(policy, player);
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
        public void BeginExitWhilePaused_ShouldFadeOutFromTheDuckedVolume()
        {
            // Quitting from the pause menu must not make the track louder on its way out.
            // Arrange
            var (policy, player) = Arena();
            ReachBackgroundVolume(policy, player);
            policy.Update(Frames.DefaultStep, player, true);

            // Act
            policy.BeginExit();
            AdvanceSeconds(policy, player, FadeOut / 2f);

            // Assert
            Assert.Equal(ArenaMusicState.FadingOut, policy.State);
            Assert.InRange(player.Volume, 0.001f, Ducked);
        }

        [Fact]
        public void PausingWhileFadingOut_ShouldNotInterruptTheFadeOut()
        {
            // The loading screen covers the arena exactly like the pause menu does, and the fade
            // has to finish anyway.
            // Arrange
            var (policy, player) = Arena();
            ReachBackgroundVolume(policy, player);
            policy.BeginExit();

            // Act
            AdvanceSeconds(policy, player, FadeOut + Frames.DefaultStep, paused: true);

            // Assert
            Assert.Equal(ArenaMusicState.Stopped, policy.State);
            Assert.Equal(0f, player.Volume);
            Assert.Equal(1, player.StopCount);
        }

        [Fact]
        public void BeginExitCalledTwice_ShouldNotRestartTheFadeOut()
        {
            // Arrange
            var (policy, player) = Arena();
            ReachBackgroundVolume(policy, player);
            policy.BeginExit();
            AdvanceSeconds(policy, player, FadeOut / 2f);

            // Act
            policy.BeginExit();
            AdvanceSeconds(policy, player, FadeOut / 2f + Frames.DefaultStep);

            // Assert
            Assert.Equal(ArenaMusicState.Stopped, policy.State);
            Assert.Equal(1, player.StopCount);
        }

        [Fact]
        public void BeginExitBeforeTheFirstUpdate_ShouldNeverStartTheTrack()
        {
            // A stage abandoned during its transition on never had a track to fade.
            // Arrange
            var (policy, player) = Arena();

            // Act
            policy.BeginExit();
            AdvanceSeconds(policy, player, 5f);

            // Assert
            Assert.Equal(ArenaMusicState.Stopped, policy.State);
            Assert.Equal(0, player.PlayCount);
            Assert.Equal(0, player.StopCount);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        public void ANonPositiveFadeIn_ShouldStartAtTheBackgroundVolume(float fadeInSeconds)
        {
            // Even with no ramp the ceiling holds: skipping the fade must not mean full volume.
            // Arrange
            var (policy, player) = Arena(fadeInSeconds: fadeInSeconds);

            // Act
            policy.Update(Frames.DefaultStep, player, false);

            // Assert
            Assert.Equal(ArenaMusicState.Playing, policy.State);
            Assert.Equal(Background, player.Volume);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-2.5f)]
        public void ANonPositiveFadeOut_ShouldStopOnTheNextFrame(float fadeOutSeconds)
        {
            // Arrange
            var (policy, player) = Arena(fadeOutSeconds: fadeOutSeconds);
            ReachBackgroundVolume(policy, player);
            policy.BeginExit();

            // Act
            policy.Update(Frames.DefaultStep, player, false);

            // Assert
            Assert.Equal(ArenaMusicState.Stopped, policy.State);
            Assert.Equal(0f, player.Volume);
            Assert.Equal(1, player.StopCount);
        }

        [Fact]
        public void AHugeDeltaAfterAFreeze_ShouldNotOvershootTheFadeIn()
        {
            // The dev editor freezing the scene on F1 hands the next frame a huge delta.
            // Arrange
            var (policy, player) = Arena();
            policy.Update(Frames.DefaultStep, player, false);

            // Act
            policy.Update(100f, player, false);

            // Assert
            Assert.Equal(ArenaMusicState.Playing, policy.State);
            Assert.Equal(Background, player.Volume);
        }

        [Fact]
        public void AHugeDeltaAfterAFreeze_ShouldFinishTheFadeOutCleanly()
        {
            // Arrange
            var (policy, player) = Arena();
            ReachBackgroundVolume(policy, player);
            policy.BeginExit();

            // Act
            policy.Update(100f, player, false);

            // Assert
            Assert.Equal(ArenaMusicState.Stopped, policy.State);
            Assert.Equal(0f, player.Volume);
            Assert.Equal(1, player.StopCount);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        public void ANonPositiveDelta_ShouldNotRewindTheFade(float elapsedSeconds)
        {
            // Arrange
            var (policy, player) = Arena();
            AdvanceSeconds(policy, player, FadeIn / 2f);
            float reached = player.Volume;

            // Act
            policy.Update(elapsedSeconds, player, false);

            // Assert
            Assert.Equal(reached, player.Volume);
        }

        [Fact]
        public void TheVolume_ShouldNeverExceedTheBackgroundVolume()
        {
            // Every state the stage can put the track through, in one run.
            // Arrange
            var (policy, player) = Arena();
            policy.Update(-5f, player, false);
            policy.Update(100f, player, false);
            AdvanceSeconds(policy, player, 1f, paused: true);
            AdvanceSeconds(policy, player, 1f);

            // Act
            policy.BeginExit();
            policy.Update(0f, player, false);
            AdvanceSeconds(policy, player, FadeOut + 1f);

            // Assert
            Assert.All(player.Volumes, v => Assert.InRange(v, 0f, Background));
        }

        [Fact]
        public void ANullPlayer_ShouldNotCrashTheArena()
        {
            // The service lookup hands back null on any platform where no player was registered.
            // Arrange
            var policy = new ArenaMusicPolicy(FadeIn, FadeOut);

            // Act
            var exception = Record.Exception(() =>
            {
                policy.Update(Frames.DefaultStep, null, false);
                policy.Update(Frames.DefaultStep, null, true);
                policy.BeginExit();
                policy.Update(Frames.DefaultStep, null, false);
                policy.Stop(null);
            });

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void Stop_ShouldSilenceTheTrackImmediately()
        {
            // Quitting to the menu from the pause screen leaves no fade to ride.
            // Arrange
            var (policy, player) = Arena();
            ReachBackgroundVolume(policy, player);

            // Act
            policy.Stop(player);

            // Assert
            Assert.Equal(ArenaMusicState.Stopped, policy.State);
            Assert.Equal(1, player.StopCount);
            Assert.Equal(0f, player.Volume);
        }

        [Fact]
        public void Stop_ShouldBeIdempotent()
        {
            // UnloadContent runs after a fade-out that already stopped the track.
            // Arrange
            var (policy, player) = Arena();
            ReachBackgroundVolume(policy, player);

            // Act
            policy.Stop(player);
            policy.Stop(player);

            // Assert
            Assert.Equal(1, player.StopCount);
        }

        [Fact]
        public void Stop_ShouldNotTouchThePlayer_IfTheTrackNeverStarted()
        {
            // Arrange
            var (policy, player) = Arena();

            // Act
            policy.Stop(player);

            // Assert
            Assert.Empty(player.Operations);
            Assert.Equal(ArenaMusicState.Stopped, policy.State);
        }

        [Fact]
        public void ARestartedArena_ShouldStartTheTrackAgain()
        {
            // Sofia being defeated builds a new BeatEmUpScreen, so a new policy meets the same music
            // player and the track has to start over from the top.
            // Arrange
            var (first, player) = Arena();
            ReachBackgroundVolume(first, player);
            first.BeginExit();
            AdvanceSeconds(first, player, FadeOut + Frames.DefaultStep);

            // Act
            var second = new ArenaMusicPolicy(FadeIn, FadeOut);
            second.Update(Frames.DefaultStep, player, false);

            // Assert
            Assert.Equal(2, player.PlayCount);
            Assert.Equal(ArenaMusicState.FadingIn, second.State);
        }
    }
}
