using System;
using System.Collections.Generic;
using System.Linq;
using Curitiba.Core.Audio;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.Audio
{
    /// <summary>
    /// When the corridor moans: the gap between moans, what an empty corridor does, and that a
    /// run of moans spends the whole bank.
    /// </summary>
    /// <remarks>
    /// The instant of a moan is drawn from a <see cref="Random"/>, so the seed is pinned here and
    /// every assertion below is written to hold for <em>any</em> seed — a window has a floor and a
    /// ceiling, and counting over a stretch long enough to bracket both is what makes a random
    /// schedule testable without pretending it is not random.
    /// </remarks>
    public class ZombieMoanSchedulerTests
    {
        private const int Seed = 20260919;

        private static ZombieMoanScheduler NewScheduler() =>
            new ZombieMoanScheduler(rng: new Random(Seed));

        /// <summary>Every moan fired over <paramref name="seconds"/> with a corridor of that size.</summary>
        private static List<string> MoansOver(ZombieMoanScheduler scheduler, float seconds, int zombiesAlive)
        {
            var fired = new List<string>();
            int frames = Frames.FramesFor(seconds);
            for (int i = 0; i < frames; i++)
            {
                string asset = scheduler.Tick(Frames.DefaultStep, zombiesAlive);
                if (asset != null) fired.Add(asset);
            }

            return fired;
        }

        // ---------------------------------------------------------------- the first moan

        [Fact]
        public void AFreshScheduler_ShouldNotMoanOnItsFirstFrame()
        {
            // Arrange
            var scheduler = NewScheduler();

            // Act
            string asset = scheduler.Tick(Frames.DefaultStep, 3);

            // Assert — a wave that walked in already moaning would sound like the sound was
            // triggered by the spawn, not by the zombies standing there.
            Assert.Null(asset);
        }

        [Fact]
        public void AWaveWalkingIn_ShouldAnnounceItselfQuickly()
        {
            // Arrange
            var scheduler = NewScheduler();

            // Act - the opening gap, not the steady one: a corridor that has just filled up
            // waits far less than it will between moans from then on.
            List<string> fired = MoansOver(scheduler, ZombieAmbience.OpeningMoanMax + 0.1f, 3);

            // Assert - a wave the player can see should be a wave the player can hear, and
            // several seconds of silence over visible zombies reads as the sound being broken.
            Assert.Single(fired);
            Assert.Contains(fired[0], ZombieAmbience.Moans);
        }

        [Fact]
        public void AWaveWalkingIn_ShouldStillNotMoanInstantly()
        {
            // Arrange
            var scheduler = NewScheduler();

            // Act
            List<string> fired = MoansOver(scheduler, ZombieAmbience.OpeningMoanMin - 0.1f, 3);

            // Assert - prompt is not the same as immediate: a moan on the very frame the first
            // body appears sounds triggered by the spawn rather than by the zombies.
            Assert.Empty(fired);
        }

        [Fact]
        public void AfterTheOpeningMoan_ShouldSettleIntoTheWindow()
        {
            // Arrange - spend the opening moan first.
            var scheduler = NewScheduler();
            Assert.Single(MoansOver(scheduler, ZombieAmbience.OpeningMoanMax + 0.1f, 1));

            // Act - past the ceiling of the steady window, but nowhere near two of them.
            List<string> fired = MoansOver(scheduler, ZombieAmbience.LonelyWindowMax + 1f, 1);

            // Assert - exactly one, and it came from the bank rather than from nowhere.
            Assert.Single(fired);
            Assert.Contains(fired[0], ZombieAmbience.Moans);
        }

        [Fact]
        public void AMoan_ShouldNotFireAgainOnTheNextFrame()
        {
            // Arrange - run right up to the first moan.
            var scheduler = NewScheduler();
            string moan = null;
            for (int i = 0; i < Frames.FramesFor(ZombieAmbience.LonelyWindowMax + 1f) && moan == null; i++)
                moan = scheduler.Tick(Frames.DefaultStep, 1);

            // Act
            string next = scheduler.Tick(Frames.DefaultStep, 1);

            // Assert - the window rearms on the moan; without that the timer would sit at zero
            // and moan every frame from then on.
            Assert.NotNull(moan);
            Assert.Null(next);
        }

        // ---------------------------------------------------------------- an empty corridor

        [Fact]
        public void AnEmptyCorridor_ShouldNeverMoan()
        {
            // Arrange
            var scheduler = NewScheduler();

            // Act
            List<string> fired = MoansOver(scheduler, 60f, 0);

            // Assert - between waves there is nothing there to moan.
            Assert.Empty(fired);
        }

        [Fact]
        public void ACorridorThatFillsUp_ShouldStartItsWindowOver()
        {
            // Arrange - a long empty stretch, far past any window.
            var scheduler = NewScheduler();
            MoansOver(scheduler, 60f, 0);

            // Act - the next wave walks in.
            List<string> early = MoansOver(scheduler, ZombieAmbience.OpeningMoanMin - 0.1f, 1);
            List<string> announced = MoansOver(scheduler, ZombieAmbience.OpeningMoanMax + 0.1f, 1);

            // Assert - the clock does not run in the empty corridor, so the wave neither arrives
            // with a moan already due nor inherits the long steady gap: every wave announces
            // itself the same way the first one did.
            Assert.Empty(early);
            Assert.Single(announced);
        }

        // ---------------------------------------------------------------- the bank and the crowd

        [Fact]
        public void ConsecutiveMoans_ShouldSpendTheWholeBank()
        {
            // Arrange
            var scheduler = NewScheduler();

            // Act - long enough to guarantee at least three moans at full crowd.
            List<string> fired = MoansOver(scheduler, ZombieAmbience.CrowdWindowMax * 4f, ZombieAmbience.FullCrowd);

            // Assert - a lone sample on repeat is the thing the bank exists to avoid, and
            // ambience repeats for far longer than a fight does.
            Assert.True(fired.Count >= 3, $"expected at least three moans, got {fired.Count}");
            Assert.Equal(3, fired.Take(3).Distinct().Count());
        }

        [Fact]
        public void ABiggerCrowd_ShouldMoanMoreOverTheSameStretch()
        {
            // Arrange - same seed, same stretch, different corridors.
            const float Stretch = 60f;

            // Act
            int lonely = MoansOver(NewScheduler(), Stretch, 1).Count;
            int packed = MoansOver(NewScheduler(), Stretch, ZombieAmbience.FullCrowd).Count;

            // Assert - the windows do not overlap over this stretch, so this holds for any seed.
            Assert.True(packed > lonely, $"a crowd of {ZombieAmbience.FullCrowd} moaned {packed} times, a lone zombie {lonely}");
        }

        [Fact]
        public void EveryGap_ShouldFallInsideTheWindow()
        {
            // Arrange - the opening moan is spent first; its gap is deliberately not one of
            // these, and measuring from it would fail the very rule under test.
            var scheduler = NewScheduler();
            MoansOver(scheduler, ZombieAmbience.OpeningMoanMax + 0.1f, ZombieAmbience.FullCrowd);
            (float Min, float Max) window = ZombieAmbience.WindowFor(ZombieAmbience.FullCrowd);
            var gaps = new List<float>();
            float since = 0f;

            // Act
            for (int i = 0; i < Frames.FramesFor(300f); i++)
            {
                since += Frames.DefaultStep;
                if (scheduler.Tick(Frames.DefaultStep, ZombieAmbience.FullCrowd) == null) continue;
                gaps.Add(since);
                since = 0f;
            }

            // Assert - a draw that escaped the window would either stack moans on top of each
            // other or go silent for a stretch that reads as the sound having broken.
            Assert.True(gaps.Count > 20, $"expected a long run of moans, got {gaps.Count}");
            Assert.All(gaps, gap => Assert.InRange(gap, window.Min, window.Max + Frames.DefaultStep));
        }
    }
}
