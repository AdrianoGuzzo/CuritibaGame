using System;
using System.Collections.Generic;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Turns a hand-authored <see cref="ScoreConfig"/> into the <see cref="ScoreRules"/> the
    /// mechanic runs on, sorting the ladders, clamping the values and filling in what was left out.
    /// </summary>
    /// <remarks>
    /// The counterpart of <see cref="Combat.CombatDefaults.BuildChain"/>, and the single place that
    /// deals with hostile data. A stage file, a Tiled import or the in-game editor can hand over
    /// nulls, empty tables, unsorted rungs, duplicates and negative numbers; past this point the
    /// rules are sane, so no scoring path needs a guard of its own.
    /// <para>
    /// The distinction that matters: a <c>null</c> ladder means "not authored" and falls back to the
    /// designed table, while an empty one means "authored as none" and switches that ladder off.
    /// Without it, a designer could never turn a bonus off from data.
    /// </para>
    /// </remarks>
    internal static class ScoreDefaults
    {
        /// <summary>
        /// Floor for the combo window. A zero or negative duration in the data would break every
        /// combo on the frame after it started, switching the whole mechanic off in silence.
        /// </summary>
        public const float MinimumComboDuration = 0.1f;

        private static readonly ScoreTier[] NoTiers = new ScoreTier[0];
        private static readonly int[] NoPercentages = new int[0];

        public static ScoreRules Build(ScoreConfig config)
        {
            ScoreConfig c = config ?? ScoreConfig.Defaults();
            ScoreConfig fallback = ScoreConfig.Defaults();

            float duration = c.ComboDuration > 0f ? c.ComboDuration : MinimumComboDuration;
            int ceiling = Math.Max(1, c.MaxMultiplier);

            HitPointsDef hits = c.HitPoints ?? fallback.HitPoints;
            EnemyPointsDef enemies = c.EnemyPoints ?? fallback.EnemyPoints;

            return new ScoreRules(
                duration,
                ceiling,
                c.ResetComboOnDamage,
                Math.Max(1, c.ComboPenaltyOnDamage),
                // Order matches the AttackType / EnemyType members: the enum value is the index.
                new[]
                {
                    Points(hits.Normal), Points(hits.Heavy), Points(hits.Air), Points(hits.Finisher),
                },
                new[]
                {
                    Points(enemies.Normal), Points(enemies.Strong), Points(enemies.Boss),
                },
                Ladder(c.MultiplierThresholds, fallback.MultiplierThresholds,
                    d => Math.Max(0, d.MinCombo), d => Math.Min(ceiling, Math.Max(1, d.Multiplier))),
                Percentages(c.VarietyBonusPercent, fallback.VarietyBonusPercent),
                Ladder(c.NoDamageBonuses, fallback.NoDamageBonuses,
                    d => Math.Max(1, d.Hits), d => Points(d.Bonus)),
                Ladder(c.ComboEndBonuses, fallback.ComboEndBonuses,
                    d => Math.Max(1, d.MinCombo), d => Points(d.Bonus)));
        }

        /// <summary>A negative price is meaningless and must never drain the score.</summary>
        private static int Points(int value) => Math.Max(0, value);

        private static int[] Percentages(List<int> authored, List<int> fallback)
        {
            // The fallback comes from ScoreConfig.Defaults(), whose ladders are never null, so the
            // only case left is a ladder authored as empty — which means "pay nothing".
            List<int> source = authored ?? fallback;
            if (source.Count == 0)
                return NoPercentages;

            var result = new int[source.Count];
            for (int i = 0; i < source.Count; i++)
                result[i] = Points(source[i]);

            return result;
        }

        private static ScoreTier[] Ladder<T>(List<T> authored, List<T> fallback,
            Func<T, int> threshold, Func<T, int> value)
            where T : class
        {
            List<T> source = authored ?? fallback;
            if (source.Count == 0)
                return NoTiers;

            var tiers = new List<ScoreTier>(source.Count);
            foreach (T entry in source)
            {
                // A null row in a hand-edited list is a typo, not a rung.
                if (entry != null)
                    tiers.Add(new ScoreTier(threshold(entry), value(entry)));
            }

            return Normalise(tiers);
        }

        /// <summary>
        /// Sorts a ladder by threshold and drops duplicates, keeping the last one authored. The sort
        /// is a stable insertion sort — the ladders are a handful of entries long, and stability is
        /// what makes "the last duplicate wins" a defined outcome rather than a coin toss.
        /// </summary>
        private static ScoreTier[] Normalise(List<ScoreTier> tiers)
        {
            for (int i = 1; i < tiers.Count; i++)
            {
                ScoreTier current = tiers[i];
                int j = i - 1;
                while (j >= 0 && tiers[j].Threshold > current.Threshold)
                {
                    tiers[j + 1] = tiers[j];
                    j--;
                }

                tiers[j + 1] = current;
            }

            var result = new List<ScoreTier>(tiers.Count);
            for (int i = 0; i < tiers.Count; i++)
            {
                bool lastOfItsThreshold = i == tiers.Count - 1 || tiers[i + 1].Threshold != tiers[i].Threshold;
                if (lastOfItsThreshold)
                    result.Add(tiers[i]);
            }

            return result.ToArray();
        }
    }
}
