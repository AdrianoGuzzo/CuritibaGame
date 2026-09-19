using System.Linq;
using Curitiba.Core.Audio;
using Xunit;

namespace Curitiba.Tests.Audio
{
    /// <summary>
    /// The crowd's own sound: the bank a moan draws from, how loud it sits, and how the gap
    /// between moans tightens as the corridor fills up.
    /// </summary>
    public class ZombieAmbienceTests
    {
        // ---------------------------------------------------------------- the bank

        [Fact]
        public void TheMoanBank_ShouldOfferMoreThanOneVariant()
        {
            // Assert — one sample on a loop is the thing a bank exists to avoid, and ambience
            // repeats far longer than a fight does.
            Assert.True(ZombieAmbience.Moans.Count > 1);
        }

        [Fact]
        public void TheMoanBank_ShouldNotRepeatAnAsset()
        {
            // Assert — near-identical asset strings are exactly where a copy-paste slips through,
            // and a duplicate would silently make the rotation shorter than it looks.
            Assert.Equal(ZombieAmbience.Moans.Count, ZombieAmbience.Moans.Distinct().Count());
        }

        [Fact]
        public void TheMoanBank_ShouldNotBorrowACombatSound()
        {
            // Assert — ambience and combat are different channels of meaning. A moan that was
            // also an impact would make the crowd sound like it was being hit.
            Assert.Empty(ZombieAmbience.Moans.Intersect(CombatSounds.PunchHits));
            Assert.DoesNotContain(CombatSounds.KickSwing, ZombieAmbience.Moans);
        }

        // ---------------------------------------------------------------- how loud

        [Fact]
        public void AMoan_ShouldSitUnderTheMusicBed()
        {
            // Assert — the first sound in the game deliberately *below* the 0.30 bed. Presence is
            // something you notice without listening for it; a moan that read over the music
            // would be an event, which is the opposite of what it is for.
            Assert.InRange(ZombieAmbience.MoanVolume, 0f, ArenaMusicPolicy.BackgroundVolume);
        }

        [Fact]
        public void AMoan_ShouldBeQuieterThanAnyBlow()
        {
            // Assert — combat is the foreground. A moan that competed with the swing or the
            // impact would blur the confirmation the player actually plays on.
            Assert.True(ZombieAmbience.MoanVolume < CombatSounds.KickSwingVolume);
            Assert.True(ZombieAmbience.MoanVolume < CombatSounds.PunchHitVolume);
        }

        // ---------------------------------------------------------------- the gap

        [Theory]
        [InlineData(1, 2)]
        [InlineData(1, 5)]
        [InlineData(2, 4)]
        public void AFullerCrowd_ShouldMoanMoreOften(int fewer, int more)
        {
            // Act
            (float Min, float Max) sparse = ZombieAmbience.WindowFor(fewer);
            (float Min, float Max) packed = ZombieAmbience.WindowFor(more);

            // Assert — more undead on screen is more presence, and that is the whole of what
            // "each zombie moans" can mean on a channel with one voice and no panning.
            Assert.True(packed.Min < sparse.Min);
            Assert.True(packed.Max < sparse.Max);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(99)]
        public void EveryCrowdSize_ShouldGiveAUsableWindow(int zombiesAlive)
        {
            // Act
            (float Min, float Max) window = ZombieAmbience.WindowFor(zombiesAlive);

            // Assert — a window that inverted or went negative would either moan every frame or
            // never moan again, and a wave count is external data that can be anything.
            Assert.True(window.Min > 0f);
            Assert.True(window.Min <= window.Max);
        }

        [Fact]
        public void ACrowdBeyondTheFullOne_ShouldNotKeepTightening()
        {
            // Assert — the gap has a floor. Without one, a big authored wave would moan
            // continuously, which reads as a stuck sound rather than as a lot of zombies.
            Assert.Equal(ZombieAmbience.WindowFor(ZombieAmbience.FullCrowd),
                         ZombieAmbience.WindowFor(ZombieAmbience.FullCrowd + 20));
        }
    }
}
