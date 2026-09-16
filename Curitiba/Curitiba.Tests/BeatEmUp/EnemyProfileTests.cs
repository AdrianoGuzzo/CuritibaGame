using System;
using Curitiba.Core.BeatEmUp;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// The personality archetypes that vary how an enemy fights, and how the JSON
    /// <c>personalities</c> block overrides the built-in numbers.
    /// </summary>
    public class EnemyProfileTests
    {
        // Personalities are named rather than passed as the internal enum, since a public
        // test signature cannot mention an internal type.
        [Theory]
        [InlineData("Aggressive", 0.92f, 0.8f, 40f, 320f, 1.6f)]
        [InlineData("Defensive", 0.4f, 1.8f, 120f, 320f, 1.4f)]
        [InlineData("Runner", 0.72f, 1.2f, 60f, 220f, 1.9f)]
        [InlineData("Balanced", 0.7f, 1.3f, 60f, 300f, 1.5f)]
        public void BuiltInProfile_ShouldMatchItsArchetype(string personality, float chance, float cooldown,
                                                           float preferred, float runDistance, float runMultiplier)
        {
            EnemyProfile profile = EnemyProfile.From(Parse(personality));

            Assert.Equal(personality, profile.Profile.ToString());
            Assert.Equal(chance, profile.AttackChance, 4);
            Assert.Equal(cooldown, profile.AttackCooldown, 4);
            Assert.Equal(preferred, profile.PreferredDistance, 4);
            Assert.Equal(runDistance, profile.RunDistance, 4);
            Assert.Equal(runMultiplier, profile.RunSpeedMultiplier, 4);
        }

        [Fact]
        public void AggressiveProfile_ShouldBeKeenerThanDefensive()
        {
            EnemyProfile aggressive = EnemyProfile.From(Parse("Aggressive"));
            EnemyProfile defensive = EnemyProfile.From(Parse("Defensive"));

            Assert.True(aggressive.AttackChance > defensive.AttackChance);
            Assert.True(aggressive.AttackCooldown < defensive.AttackCooldown);
            Assert.True(aggressive.PreferredDistance < defensive.PreferredDistance);
        }

        [Fact]
        public void RunnerProfile_ShouldSprintSoonerAndFaster()
        {
            EnemyProfile runner = EnemyProfile.From(Parse("Runner"));
            EnemyProfile balanced = EnemyProfile.From(Parse("Balanced"));

            Assert.True(runner.RunDistance < balanced.RunDistance, "a runner closes from further out");
            Assert.True(runner.RunSpeedMultiplier > balanced.RunSpeedMultiplier);
        }

        [Fact]
        public void OutOfRangePersonality_ShouldFallBackToBalanced()
        {
            // The switch's default branch deliberately rewrites Profile as well as the numbers.
            EnemyProfile profile = EnemyProfile.From((EnemyPersonality)99);

            Assert.Equal("Balanced", profile.Profile.ToString());
            Assert.Equal(0.7f, profile.AttackChance, 4);
        }

        [Fact]
        public void NullDefinition_ShouldFallBackToTheBuiltInNumbers()
        {
            EnemyProfile fromNull = EnemyProfile.From(Parse("Aggressive"), null);
            EnemyProfile builtIn = EnemyProfile.From(Parse("Aggressive"));

            Assert.Equal(builtIn.AttackChance, fromNull.AttackChance);
            Assert.Equal(builtIn.AttackCooldown, fromNull.AttackCooldown);
        }

        [Fact]
        public void Definition_ShouldOverrideEveryNumber()
        {
            var def = new PersonalityDef
            {
                AttackChance = 0.11f,
                AttackCooldown = 2.2f,
                PreferredDistance = 33f,
                RunDistance = 44f,
                RunSpeedMultiplier = 5.5f,
            };

            EnemyProfile profile = EnemyProfile.From(Parse("Aggressive"), def);

            Assert.Equal("Aggressive", profile.Profile.ToString());
            Assert.Equal(0.11f, profile.AttackChance, 4);
            Assert.Equal(2.2f, profile.AttackCooldown, 4);
            Assert.Equal(33f, profile.PreferredDistance, 4);
            Assert.Equal(44f, profile.RunDistance, 4);
            Assert.Equal(5.5f, profile.RunSpeedMultiplier, 4);
        }

        [Fact]
        public void Definition_ShouldBeCopiedVerbatim_WithoutValidation()
        {
            // A PersonalityDef straight out of new() is all zeros, and nothing clamps it. Recording
            // this keeps the behaviour honest: a personality block that omits fields makes an enemy
            // that never attacks, rather than one that quietly gets the defaults.
            EnemyProfile profile = EnemyProfile.From(Parse("Balanced"), new PersonalityDef());

            Assert.Equal(0f, profile.AttackChance);
            Assert.Equal(0f, profile.AttackCooldown);
        }

        private static EnemyPersonality Parse(string name) =>
            (EnemyPersonality)Enum.Parse(typeof(EnemyPersonality), name);
    }
}
