namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Data-driven combat stats and timings for a single <see cref="Fighter"/> archetype.
    /// Loaded from the stage JSON and applied via <see cref="Fighter.ApplyTuning"/>; the
    /// <see cref="SofiaDefaults"/> / <see cref="PiaLocoDefaults"/> reproduce the values that
    /// used to live as constants in code, so a stage with no tuning behaves exactly as before.
    /// </summary>
    public sealed class FighterTuning
    {
        // Body / health.
        public int MaxHealth { get; set; } = 100;
        public int AttackDamage { get; set; } = 10;
        public int AttackReach { get; set; } = 46;

        // Combo finisher (Sofia's kick). Stronger than the punches; only the Attack3 swing uses these.
        public int KickDamage { get; set; } = 22;
        public int KickReach { get; set; } = 62;
        public float KickKnockback { get; set; } = 380f;
        public int BodyWidth { get; set; } = 40;
        public int BodyHeight { get; set; } = 72;

        /// <summary>Walking speed, px/s.</summary>
        public float MoveSpeed { get; set; } = 175f;

        // Attack timing (seconds).
        public float AttackWindup { get; set; } = 0.12f;
        public float AttackActive { get; set; } = 0.10f;
        public float AttackRecovery { get; set; } = 0.18f;

        // Reaction timing.
        public float HitDuration { get; set; } = 0.30f;
        public float DeathDuration { get; set; } = 0.70f;
        public float InvulnerabilityOnHit { get; set; } = 0.25f;

        // Poise / knockdown.
        public float PoiseResetWindow { get; set; } = 1.5f;
        public float KnockdownDuration { get; set; } = 0.9f;
        public float GetUpInvulnerability { get; set; } = 0.4f;

        // Jump / dash.
        public float JumpImpulse { get; set; } = 430f;
        public float JumpGravity { get; set; } = 1500f;
        public float PlanarJumpSpeed { get; set; } = 150f;
        // Phase-animation timing for the hop. The anticipation (crouch) and landing windows
        // are short, fixed, on the ground; ApexThreshold is the |vertical speed| under which
        // the arc is drawn as its floaty top. These keep the phased animation in sync with the
        // arc no matter how JumpImpulse/JumpGravity are tuned, so changing jump height/strength
        // never desyncs the sprite.
        public float JumpWindup { get; set; } = 0.10f;        // crouch before launch, seconds
        public float JumpLandRecovery { get; set; } = 0.16f;  // landing recovery, seconds
        public float JumpApexThreshold { get; set; } = 90f;   // |vert. speed| below this = apex, px/s
        public float DashSpeed { get; set; } = 1050f;
        public float DashDuration { get; set; } = 0.40f;
        public float DashInvulnerability { get; set; } = 0.16f;

        /// <summary>The player's stats, matching the previous hardcoded values exactly.</summary>
        public static FighterTuning SofiaDefaults() => new FighterTuning
        {
            MaxHealth = 100,
            AttackDamage = 10,
            AttackReach = 48,
            KickDamage = 22,
            KickReach = 62,
            KickKnockback = 380f,
            BodyWidth = 40,
            BodyHeight = 74,
            MoveSpeed = 175f,
        };

        /// <summary>The basic enemy's stats, matching the previous hardcoded values exactly.</summary>
        public static FighterTuning PiaLocoDefaults() => new FighterTuning
        {
            MaxHealth = 30,
            AttackDamage = 5,
            AttackReach = 40,
            BodyWidth = 42,
            BodyHeight = 72,
            MoveSpeed = 72f,
        };
    }
}
