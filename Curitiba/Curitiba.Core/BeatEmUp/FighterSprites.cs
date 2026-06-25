using System.Collections.Generic;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Maps each <see cref="FighterState"/> to the sprite-strip asset name expected
    /// under <c>Content/Sprites/&lt;set&gt;/</c>. The art in <c>Curitiba.Art</c> is
    /// delivered as 64x64 frames, "one row per animation"; once those rows are sliced
    /// into per-animation strips and registered in <c>Curitiba.mgcb</c>, the
    /// <see cref="FighterAnimator"/> picks them up automatically using these names.
    /// </summary>
    internal static class FighterSprites
    {
        public static readonly IReadOnlyDictionary<FighterState, string> Sofia = new Dictionary<FighterState, string>
        {
            { FighterState.Idle, "Idle" },
            { FighterState.Walk, "Walk" },
            { FighterState.Dash, "Dash" },
            { FighterState.Attack, "Attack" },
            { FighterState.Attack2, "Attack2" },
            { FighterState.Attack3, "Kick" },
            { FighterState.Jump, "Jump" },
            { FighterState.JumpAttack, "JumpKick" },
            { FighterState.Hit, "Hit" },
            { FighterState.KnockedDown, "Knockdown" },
            { FighterState.Dead, "Knockdown" },
        };

        /// <summary>
        /// Sofia's per-phase hop strips (under <c>Content/Sprites/Sofia/</c>). The single
        /// <see cref="FighterState.Jump"/> strip above is the fallback; when these phase strips
        /// exist the animator plays them instead, driven by <see cref="JumpPhase"/>, for a far
        /// more fluid jump (crouch → rise → apex → fall → land).
        /// </summary>
        public static readonly IReadOnlyDictionary<JumpPhase, string> SofiaJumpPhases = new Dictionary<JumpPhase, string>
        {
            { JumpPhase.Start, "JumpStart" },
            { JumpPhase.Rise, "JumpUp" },
            { JumpPhase.Apex, "JumpApex" },
            { JumpPhase.Fall, "JumpFall" },
            { JumpPhase.Land, "JumpLand" },
        };

        public static readonly IReadOnlyDictionary<FighterState, string> PiaLoco = new Dictionary<FighterState, string>
        {
            { FighterState.Idle, "Idle" },
            { FighterState.Walk, "Walk" },
            { FighterState.Attack, "Attack" },
            { FighterState.Hit, "Hit" },
            { FighterState.KnockedDown, "Death" },
            { FighterState.Dead, "Death" },
        };
    }
}
