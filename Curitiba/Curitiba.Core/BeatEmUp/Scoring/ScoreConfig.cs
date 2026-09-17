using System.Collections.Generic;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Every number the scoring mechanic turns, in one place: the combo window, the multiplier
    /// ladder, what each blow and each enemy is worth, and the bonus tables.
    /// </summary>
    /// <remarks>
    /// Shaped like <see cref="FighterTuning"/> — a plain mutable DTO with property initialisers and
    /// a <see cref="Defaults"/> factory — so the day this becomes a <c>"scoring"</c> block in the
    /// stage JSON it serialises as it stands, and a partial block keeps the remaining defaults.
    /// <para>
    /// Nothing here is read during play: <see cref="ScoreDefaults.Build"/> resolves it once into the
    /// sanitised <see cref="ScoreRules"/> a <see cref="ScoreSystem"/> holds, so a config mutated
    /// afterwards (the in-game editor does exactly that to live DTOs) cannot change the rules
    /// mid-combo.
    /// </para>
    /// <para>
    /// For every ladder below, <c>null</c> and empty mean different things: null is "not authored,
    /// use the designed table", empty is "authored as none" and switches that bonus off.
    /// </para>
    /// </remarks>
    public sealed class ScoreConfig
    {
        /// <summary>Seconds a combo survives without a new landed blow.</summary>
        public float ComboDuration { get; set; } = 2.0f;

        /// <summary>Ceiling for the multiplier ladder, however the tiers are authored.</summary>
        public int MaxMultiplier { get; set; } = 5;

        /// <summary>
        /// Whether taking damage ends the combo outright. False instead docks
        /// <see cref="ComboPenaltyOnDamage"/> hits off it.
        /// </summary>
        public bool ResetComboOnDamage { get; set; } = true;

        /// <summary>Hits knocked off the combo per hit taken, when <see cref="ResetComboOnDamage"/> is false.</summary>
        public int ComboPenaltyOnDamage { get; set; } = 5;

        /// <summary>What a landed blow is worth, before the multiplier.</summary>
        public HitPointsDef HitPoints { get; set; } = new HitPointsDef();

        /// <summary>What a defeated enemy is worth, before the multiplier.</summary>
        public EnemyPointsDef EnemyPoints { get; set; } = new EnemyPointsDef();

        /// <summary>
        /// The multiplier ladder: the lowest combo that earns each multiplier. Resolved by taking
        /// the highest tier the combo reaches, so order in the file does not matter.
        /// </summary>
        public List<MultiplierTierDef> MultiplierThresholds { get; set; } = new List<MultiplierTierDef>
        {
            new MultiplierTierDef { MinCombo = 0, Multiplier = 1 },
            new MultiplierTierDef { MinCombo = 5, Multiplier = 2 },
            new MultiplierTierDef { MinCombo = 10, Multiplier = 3 },
            new MultiplierTierDef { MinCombo = 20, Multiplier = 4 },
            new MultiplierTierDef { MinCombo = 30, Multiplier = 5 },
        };

        /// <summary>
        /// Percentage added to a combo's earned score per distinct attack type used in it, indexed
        /// by (distinct types - 1). The last entry applies to anything beyond the table.
        /// </summary>
        public List<int> VarietyBonusPercent { get; set; } = new List<int> { 0, 10, 20, 30 };

        /// <summary>Flat bonuses paid as a run of blows without taking damage passes each mark.</summary>
        public List<NoDamageBonusDef> NoDamageBonuses { get; set; } = new List<NoDamageBonusDef>
        {
            new NoDamageBonusDef { Hits = 5, Bonus = 100 },
            new NoDamageBonusDef { Hits = 10, Bonus = 300 },
            new NoDamageBonusDef { Hits = 20, Bonus = 750 },
            new NoDamageBonusDef { Hits = 30, Bonus = 1500 },
        };

        /// <summary>Flat bonus paid once a combo ends, by how long it got.</summary>
        public List<ComboEndBonusDef> ComboEndBonuses { get; set; } = new List<ComboEndBonusDef>
        {
            new ComboEndBonusDef { MinCombo = 10, Bonus = 500 },
            new ComboEndBonusDef { MinCombo = 20, Bonus = 1500 },
            new ComboEndBonusDef { MinCombo = 30, Bonus = 4000 },
        };

        /// <summary>The designed balance sheet. Same shape as <see cref="FighterTuning.SofiaDefaults"/>.</summary>
        public static ScoreConfig Defaults() => new ScoreConfig();
    }

    /// <summary>Base points per <see cref="AttackType"/>.</summary>
    public sealed class HitPointsDef
    {
        public int Normal { get; set; } = 100;
        public int Heavy { get; set; } = 200;
        public int Air { get; set; } = 250;
        public int Finisher { get; set; } = 500;
    }

    /// <summary>Base points per <see cref="EnemyType"/>.</summary>
    public sealed class EnemyPointsDef
    {
        public int Normal { get; set; } = 500;
        public int Strong { get; set; } = 1000;
        public int Boss { get; set; } = 5000;
    }

    /// <summary>One rung of the multiplier ladder.</summary>
    public sealed class MultiplierTierDef
    {
        /// <summary>The lowest combo that earns <see cref="Multiplier"/>.</summary>
        public int MinCombo { get; set; }

        /// <summary>What blows and bounties are multiplied by from this combo up.</summary>
        public int Multiplier { get; set; }
    }

    /// <summary>One mark in the "no damage taken" ladder.</summary>
    public sealed class NoDamageBonusDef
    {
        /// <summary>Blows landed without taking damage that earn this bonus.</summary>
        public int Hits { get; set; }

        /// <summary>Flat points paid once, when the streak reaches <see cref="Hits"/>.</summary>
        public int Bonus { get; set; }
    }

    /// <summary>One rung of the combo-end bonus ladder.</summary>
    public sealed class ComboEndBonusDef
    {
        /// <summary>The shortest combo that earns <see cref="Bonus"/> when it ends.</summary>
        public int MinCombo { get; set; }

        /// <summary>Flat points paid when a combo of at least <see cref="MinCombo"/> ends.</summary>
        public int Bonus { get; set; }
    }
}
