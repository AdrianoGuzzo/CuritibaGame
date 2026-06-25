namespace Curitiba.Core.BeatEmUp.Combat
{
    /// <summary>
    /// An ordered sequence of <see cref="ComboMove"/>s a fighter throws when the attack button is
    /// chained, plus the <see cref="ChainResetWindow"/>: how long after a swing ends the next press
    /// still continues the chain before it restarts from the first move. Built once from
    /// <see cref="FighterTuning"/> (see <see cref="CombatDefaults.BuildChain"/>); replaces the old
    /// hardcoded two-punch alternation.
    /// </summary>
    internal sealed class ComboChainDef
    {
        private readonly ComboMove[] moves;

        /// <summary>Seconds the chain stays "open" after a swing ends before it resets to move 0.</summary>
        public float ChainResetWindow { get; }

        public ComboChainDef(ComboMove[] moves, float chainResetWindow)
        {
            this.moves = moves;
            ChainResetWindow = chainResetWindow;
        }

        public int Count => moves.Length;

        public ComboMove this[int index] => moves[index];
    }
}
