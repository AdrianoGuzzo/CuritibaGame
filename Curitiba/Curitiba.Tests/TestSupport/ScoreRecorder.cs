using System.Collections.Generic;
using Curitiba.Core.BeatEmUp;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// Subscribes to every <see cref="ScoreSystem"/> event and records what came through, so the
    /// notifications a HUD will one day live off can be asserted without a HUD.
    /// </summary>
    /// <remarks>
    /// <see cref="Events"/> is the reason this exists: the order the notifications fire in is part
    /// of the contract — state before money, the cause before its consequence — and comparing a
    /// list of names says so far more plainly than counting seven separate lists.
    /// </remarks>
    internal sealed class ScoreRecorder
    {
        private readonly List<string> events = new List<string>();
        private readonly List<ScoreChangedEvent> scoreChanges = new List<ScoreChangedEvent>();
        private readonly List<ComboChangedEvent> comboStarts = new List<ComboChangedEvent>();
        private readonly List<ComboChangedEvent> comboChanges = new List<ComboChangedEvent>();
        private readonly List<ComboBrokenEvent> comboBreaks = new List<ComboBrokenEvent>();
        private readonly List<MultiplierChangedEvent> multiplierChanges = new List<MultiplierChangedEvent>();
        private readonly List<EnemyDefeatedEvent> defeats = new List<EnemyDefeatedEvent>();
        private readonly List<DamageTakenEvent> damages = new List<DamageTakenEvent>();

        public ScoreRecorder(ScoreSystem score)
        {
            score.OnScoreChanged += e =>
            {
                scoreChanges.Add(e);
                events.Add(nameof(ScoreChanged));
            };
            score.OnComboStarted += e =>
            {
                comboStarts.Add(e);
                events.Add(nameof(ComboStarted));
            };
            score.OnComboChanged += e =>
            {
                comboChanges.Add(e);
                events.Add(nameof(ComboChanged));
            };
            score.OnComboBroken += e =>
            {
                comboBreaks.Add(e);
                events.Add(nameof(ComboBroken));
            };
            score.OnMultiplierChanged += e =>
            {
                multiplierChanges.Add(e);
                events.Add(nameof(MultiplierChanged));
            };
            score.OnEnemyDefeated += e =>
            {
                defeats.Add(e);
                events.Add(nameof(EnemyDefeated));
            };
            score.OnDamageTaken += e =>
            {
                damages.Add(e);
                events.Add(nameof(DamageTaken));
            };
        }

        /// <summary>Every notification's name, in the order it fired.</summary>
        public IReadOnlyList<string> Events => events;

        public IReadOnlyList<ScoreChangedEvent> ScoreChanged => scoreChanges;

        public IReadOnlyList<ComboChangedEvent> ComboStarted => comboStarts;

        public IReadOnlyList<ComboChangedEvent> ComboChanged => comboChanges;

        public IReadOnlyList<ComboBrokenEvent> ComboBroken => comboBreaks;

        public IReadOnlyList<MultiplierChangedEvent> MultiplierChanged => multiplierChanges;

        public IReadOnlyList<EnemyDefeatedEvent> EnemyDefeated => defeats;

        public IReadOnlyList<DamageTakenEvent> DamageTaken => damages;

        /// <summary>Forgets everything recorded so far, to assert on one action in isolation.</summary>
        public void Clear()
        {
            events.Clear();
            scoreChanges.Clear();
            comboStarts.Clear();
            comboChanges.Clear();
            comboBreaks.Clear();
            multiplierChanges.Clear();
            defeats.Clear();
            damages.Clear();
        }
    }
}
