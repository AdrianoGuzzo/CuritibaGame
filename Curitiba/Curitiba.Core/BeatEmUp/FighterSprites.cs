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
            { FighterState.Attack, "Attack" },
            { FighterState.Attack2, "Attack2" },
            { FighterState.Jump, "Jump" },
            { FighterState.JumpAttack, "JumpKick" },
            { FighterState.Hit, "Hit" },
            { FighterState.KnockedDown, "Knockdown" },
            { FighterState.Dead, "Knockdown" },
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
