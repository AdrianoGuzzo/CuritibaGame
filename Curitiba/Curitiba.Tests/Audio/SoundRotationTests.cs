using System.Linq;
using Curitiba.Core.Audio;
using Xunit;

namespace Curitiba.Tests.Audio
{
    /// <summary>
    /// The rule that keeps a combo from sounding like one sample on repeat: each punch takes the
    /// next impact in the bank, wrapping round at the end.
    /// </summary>
    /// <remarks>
    /// Driven with a hand-written bank rather than the game's, so "it wraps" can be proved with two
    /// entries instead of five, and so renaming an asset never breaks the rule's own tests.
    /// </remarks>
    public class SoundRotationTests
    {
        private static readonly string[] Bank = { "one", "two", "three" };

        [Fact]
        public void AFreshRotation_ShouldStartAtTheFirstVariant()
        {
            // Arrange
            var rotation = new SoundRotation(Bank);

            // Act
            string first = rotation.Advance();

            // Assert
            Assert.Equal("one", first);
        }

        [Fact]
        public void EachPunch_ShouldTakeTheNextVariant()
        {
            // Arrange
            var rotation = new SoundRotation(Bank);

            // Act
            string[] heard = { rotation.Advance(), rotation.Advance(), rotation.Advance() };

            // Assert
            Assert.Equal(Bank, heard);
        }

        [Fact]
        public void TheRotation_ShouldWrapAfterTheLastVariant()
        {
            // Arrange
            var rotation = new SoundRotation(Bank);
            for (int i = 0; i < Bank.Length; i++)
                rotation.Advance();

            // Act
            string afterTheWrap = rotation.Advance();

            // Assert
            Assert.Equal("one", afterTheWrap);
        }

        [Fact]
        public void ALongCombo_ShouldNeverSoundTheSameVariantTwiceRunning()
        {
            // Arrange — far more blows than the bank holds, which is the case that went wrong.
            var rotation = new SoundRotation(Bank);

            // Act
            string[] heard = Enumerable.Range(0, 30).Select(_ => rotation.Advance()).ToArray();

            // Assert
            for (int i = 1; i < heard.Length; i++)
                Assert.NotEqual(heard[i - 1], heard[i]);
        }

        [Fact]
        public void ARotationWithNoAuthoredBank_ShouldCycleTheGameSounds()
        {
            // Arrange — this is how the arena builds one.
            var rotation = new SoundRotation();

            // Act — exactly one full cycle.
            string[] heard = Enumerable.Range(0, CombatSounds.PunchHits.Count)
                                       .Select(_ => rotation.Advance())
                                       .ToArray();

            // Assert — a full cycle spends the whole bank, so no variant is starved.
            Assert.Equal(CombatSounds.PunchHits.OrderBy(a => a), heard.OrderBy(a => a));
        }
    }
}
