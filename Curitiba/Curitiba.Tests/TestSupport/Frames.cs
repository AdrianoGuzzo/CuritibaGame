using System;
using Curitiba.Core.BeatEmUp;
using Microsoft.Xna.Framework;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// Drives the game loop from a test with a fixed timestep.
    /// </summary>
    /// <remarks>
    /// Everything in the simulation reads <c>gameTime.ElapsedGameTime.TotalSeconds</c> and nothing
    /// reads the wall clock, so a synthetic <see cref="GameTime"/> makes every timing rule exactly
    /// reproducible. No test ever sleeps.
    /// </remarks>
    internal static class Frames
    {
        /// <summary>The timestep tests use unless they need something else: 60 FPS.</summary>
        public const float DefaultStep = 1f / 60f;

        /// <summary>A single frame of <paramref name="dt"/> seconds.</summary>
        public static GameTime Step(float dt = DefaultStep) =>
            new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(dt));

        /// <summary>
        /// Number of whole frames of <paramref name="dt"/> needed to cover <paramref name="seconds"/>,
        /// rounded up, so "advance past this duration" is expressed once rather than per test.
        /// </summary>
        public static int FramesFor(float seconds, float dt = DefaultStep) =>
            (int)Math.Ceiling(seconds / dt);

        /// <summary>Updates <paramref name="fighter"/> for <paramref name="frames"/> frames.</summary>
        public static void Advance(Fighter fighter, int frames, float dt = DefaultStep)
        {
            GameTime step = Step(dt);
            for (int i = 0; i < frames; i++)
                fighter.Update(step);
        }

        /// <summary>Updates <paramref name="fighter"/> until at least <paramref name="seconds"/> have elapsed.</summary>
        public static void AdvanceSeconds(Fighter fighter, float seconds, float dt = DefaultStep) =>
            Advance(fighter, FramesFor(seconds, dt), dt);

        /// <summary>
        /// Updates until <paramref name="predicate"/> holds, or gives up after <paramref name="maxFrames"/>.
        /// Returns the number of frames actually run, so a test can assert it stopped early.
        /// </summary>
        public static int AdvanceUntil(Fighter fighter, Func<bool> predicate, int maxFrames = 600,
                                       float dt = DefaultStep)
        {
            GameTime step = Step(dt);
            for (int i = 0; i < maxFrames; i++)
            {
                if (predicate())
                    return i;
                fighter.Update(step);
            }
            return maxFrames;
        }
    }
}
