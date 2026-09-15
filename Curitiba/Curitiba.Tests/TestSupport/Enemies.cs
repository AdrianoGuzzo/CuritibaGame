using System.Collections.Generic;
using Curitiba.Core.BeatEmUp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// Builds <see cref="PiaLocoEnemy"/> instances for tests. The real constructor takes six
    /// collaborators; almost every test cares about one or two of them, so the rest get sane,
    /// inert defaults here instead of being repeated at each call site.
    /// </summary>
    internal static class Enemies
    {
        /// <summary>An attack chance that always passes the roll, removing the AI's only randomness.</summary>
        public const float AlwaysAttacks = 1f;

        /// <summary>
        /// An attack chance that can never pass the roll. <c>Random.NextDouble()</c> returns a value
        /// in [0,1), and the check is <c>value &lt;= chance</c>, so a negative chance is the only way
        /// to be certain — 0f would still pass on an exact 0.0 draw.
        /// </summary>
        public const float NeverAttacks = -1f;

        /// <summary>Creates an enemy with the given profile, at <paramref name="position"/>.</summary>
        public static PiaLocoEnemy Create(ContentManager content, SofiaPlayer target,
                                          Vector2 position = default,
                                          EnemyProfile? profile = null,
                                          int hitsToKnockdown = 0,
                                          AttackSlotManager slots = null,
                                          IReadOnlyList<PiaLocoEnemy> neighbors = null,
                                          FighterTuning tuning = null)
        {
            return new PiaLocoEnemy(
                content,
                null,
                position,
                target,
                hitsToKnockdown,
                slots ?? new AttackSlotManager(),
                neighbors ?? new List<PiaLocoEnemy>(),
                profile ?? EnemyProfile.From(EnemyPersonality.Balanced),
                tuning);
        }

        /// <summary>A profile that differs from the built-ins only in its attack eagerness.</summary>
        public static EnemyProfile ProfileWithChance(float attackChance,
                                                     EnemyPersonality personality = EnemyPersonality.Balanced)
        {
            EnemyProfile profile = EnemyProfile.From(personality);
            profile.AttackChance = attackChance;
            return profile;
        }
    }
}
