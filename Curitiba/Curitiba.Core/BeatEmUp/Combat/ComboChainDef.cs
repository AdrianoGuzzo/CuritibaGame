namespace Curitiba.Core.BeatEmUp.Combat
{
    /// <summary>
    /// An ordered sequence of <see cref="ComboMove"/>s a fighter throws when the attack button is
    /// chained. Built once from <see cref="FighterTuning"/> (see <see cref="CombatDefaults.BuildChain"/>);
    /// replaces the old hardcoded two-punch alternation. The chain is driven purely by the in-swing
    /// buffered cancel (see <see cref="Fighter.UpdateAttack"/>): a swing that returns to idle drops
    /// the combo, so the next press opens at the first move again.
    /// </summary>
    internal sealed class ComboChainDef
    {
        private readonly ComboMove[] moves;

        public ComboChainDef(ComboMove[] moves)
        {
            this.moves = moves;
        }

        public int Count => moves.Length;

        public ComboMove this[int index] => moves[index];
    }
}
