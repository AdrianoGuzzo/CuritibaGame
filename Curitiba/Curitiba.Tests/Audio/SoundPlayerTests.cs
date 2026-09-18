using Curitiba.Core.Audio;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.Audio
{
    /// <summary>
    /// The one-shot sound channel, on the paths that never reach a real audio device.
    /// </summary>
    /// <remarks>
    /// Same boundary as <c>MusicPlayerTests</c>: with no compiled content every load fails, so what
    /// is exercised here is the robustness the game depends on — a missing effect is silence, never
    /// an exception — plus the one rule that only matters because this runs in a hot path: a load
    /// that failed is not tried again. Actually hearing the impact is a manual check, see the
    /// checklist in the test README.
    /// </remarks>
    public class SoundPlayerTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AnUnnamedEffect_ShouldNotEvenBeLookedUp(string assetName)
        {
            // Arrange
            var content = new CountingContentManager();
            var player = new SoundPlayer(content);

            // Act
            player.Play(assetName, 1f);

            // Assert
            Assert.Empty(content.Loads);
        }

        [Fact]
        public void AMissingEffect_ShouldPlaySilenceRatherThanThrow()
        {
            // Arrange
            var player = new SoundPlayer(HeadlessContent.Create());

            // Act
            var exception = Record.Exception(() => player.Play(CombatSounds.PunchHits[0], 1f));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void AnEffectThatFailedToLoad_ShouldNotBeLoadedAgain()
        {
            // Arrange
            var content = new CountingContentManager();
            var player = new SoundPlayer(content);

            // Act — a combo's worth of punches against an effect that is not there.
            for (int i = 0; i < 10; i++)
                player.Play(CombatSounds.PunchHits[0], 1f);

            // Assert — the failure is remembered, so the disk is hit once, not once per blow.
            Assert.Single(content.Loads);
            Assert.Equal(CombatSounds.PunchHits[0], content.Loads[0]);
        }
    }
}
