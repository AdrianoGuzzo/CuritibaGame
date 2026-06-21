namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// The high level state of a beat 'em up combatant. Drives both the
    /// animation that is played and what the fighter is allowed to do.
    /// </summary>
    internal enum FighterState
    {
        Idle,
        Walk,
        Attack,
        Attack2,
        Jump,
        JumpAttack,
        Hit,
        KnockedDown,
        Dead,
    }
}
