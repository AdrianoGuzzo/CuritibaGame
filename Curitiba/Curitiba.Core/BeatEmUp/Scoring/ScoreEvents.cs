namespace Curitiba.Core.BeatEmUp
{
    /// <summary>Why points were awarded.</summary>
    /// <remarks>
    /// Doubles as the rule for what counts towards a combo's own ledger: only <see cref="Hit"/> and
    /// <see cref="EnemyDefeated"/> are <em>earned</em> in the sequence, so the variety bonus is a
    /// percentage of those alone and never of another bonus.
    /// </remarks>
    internal enum ScoreReason
    {
        /// <summary>A blow that connected.</summary>
        Hit,

        /// <summary>The bounty for putting an enemy down.</summary>
        EnemyDefeated,

        /// <summary>A mark passed in the run of blows without taking damage.</summary>
        NoDamageBonus,

        /// <summary>The share paid at the end of a combo for the variety of blows in it.</summary>
        VarietyBonus,

        /// <summary>The flat payout for how long a combo got.</summary>
        ComboEndBonus,
    }

    /// <summary>What ended a combo.</summary>
    internal enum ComboBreakReason
    {
        /// <summary>The window ran out with no new blow landing.</summary>
        Timeout,

        /// <summary>The player was hit.</summary>
        Damage,

        /// <summary>A caller settled it — the stage ending, the player being defeated.</summary>
        Manual,
    }

    /// <summary>Points were banked.</summary>
    internal readonly struct ScoreChangedEvent
    {
        /// <summary>The run's total after the award.</summary>
        public readonly long Total;

        /// <summary>What was just awarded.</summary>
        public readonly long Gained;

        /// <summary>What earned it.</summary>
        public readonly ScoreReason Reason;

        public ScoreChangedEvent(long total, long gained, ScoreReason reason)
        {
            Total = total;
            Gained = gained;
            Reason = reason;
        }
    }

    /// <summary>A combo started or grew. Carries the multiplier already brought up to date.</summary>
    internal readonly struct ComboChangedEvent
    {
        /// <summary>Blows landed in the combo now running.</summary>
        public readonly int Combo;

        /// <summary>What that combo currently earns.</summary>
        public readonly int Multiplier;

        public ComboChangedEvent(int combo, int multiplier)
        {
            Combo = combo;
            Multiplier = multiplier;
        }
    }

    /// <summary>A combo ended, with everything a HUD needs to show what it was worth.</summary>
    internal readonly struct ComboBrokenEvent
    {
        /// <summary>How many blows the combo reached.</summary>
        public readonly int FinalCombo;

        /// <summary>The multiplier it was earning when it ended.</summary>
        public readonly int PeakMultiplier;

        /// <summary>What its blows and bounties earned, before bonuses.</summary>
        public readonly long ComboScore;

        /// <summary>The variety share plus the tier payout settled just now.</summary>
        public readonly long BonusScore;

        /// <summary>What ended it.</summary>
        public readonly ComboBreakReason Reason;

        public ComboBrokenEvent(int finalCombo, int peakMultiplier, long comboScore, long bonusScore,
            ComboBreakReason reason)
        {
            FinalCombo = finalCombo;
            PeakMultiplier = peakMultiplier;
            ComboScore = comboScore;
            BonusScore = bonusScore;
            Reason = reason;
        }
    }

    /// <summary>The multiplier crossed a tier, in either direction.</summary>
    internal readonly struct MultiplierChangedEvent
    {
        public readonly int Previous;

        public readonly int Current;

        public MultiplierChangedEvent(int previous, int current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>An enemy went down.</summary>
    internal readonly struct EnemyDefeatedEvent
    {
        /// <summary>The scoring tier it was worth.</summary>
        public readonly EnemyType EnemyType;

        /// <summary>The bounty paid, multiplier included.</summary>
        public readonly long Gained;

        /// <summary>How many have gone down during the combo now running.</summary>
        public readonly int DefeatedInCombo;

        public EnemyDefeatedEvent(EnemyType enemyType, long gained, int defeatedInCombo)
        {
            EnemyType = enemyType;
            Gained = gained;
            DefeatedInCombo = defeatedInCombo;
        }
    }

    /// <summary>
    /// The player was hit. Raised before the combo is docked or settled, and carries both sides so
    /// a subscriber can show the cost without having to read the state back.
    /// </summary>
    internal readonly struct DamageTakenEvent
    {
        /// <summary>The combo as it stood when the blow landed.</summary>
        public readonly int ComboBefore;

        /// <summary>What is left of it: zero when the hit ends the combo.</summary>
        public readonly int ComboAfter;

        /// <summary>The run of unhurt blows the hit threw away.</summary>
        public readonly int StreakLost;

        public DamageTakenEvent(int comboBefore, int comboAfter, int streakLost)
        {
            ComboBefore = comboBefore;
            ComboAfter = comboAfter;
            StreakLost = streakLost;
        }
    }
}
