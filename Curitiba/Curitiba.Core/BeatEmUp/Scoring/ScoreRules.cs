namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// One rung of a scoring ladder: the threshold that unlocks it and what it is worth.
    /// </summary>
    /// <remarks>
    /// The same shape answers all three ladders — multiplier by combo, bonus by streak, bonus by
    /// final combo — so they are sorted, de-duplicated and looked up by one piece of code.
    /// </remarks>
    internal readonly struct ScoreTier
    {
        /// <summary>The lowest count that reaches this rung.</summary>
        public readonly int Threshold;

        /// <summary>What the rung is worth: a multiplier, or a flat bonus in points.</summary>
        public readonly int Value;

        public ScoreTier(int threshold, int value)
        {
            Threshold = threshold;
            Value = value;
        }
    }

    /// <summary>
    /// The resolved, sanitised form of a <see cref="ScoreConfig"/>: ladders sorted and clamped,
    /// tables never null, every lookup in range. What <see cref="ScoreSystem"/> actually reads.
    /// </summary>
    /// <remarks>
    /// This is to <see cref="ScoreConfig"/> what <see cref="Combat.ComboChainDef"/> is to
    /// <see cref="FighterTuning"/>: the hostile-data handling happens once, in
    /// <see cref="ScoreDefaults.Build"/>, and everything downstream can assume sane values.
    /// </remarks>
    internal sealed class ScoreRules
    {
        private readonly int[] hitPoints;
        private readonly int[] enemyPoints;
        private readonly ScoreTier[] multiplierTiers;
        private readonly int[] varietyPercent;
        private readonly ScoreTier[] noDamageMilestones;
        private readonly ScoreTier[] comboEndBonuses;

        public ScoreRules(
            float comboDuration,
            int maxMultiplier,
            bool resetComboOnDamage,
            int comboPenaltyOnDamage,
            int[] hitPoints,
            int[] enemyPoints,
            ScoreTier[] multiplierTiers,
            int[] varietyPercent,
            ScoreTier[] noDamageMilestones,
            ScoreTier[] comboEndBonuses)
        {
            ComboDuration = comboDuration;
            MaxMultiplier = maxMultiplier;
            ResetComboOnDamage = resetComboOnDamage;
            ComboPenaltyOnDamage = comboPenaltyOnDamage;
            this.hitPoints = hitPoints;
            this.enemyPoints = enemyPoints;
            this.multiplierTiers = multiplierTiers;
            this.varietyPercent = varietyPercent;
            this.noDamageMilestones = noDamageMilestones;
            this.comboEndBonuses = comboEndBonuses;
        }

        /// <summary>Seconds a combo survives without a new landed blow. Always positive.</summary>
        public float ComboDuration { get; }

        /// <summary>Ceiling already applied to every rung of the multiplier ladder. At least 1.</summary>
        public int MaxMultiplier { get; }

        /// <summary>Whether damage ends the combo outright rather than docking it.</summary>
        public bool ResetComboOnDamage { get; }

        /// <summary>Hits docked off the combo per hit taken. At least 1.</summary>
        public int ComboPenaltyOnDamage { get; }

        /// <summary>How many marks the "no damage taken" ladder has.</summary>
        public int MilestoneCount => noDamageMilestones.Length;

        /// <summary>The streak length that earns mark <paramref name="index"/>, lowest first.</summary>
        public int MilestoneHits(int index) => noDamageMilestones[index].Threshold;

        /// <summary>The flat bonus mark <paramref name="index"/> pays.</summary>
        public int MilestoneBonus(int index) => noDamageMilestones[index].Value;

        /// <summary>
        /// Base points for a blow. The caller narrows the value to a declared member first — an
        /// enum in C# holds any int, and the bitmask in <see cref="ScoreSystem"/> needs it narrowed
        /// anyway — so this indexes the table directly rather than guarding it a second time.
        /// </summary>
        public int PointsFor(AttackType attackType) => hitPoints[(int)attackType];

        /// <inheritdoc cref="PointsFor"/>
        public int BountyFor(EnemyType enemyType) => enemyPoints[(int)enemyType];

        /// <summary>
        /// The multiplier a combo of this length earns: the highest rung it reaches, or 1 when it
        /// reaches none. Scanned backwards over a handful of entries — cheaper than a binary search
        /// at this size, and it allocates nothing.
        /// </summary>
        public int MultiplierFor(int combo)
        {
            for (int i = multiplierTiers.Length - 1; i >= 0; i--)
            {
                if (combo >= multiplierTiers[i].Threshold)
                    return multiplierTiers[i].Value;
            }

            return 1;
        }

        /// <summary>
        /// The percentage a combo using this many distinct attack types earns. Beyond the table the
        /// last rung applies; no types used earns nothing.
        /// </summary>
        public int VarietyPercentFor(int distinctTypes)
        {
            if (varietyPercent.Length == 0 || distinctTypes <= 0)
                return 0;

            int index = distinctTypes - 1;
            return index < varietyPercent.Length ? varietyPercent[index] : varietyPercent[varietyPercent.Length - 1];
        }

        /// <summary>The flat bonus a combo of this final length pays out, or 0 if it reaches no rung.</summary>
        public int EndBonusFor(int finalCombo)
        {
            for (int i = comboEndBonuses.Length - 1; i >= 0; i--)
            {
                if (finalCombo >= comboEndBonuses[i].Threshold)
                    return comboEndBonuses[i].Value;
            }

            return 0;
        }
    }
}
