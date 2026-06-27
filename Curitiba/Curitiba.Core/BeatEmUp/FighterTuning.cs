using System.Collections.Generic;

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
        public int BodyWidth { get; set; } = 40;
        public int BodyHeight { get; set; } = 72;

        /// <summary>Walking speed, px/s.</summary>
        public float MoveSpeed { get; set; } = 175f;

        // Attack timing (seconds). These scalar values stay the single-swing fallback used when
        // no explicit ComboChain is supplied (and for the airborne kick).
        public float AttackWindup { get; set; } = 0.12f;
        public float AttackActive { get; set; } = 0.10f;
        public float AttackRecovery { get; set; } = 0.18f;

        // Combo / input feel.
        /// <summary>How long an attack press is remembered so it still fires when the fighter can act
        /// again (seconds). Keeps chaining responsive — a press during the previous swing isn't lost.</summary>
        public float AttackBufferDuration { get; set; } = 0.15f;

        /// <summary>Ordered swings the fighter chains through. Null/empty falls back to a single swing
        /// built from the scalar <see cref="AttackWindup"/>/<see cref="AttackActive"/>/<see cref="AttackRecovery"/>
        /// timings, so older stage JSON keeps working unchanged.</summary>
        public List<ComboMoveDef> ComboChain { get; set; }

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

        /// <summary>The player's stats. Sofia's string is punch → punch → punch2 → kick (finisher):
        /// snappy startups/recoveries for a fast, fluid feel, and <c>RequiresHitConfirm</c> on every
        /// link so the chain only flows while she is connecting — a whiff leaves her throwing only the
        /// first punch. The cancel point opens right as the active frames end, so a confirmed hit can
        /// cancel into the next swing immediately (Streets of Rage 4 feel).</summary>
        public static FighterTuning SofiaDefaults() => new FighterTuning
        {
            MaxHealth = 100,
            AttackDamage = 10,
            AttackReach = 48,
            BodyWidth = 40,
            BodyHeight = 74,
            MoveSpeed = 175f,
            AttackBufferDuration = 0.15f,
            ComboChain = new List<ComboMoveDef>
            {
                new ComboMoveDef
                {
                    Id = "punch1", State = "Attack",
                    Startup = 0.07f, Active = 0.06f, Recovery = 0.11f,
                    Damage = 10, Reach = 48, KnockbackX = 220f, KnockbackY = -40f,
                    CancelPoint = 0.14f, RequiresHitConfirm = true,
                },
                new ComboMoveDef
                {
                    Id = "punch2", State = "Attack",
                    Startup = 0.07f, Active = 0.06f, Recovery = 0.11f,
                    Damage = 10, Reach = 48, KnockbackX = 220f, KnockbackY = -40f,
                    CancelPoint = 0.14f, RequiresHitConfirm = true,
                },
                new ComboMoveDef
                {
                    Id = "punch3", State = "Attack2",
                    Startup = 0.07f, Active = 0.06f, Recovery = 0.12f,
                    Damage = 12, Reach = 50, KnockbackX = 240f, KnockbackY = -40f,
                    CancelPoint = 0.14f, RequiresHitConfirm = true,
                },
                new ComboMoveDef
                {
                    Id = "kick", State = "Attack3",
                    Startup = 0.10f, Active = 0.08f, Recovery = 0.20f,
                    Damage = 22, Reach = 62, KnockbackX = 380f, KnockbackY = -60f,
                    CancelPoint = 0f, RequiresHitConfirm = true, // finisher: no cancel
                },
            },
        };

        /// <summary>The basic enemy's stats, matching the previous hardcoded values exactly. A single
        /// swing with no cancel (CancelPoint at the swing's end), so enemy behaviour is unchanged.</summary>
        public static FighterTuning PiaLocoDefaults() => new FighterTuning
        {
            MaxHealth = 30,
            AttackDamage = 5,
            AttackReach = 40,
            BodyWidth = 42,
            BodyHeight = 72,
            MoveSpeed = 72f,
            // Short hit-invulnerability (< the player's ~0.14s combo cadence) so each punch in
            // Sofia's chain reconnects and the hit-confirmed combo flows instead of stalling.
            InvulnerabilityOnHit = 0.10f,
            AttackBufferDuration = 0.15f,
            ComboChain = new List<ComboMoveDef>
            {
                new ComboMoveDef
                {
                    Id = "hit", State = "Attack",
                    Startup = 0.12f, Active = 0.10f, Recovery = 0.18f,
                    Damage = 5, Reach = 40, KnockbackX = 220f, KnockbackY = -40f, CancelPoint = 0.40f,
                },
            },
        };
    }

    /// <summary>
    /// Serialisable definition of one combo swing (mirrors the JSON <c>comboChain[]</c> entries).
    /// Resolved into a runtime <see cref="Combat.ComboMove"/> by <see cref="Combat.CombatDefaults.BuildChain"/>.
    /// </summary>
    public sealed class ComboMoveDef
    {
        public string Id { get; set; } = "attack";

        /// <summary>Animation/state name, parsed to a <see cref="FighterState"/> (e.g. "Attack", "Attack2").</summary>
        public string State { get; set; } = "Attack";

        public float Startup { get; set; } = 0.12f;
        public float Active { get; set; } = 0.10f;
        public float Recovery { get; set; } = 0.18f;

        public int Damage { get; set; } = 10;
        public int Reach { get; set; } = 46;

        public float KnockbackX { get; set; } = 220f;
        public float KnockbackY { get; set; } = -40f;

        /// <summary>Seconds into the swing after which a buffered press cancels into the next move.
        /// 0 = no cancel (the swing must finish first).</summary>
        public float CancelPoint { get; set; }

        /// <summary>When true, the chain only advances past this move if the swing connected (hit
        /// confirm). Default false keeps single-swing/enemy chains unaffected.</summary>
        public bool RequiresHitConfirm { get; set; }
    }
}
