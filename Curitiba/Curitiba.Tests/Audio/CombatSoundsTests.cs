using System;
using System.Linq;
using Curitiba.Core.Audio;
using Curitiba.Core.BeatEmUp;
using Xunit;

namespace Curitiba.Tests.Audio
{
    /// <summary>
    /// Which blow is audible, when it is audible, the sound bank it draws from, and the voice a
    /// body lets out on the floor.
    /// </summary>
    /// <remarks>
    /// The weight class travels as a <see cref="string"/> and is parsed inside: <c>AttackType</c> is
    /// internal, and an internal type may not appear in a public test method's signature.
    /// </remarks>
    public class CombatSoundsTests
    {
        private static AttackType Weight(string name) => (AttackType)Enum.Parse(typeof(AttackType), name);

        // ---------------------------------------------------------------- landing

        [Theory]
        [InlineData("Normal")]
        [InlineData("Heavy")]
        [InlineData("Finisher")]
        public void ALandedBlow_ShouldSoundTheImpact(string attackType)
        {
            // Arrange
            AttackType type = Weight(attackType);

            // Act
            bool audible = CombatSounds.HasImpactSound(type);

            // Assert — flesh is flesh: the kick borrows the punch bank for the moment of contact,
            // and gets its identity from the swing that opens it instead.
            Assert.True(audible);
        }

        [Fact]
        public void ALandedAirKick_ShouldStaySilent()
        {
            // Act
            bool audible = CombatSounds.HasImpactSound(Weight("Air"));

            // Assert — the jump kick is still waiting for a sound of its own.
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

        // ---------------------------------------------------------------- swinging

        [Fact]
        public void TheKick_ShouldSoundItsSwing()
        {
            // Act
            string asset = CombatSounds.SwingSoundFor(Weight("Finisher"));

            // Assert — the whoosh is what makes the finisher read as a kick rather than as one
            // more punch, and it is the only part of a blow that sounds before it lands.
            Assert.False(string.IsNullOrEmpty(asset));
        }

        [Theory]
        [InlineData("Normal")]
        [InlineData("Heavy")]
        [InlineData("Air")]
        public void ABlowThatIsNotTheKick_ShouldSwingSilently(string attackType)
        {
            // Act
            string asset = CombatSounds.SwingSoundFor(Weight(attackType));

            // Assert — a whoosh on every jab would fire several times a second and turn the
            // string into noise; only the blow that closes it announces itself.
            Assert.Null(asset);
        }

        [Fact]
        public void TheKickSwing_ShouldNotBeOneOfTheImpacts()
        {
            // Assert — swing and contact are two different moments of the same kick, and playing
            // an impact as the windup would give away the hit before it happened.
            Assert.DoesNotContain(CombatSounds.SwingSoundFor(Weight("Finisher")), CombatSounds.PunchHits);
        }

        [Fact]
        public void TheKickSwing_ShouldReadOverTheMusicAndUnderTheImpact()
        {
            // Assert — audible over the 0.30 music bed, but never louder than the contact it
            // announces: the windup is the anticipation, the impact is the payoff.
            Assert.InRange(CombatSounds.KickSwingVolume,
                ArenaMusicPolicy.BackgroundVolume, CombatSounds.PunchHitVolume);
        }

        // ---------------------------------------------------------------- hitting the floor

        [Fact]
        public void TheFallGrunt_ShouldReadOverTheMusicAndNoLouderThanTheBlow()
        {
            // Assert — the punch and the body hitting the floor are one event to the ear, so the
            // grunt sits level with the impact that caused it, and well over the 0.30 music bed.
            Assert.InRange(CombatSounds.EnemyFallVolume,
                ArenaMusicPolicy.BackgroundVolume, CombatSounds.PunchHitVolume);
        }

        [Fact]
        public void TheFallGrunt_ShouldNotBeOneOfTheBlows()
        {
            // Assert — it is a voice, not a blow: the body is what makes it, and it fires on the
            // frame the body lands rather than on the frame something is struck.
            Assert.DoesNotContain(CombatSounds.EnemyFall, CombatSounds.PunchHits);
            Assert.NotEqual(CombatSounds.KickSwing, CombatSounds.EnemyFall);
        }
    }
}
