namespace Curitiba.Core.BeatEmUp.Combat
{
    /// <summary>
    /// One swing in a combo chain, resolved from data once at tuning time. Holds the frame
    /// timing (startup → active → recovery), the hitbox stats and the <see cref="CancelPoint"/>:
    /// the instant into the swing (seconds from its start) after which a buffered attack press
    /// cancels the recovery into the next move of the chain. A <see cref="ComboMove"/> is a
    /// shared reference held by <see cref="ComboChainDef"/> and never copied per frame.
    /// </summary>
    internal sealed class ComboMove
    {
        public string Id { get; }

        /// <summary>Which animation/state this swing plays (e.g. Attack or Attack2).</summary>
        public FighterState State { get; }

        public float Startup { get; }
        public float Active { get; }
        public float Recovery { get; }

        public int Damage { get; }
        public int Reach { get; }

        public float KnockbackX { get; }
        public float KnockbackY { get; }

        /// <summary>
        /// Seconds from the swing's start after which a buffered attack cancels the recovery into
        /// the next move. Clamped to [Startup+Active, TotalDuration] when the chain is built, so a
        /// cancel can never cut the active frames short. Equal to <see cref="TotalDuration"/> means
        /// "no cancel" (the swing must finish before the next one starts).
        /// </summary>
        public float CancelPoint { get; }

        /// <summary>
        /// When true, the chain only advances past this move if the swing actually connected (hit
        /// confirm). A whiff leaves the fighter able to throw only the first move again — so Sofia's
        /// punch→punch→punch2→kick string only flows while she is landing blows.
        /// </summary>
        public bool RequiresHitConfirm { get; }

        public ComboMove(string id, FighterState state, float startup, float active, float recovery,
                         int damage, int reach, float knockbackX, float knockbackY, float cancelPoint,
                         bool requiresHitConfirm)
        {
            Id = id;
            State = state;
            Startup = startup;
            Active = active;
            Recovery = recovery;
            Damage = damage;
            Reach = reach;
            KnockbackX = knockbackX;
            KnockbackY = knockbackY;
            CancelPoint = cancelPoint;
            RequiresHitConfirm = requiresHitConfirm;
        }

        public float TotalDuration => Startup + Active + Recovery;
    }
}
