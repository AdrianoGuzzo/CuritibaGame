using System;
using System.Linq;
using Curitiba.Core.Audio;
using Curitiba.Core.BeatEmUp;
using Xunit;

namespace Curitiba.Tests.Audio
{
    /// <summary>
    /// Which landed blow is audible at all, and the sound bank it draws from.
    /// </summary>
    /// <remarks>
    /// The weight class travels as a <see cref="string"/> and is parsed inside: <c>AttackType</c> is
    /// internal, and an internal type may not appear in a public test method's signature.
    /// </remarks>
    public class CombatSoundsTests
    {
        private static AttackType Weight(string name) => (AttackType)Enum.Parse(typeof(AttackType), name);

        [Theory]
        [InlineData("Normal")]
        [InlineData("Heavy")]
        public void APunchLanding_ShouldBeAudible(string attackType)
        {
            // Arrange
            AttackType type = Weight(attackType);

            // Act
            bool audible = CombatSounds.IsPunch(type);

            // Assert
            Assert.True(audible);
        }

        [Theory]
        [InlineData("Finisher")]
        [InlineData("Air")]
        public void ABlowThatIsNotAPunch_ShouldStaySilent(string attackType)
        {
            // Arrange
            AttackType type = Weight(attackType);

            // Act
            bool audible = CombatSounds.IsPunch(type);

            // Assert — the kick and the jump kick are waiting for sounds of their own.
            Assert.False(audible);
        }

        [Fact]
        public void ThePunchBank_ShouldHoldSeveralVariantsToRotateThrough()
        {
            // Assert — one impact replayed on every blow is what made the first pass sound like a
            // typewriter; the whole point of the bank is that there is more than one of them.
            Assert.True(CombatSounds.PunchHits.Count > 1,
                "a single variant cannot be rotated, and is what the rotation exists to avoid");
        }

        [Fact]
        public void ThePunchBank_ShouldNotRepeatAnAsset()
        {
            // Assert — five near-identical asset strings are exactly where a copy-paste slips
            // through, and a duplicate would silently make the rotation shorter than it looks.
            Assert.Equal(CombatSounds.PunchHits.Count, CombatSounds.PunchHits.Distinct().Count());
        }

        [Fact]
        public void ThePunchImpact_ShouldReadAboveTheMusicBed()
        {
            // Assert — the arena's music sits at 0.30 on purpose; an impact under it is inaudible.
            Assert.InRange(CombatSounds.PunchHitVolume, ArenaMusicPolicy.BackgroundVolume, 1f);
        }
    }
}
