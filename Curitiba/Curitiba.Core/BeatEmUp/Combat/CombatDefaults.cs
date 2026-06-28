using System;
using Microsoft.Xna.Framework;

namespace Curitiba.Core.BeatEmUp.Combat
{
    /// <summary>
    /// Turns the data-driven <see cref="FighterTuning"/> into the runtime <see cref="ComboChainDef"/>
    /// a fighter uses. When the tuning lists an explicit <see cref="FighterTuning.ComboChain"/> it is
    /// mapped move-for-move (clamping each <see cref="ComboMove.CancelPoint"/> into its legal range);
    /// otherwise a single move is synthesised from the legacy scalar timings so a stage/JSON that
    /// predates combo chains still behaves exactly as before.
    /// </summary>
    internal static class CombatDefaults
    {
        public static ComboChainDef BuildChain(FighterTuning t)
        {
            if (t.ComboChain != null && t.ComboChain.Count > 0)
            {
                var moves = new ComboMove[t.ComboChain.Count];
                for (int i = 0; i < moves.Length; i++)
                    moves[i] = FromDef(t.ComboChain[i]);
                return new ComboChainDef(moves);
            }

            var single = new ComboMove("attack", FighterState.Attack,
                t.AttackWindup, t.AttackActive, t.AttackRecovery,
                t.AttackDamage, t.AttackReach, 220f, -40f,
                t.AttackWindup + t.AttackActive + t.AttackRecovery,
                false,
                false);
            return new ComboChainDef(new[] { single });
        }

        private static ComboMove FromDef(ComboMoveDef d)
        {
            FighterState state = Enum.TryParse(d.State, out FighterState parsed) ? parsed : FighterState.Attack;
            float total = d.Startup + d.Active + d.Recovery;

            float cancel = d.CancelPoint <= 0f
                ? total
                : MathHelper.Clamp(d.CancelPoint, d.Startup + d.Active, total);

            return new ComboMove(d.Id, state, d.Startup, d.Active, d.Recovery,
                d.Damage, d.Reach, d.KnockbackX, d.KnockbackY, cancel, d.RequiresHitConfirm, d.Launches);
        }
    }
}
