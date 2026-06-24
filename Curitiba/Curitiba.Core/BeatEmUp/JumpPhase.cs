namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Sub-phase of a hop, used only to pick which animation strip is drawn while the
    /// fighter is in <see cref="FighterState.Jump"/>. The gameplay state stays a single
    /// <c>Jump</c>; the phase is driven by the arc physics (rise/apex/fall) plus two short
    /// grounded windows (anticipation before launch, recovery on landing) so the animation
    /// stays fluid no matter how high or low the jump is tuned.
    /// </summary>
    internal enum JumpPhase
    {
        Start, // crouch / anticipation, on the ground, before the launch impulse
        Rise,  // moving up (vertical speed clearly positive)
        Apex,  // near the top of the arc (vertical speed near zero)
        Fall,  // moving down (vertical speed clearly negative)
        Land,  // landing recovery, on the ground, after touchdown
    }
}
