using System;
using System.Numerics;
using Microsoft.Xna.Framework;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Keeps the score: the combo and its multiplier, what each blow and each defeated enemy is
    /// worth, and the bonuses that reward fighting variedly and without getting hit.
    /// </summary>
    /// <remarks>
    /// A plain rule over a float clock, with no fighter, no arena and nothing drawable attached —
    /// the same split as <see cref="Audio.ArenaMusicPolicy"/>. Callers tell it what happened and it
    /// answers with numbers; it never reaches back into the fight, and it holds no reference to
    /// anything in it.
    /// <para>
    /// Wiring it up is three calls from the side that already detects each event, plus a tick:
    /// </para>
    /// <code>
    /// // once, alongside the arena it belongs to
    /// private readonly ScoreSystem score = new ScoreSystem();
    ///
    /// // in CapaoRasoArena.ResolveCombat, where a blow is confirmed against an enemy
    /// sofia.AttackHitTargets.Add(enemy);
    /// score.RegisterHit(attack.Launches ? AttackType.Finisher
    ///                 : sofia.State == FighterState.JumpAttack ? AttackType.Air
    ///                 : AttackType.Normal);
    /// if (enemy.IsDefeated)
    ///     score.RegisterEnemyDefeated(EnemyType.Normal);
    ///
    /// // where the player is struck
    /// sofia.TakeDamage(attack.Damage, attack.Knockback);
    /// score.RegisterPlayerDamage();
    ///
    /// // once a frame, after combat has been resolved, so a blow this frame can still save the
    /// // combo the window would otherwise have ended
    /// score.Update(dt);
    ///
    /// // when the run is over, so the last combo is paid before the screen hands over
    /// score.EndCombo();
    /// </code>
    /// <para>
    /// Reading it back is <see cref="TotalScore"/>, <see cref="CurrentCombo"/> and
    /// <see cref="CurrentMultiplier"/> (or the <c>GetCurrent*</c> methods), and the events below
    /// exist so a HUD can react to a change instead of polling for one.
    /// </para>
    /// </remarks>
    internal sealed class ScoreSystem
    {
        private readonly ScoreRules rules;

        /// <summary>
        /// Which attack types this combo has used, one bit per <see cref="AttackType"/>. A bitmask
        /// rather than a set: the variety bonus is read on a combat frame and must not allocate.
        /// </summary>
        private int usedTypes;

        /// <summary>
        /// The next mark of the "no damage taken" ladder that can still be earned. Walking an index
        /// rather than comparing counts is what makes each mark pay exactly once.
        /// </summary>
        private int nextMilestone;

        /// <param name="config">The balance sheet. Null falls back to <see cref="ScoreConfig.Defaults"/>.</param>
        public ScoreSystem(ScoreConfig config = null)
        {
            rules = ScoreDefaults.Build(config);
        }

        /// <summary>Points were banked, for whatever reason.</summary>
        public event Action<ScoreChangedEvent> OnScoreChanged;

        /// <summary>A combo began. Raised in addition to <see cref="OnComboChanged"/>, not instead.</summary>
        public event Action<ComboChangedEvent> OnComboStarted;

        /// <summary>The combo count moved, up or down.</summary>
        public event Action<ComboChangedEvent> OnComboChanged;

        /// <summary>A combo ended and was settled.</summary>
        public event Action<ComboBrokenEvent> OnComboBroken;

        /// <summary>The multiplier crossed a tier. Never raised for a value that did not change.</summary>
        public event Action<MultiplierChangedEvent> OnMultiplierChanged;

        /// <summary>An enemy went down.</summary>
        public event Action<EnemyDefeatedEvent> OnEnemyDefeated;

        /// <summary>The player was hit.</summary>
        public event Action<DamageTakenEvent> OnDamageTaken;

        /// <summary>Points banked so far. Survives every combo break; only <see cref="Reset"/> clears it.</summary>
        public long TotalScore { get; private set; }

        /// <summary>Blows landed in the combo currently running.</summary>
        public int CurrentCombo { get; private set; }

        /// <summary>
        /// Points the combo currently running has earned from blows and bounties. Bonuses are not
        /// in here — they are a percentage of it, or a flat payout on top.
        /// </summary>
        public long CurrentComboScore { get; private set; }

        /// <summary>Blows landed since the player last took damage.</summary>
        public int HitsWithoutDamage { get; private set; }

        /// <summary>Enemies put down during the combo currently running.</summary>
        public int EnemiesDefeatedInCombo { get; private set; }

        /// <summary>How many times the player has been hit this run.</summary>
        public int DamageTakenCount { get; private set; }

        /// <summary>Seconds left before the combo lapses.</summary>
        public float ComboTimeRemaining { get; private set; }

        /// <summary>The multiplier the combo currently earns. Never below 1.</summary>
        public int CurrentMultiplier { get; private set; } = 1;

        /// <summary>The longest combo of this run.</summary>
        public int HighestCombo { get; private set; }

        /// <summary>The best multiplier of this run. Starts at 1: every run has reached x1.</summary>
        public int HighestMultiplier { get; private set; } = 1;

        /// <summary>Whether a combo is running.</summary>
        public bool IsComboActive => CurrentCombo > 0;

        /// <summary>
        /// The percentage the current combo will be paid for the variety of blows in it, settled
        /// when the combo ends. Taking damage forfeits it.
        /// </summary>
        public int VarietyBonusPercent => rules.VarietyPercentFor(DistinctTypesUsed);

        private int DistinctTypesUsed => BitOperations.PopCount((uint)usedTypes);

        /// <summary>Books a blow that connected: grows the combo, refreshes its window, pays for it.</summary>
        public void RegisterHit(AttackType attackType)
        {
            AttackType type = Known(attackType);
            bool starting = CurrentCombo == 0;

            usedTypes |= 1 << (int)type;
            ComboTimeRemaining = rules.ComboDuration;
            HitsWithoutDamage++;

            // The combo grows before the blow is priced, so the hit that reaches a tier already
            // earns the new multiplier — and so a subscriber sees the multiplier it was paid at.
            SetCombo(CurrentCombo + 1, starting);
            AddScore((long)rules.PointsFor(type) * CurrentMultiplier, ScoreReason.Hit);
            PayNoDamageMilestones();
        }

        /// <summary>
        /// Books an enemy put down. The blow that killed it has already been registered, so this
        /// only pays the bounty and counts the body — it neither grows the combo nor extends it.
        /// </summary>
        public void RegisterEnemyDefeated(EnemyType enemyType)
        {
            EnemyType tier = Known(enemyType);

            if (CurrentCombo > 0)
                EnemiesDefeatedInCombo++;

            long gained = (long)rules.BountyFor(tier) * CurrentMultiplier;
            AddScore(gained, ScoreReason.EnemyDefeated);
            OnEnemyDefeated?.Invoke(new EnemyDefeatedEvent(tier, gained, EnemiesDefeatedInCombo));
        }

        /// <summary>
        /// Books a hit taken. Always ends the run of unhurt blows and forfeits the variety share;
        /// then either breaks the combo or docks it, per <see cref="ScoreConfig.ResetComboOnDamage"/>.
        /// </summary>
        public void RegisterPlayerDamage()
        {
            DamageTakenCount++;

            int streakLost = HitsWithoutDamage;
            HitsWithoutDamage = 0;
            nextMilestone = 0;

            // Forgotten before the combo is settled, so a sequence cut short by damage is paid
            // nothing for having been varied.
            usedTypes = 0;

            int comboBefore = CurrentCombo;
            int comboAfter = comboBefore == 0 || rules.ResetComboOnDamage
                ? 0
                : Math.Max(0, comboBefore - rules.ComboPenaltyOnDamage);

            OnDamageTaken?.Invoke(new DamageTakenEvent(comboBefore, comboAfter, streakLost));

            if (comboBefore == 0)
                return;

            if (comboAfter == 0)
            {
                BreakCombo(ComboBreakReason.Damage);
                return;
            }

            // The window is deliberately left alone: being hit must not extend a combo.
            SetCombo(comboAfter, false);
        }

        /// <summary>
        /// Settles the combo running now, paying what it earned. Idempotent: with no combo running
        /// it does nothing, so it is safe to call on a stage ending or a player being defeated
        /// without knowing whether the window already lapsed.
        /// </summary>
        public void EndCombo()
        {
            BreakCombo(ComboBreakReason.Manual);
        }

        /// <summary>
        /// Starts a fresh run: clears the banked score and the peaks along with the combo, pays
        /// nothing for a combo still running, and notifies nobody. Settling and carrying on is
        /// <see cref="EndCombo"/>.
        /// </summary>
        public void Reset()
        {
            TotalScore = 0;
            CurrentCombo = 0;
            CurrentComboScore = 0;
            HitsWithoutDamage = 0;
            EnemiesDefeatedInCombo = 0;
            DamageTakenCount = 0;
            ComboTimeRemaining = 0f;
            CurrentMultiplier = 1;
            HighestCombo = 0;
            HighestMultiplier = 1;
            usedTypes = 0;
            nextMilestone = 0;
        }

        /// <summary>Ages the combo window, breaking the combo on the frame it runs out.</summary>
        public void Update(float elapsedSeconds)
        {
            if (CurrentCombo == 0)
                return;

            // A frozen or rewound clock must not move the window in either direction.
            if (elapsedSeconds > 0f)
                ComboTimeRemaining -= elapsedSeconds;

            if (ComboTimeRemaining <= 0f)
            {
                ComboTimeRemaining = 0f;
                BreakCombo(ComboBreakReason.Timeout);
            }
        }

        /// <summary>
        /// Convenience for callers holding a <see cref="GameTime"/> — the arena and the screens do.
        /// A null one is a no-op, like the rest of the frame-by-frame policies in the game.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            if (gameTime == null)
                return;

            Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        /// <inheritdoc cref="CurrentMultiplier"/>
        public int GetCurrentMultiplier() => CurrentMultiplier;

        /// <inheritdoc cref="CurrentCombo"/>
        public int GetCurrentCombo() => CurrentCombo;

        /// <inheritdoc cref="TotalScore"/>
        public long GetCurrentScore() => TotalScore;

        /// <summary>Out-of-range values are data, not a crash: they score like a normal blow.</summary>
        private static AttackType Known(AttackType attackType)
        {
            switch (attackType)
            {
                case AttackType.Heavy:
                case AttackType.Air:
                case AttackType.Finisher:
                    return attackType;
                default:
                    return AttackType.Normal;
            }
        }

        /// <summary>Out-of-range values are data, not a crash: they score like a common mook.</summary>
        private static EnemyType Known(EnemyType enemyType)
        {
            switch (enemyType)
            {
                case EnemyType.Strong:
                case EnemyType.Boss:
                    return enemyType;
                default:
                    return EnemyType.Normal;
            }
        }

        /// <summary>
        /// The one place a running combo's count changes, so the multiplier can never drift out of
        /// step with it and the notifications always come out in the same order. Only ever called
        /// with a live count (one or more) — clearing a combo is <see cref="BreakCombo"/>'s job.
        /// </summary>
        private void SetCombo(int combo, bool starting)
        {
            int previousMultiplier = CurrentMultiplier;

            CurrentCombo = combo;
            CurrentMultiplier = rules.MultiplierFor(CurrentCombo);

            // Banked as it happens, not when the combo settles: a run can end without ever being
            // settled — the player is defeated and the screen hands over — and a HUD wants the
            // peak live rather than one combo late.
            if (CurrentCombo > HighestCombo)
                HighestCombo = CurrentCombo;

            if (CurrentMultiplier > HighestMultiplier)
                HighestMultiplier = CurrentMultiplier;

            var changed = new ComboChangedEvent(CurrentCombo, CurrentMultiplier);
            if (starting)
                OnComboStarted?.Invoke(changed);

            OnComboChanged?.Invoke(changed);
            RaiseMultiplierChanged(previousMultiplier);
        }

        /// <summary>Pays every mark of the streak ladder the current run of unhurt blows has passed.</summary>
        private void PayNoDamageMilestones()
        {
            while (nextMilestone < rules.MilestoneCount
                   && HitsWithoutDamage >= rules.MilestoneHits(nextMilestone))
            {
                AddScore(rules.MilestoneBonus(nextMilestone), ScoreReason.NoDamageBonus);
                nextMilestone++;
            }
        }

        /// <summary>
        /// Settles the combo: pays the variety share on what it earned and the payout for how long
        /// it got, then clears everything that belonged to it. A no-op with no combo running, so
        /// settling twice cannot pay twice — whether the window lapsed, the player was hit or a
        /// caller settled it by hand.
        /// </summary>
        private void BreakCombo(ComboBreakReason reason)
        {
            if (CurrentCombo == 0)
                return;

            int finalCombo = CurrentCombo;
            int peakMultiplier = CurrentMultiplier;
            long earned = CurrentComboScore;

            // Both read before anything is cleared, and reported together so a HUD can show the
            // settlement as one figure.
            long variety = VarietyShare();
            long tier = rules.EndBonusFor(finalCombo);
            AddScore(variety, ScoreReason.VarietyBonus);
            AddScore(tier, ScoreReason.ComboEndBonus);

            CurrentCombo = 0;
            ComboTimeRemaining = 0f;
            CurrentComboScore = 0;
            EnemiesDefeatedInCombo = 0;
            usedTypes = 0;

            OnComboBroken?.Invoke(
                new ComboBrokenEvent(finalCombo, peakMultiplier, earned, variety + tier, reason));

            int previousMultiplier = CurrentMultiplier;
            CurrentMultiplier = 1;
            RaiseMultiplierChanged(previousMultiplier);
        }

        /// <summary>
        /// Notifies only on a real transition. A HUD gets the news, not a value rewritten on every
        /// blow of a long combo.
        /// </summary>
        private void RaiseMultiplierChanged(int previousMultiplier)
        {
            if (CurrentMultiplier != previousMultiplier)
                OnMultiplierChanged?.Invoke(new MultiplierChangedEvent(previousMultiplier, CurrentMultiplier));
        }

        /// <summary>
        /// The variety share of what the combo earned. Multiplies first while that cannot overflow
        /// and divides first once it could, so the arithmetic stays exact for every real score and
        /// safe for an absurd one.
        /// </summary>
        private long VarietyShare()
        {
            int percent = VarietyBonusPercent;
            if (percent <= 0)
                return 0;

            return CurrentComboScore <= long.MaxValue / 100
                ? CurrentComboScore * percent / 100
                : CurrentComboScore / 100 * percent;
        }

        /// <summary>
        /// Banks points. Saturates instead of overflowing: a number is never a reason for the game
        /// to fall over, however absurd the configured values are.
        /// </summary>
        private void AddScore(long gained, ScoreReason reason)
        {
            if (gained <= 0)
                return;

            TotalScore = TotalScore > long.MaxValue - gained ? long.MaxValue : TotalScore + gained;

            // Only what the sequence earned counts towards its own ledger, so the variety share is
            // never a percentage of another bonus.
            if (reason == ScoreReason.Hit || reason == ScoreReason.EnemyDefeated)
            {
                CurrentComboScore = CurrentComboScore > long.MaxValue - gained
                    ? long.MaxValue
                    : CurrentComboScore + gained;
            }

            OnScoreChanged?.Invoke(new ScoreChangedEvent(TotalScore, gained, reason));
        }
    }
}
