using System;
using System.Collections.Generic;
using Curitiba.Core.BeatEmUp.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Shared behaviour of every beat 'em up combatant: a small state machine
    /// (idle/walk/attack/hit/knockdown/dead), a body <see cref="HurtBox"/>, and a
    /// transient attack <see cref="CurrentAttack"/> that only exists during the
    /// active frames of a swing. Subclasses add input (player) or AI (enemy).
    /// </summary>
    internal abstract class Fighter
    {
        /// <summary>World position of the fighter's feet (bottom-centre of the sprite).</summary>
        public Vector2 Position;

        public FaceDirection Facing = FaceDirection.Right;

        /// <summary>
        /// Visual elevation (px) of the ground the feet stand on, above the asphalt floor.
        /// 0 on the road; <c>CurbHeight</c> on the raised sidewalk. Subtracted only when
        /// drawing (sprite + shadow) so depth-sorting, clamps and the hurtbox stay on
        /// <see cref="Position"/>. The arena drives it each frame via <see cref="SetGroundTarget"/>
        /// (see ApplyCurb); 0 = no step. On the ground it snaps to the target (a crisp curb when
        /// walking); while airborne it ramps smoothly from the take-off floor to the landing floor
        /// so a jump across the curb arcs without a step jolt.
        /// </summary>
        public float GroundOffset;

        public int Health { get; protected set; }
        public int MaxHealth { get; protected set; }
        public FighterState State { get; protected set; } = FighterState.Idle;

        /// <summary>
        /// Optional HUD portrait (head-shot) for this fighter, shown next to the health bar.
        /// Null when the set has no portrait registered; the HUD then falls back to a bare bar.
        /// Loaded by convention from <c>Sprites/Portraits/&lt;set&gt;</c> via <see cref="LoadPortrait"/>.
        /// </summary>
        public Texture2D Portrait { get; protected set; }

        /// <summary>Display name shown on the HUD (e.g. above the health bar). Null = unnamed.</summary>
        public string Name { get; protected set; }

        /// <summary>Collision body, in world space, used both as a hurtbox and for clamping.</summary>
        public int BodyWidth = 40;
        public int BodyHeight = 72;

        /// <summary>Walking speed (px/s). Set from <see cref="FighterTuning"/> via <see cref="ApplyTuning"/>.</summary>
        protected float moveSpeed = 175f;

        // Attack timing (seconds). These scalars remain the fallback for the airborne kick
        // (UpdateJumpAttack) and feed the single-swing chain when no ComboChain is supplied.
        protected float attackWindup = 0.12f;
        protected float attackActive = 0.10f;
        protected float attackRecovery = 0.18f;
        protected int attackDamage;
        protected int attackReach = 46;

        // Combo / input buffering. The buffer remembers an attack press for a short window so a
        // press during the previous swing still fires the moment a move can start (no dropped input);
        // the chain advances through comboChain, and a buffered press past a move's CancelPoint
        // cancels its recovery straight into the next move (the snappy, chainable feel).
        private InputBuffer inputBuffer;
        protected float attackBufferDuration = 0.15f;
        private ComboChainDef comboChain;
        private ComboMove currentMove;
        private int comboIndex;

        // Reaction timing.
        protected float hitDuration = 0.30f;
        protected float deathDuration = 0.70f;
        protected float deathBlinkDuration = 0.6f;
        protected float getUpDuration = 0.34f;
        protected float invulnerabilityOnHit = 0.25f;

        // Meio-ciclo do piscar do corpo morto, em segundos (alterna visível/invisível por este intervalo).
        private const float DeathBlinkInterval = 0.07f;

        // Tremor visual da sprite enquanto o fighter está em Hit (apenas render; não afeta colisão).
        private const float HitShakeAmplitude = 4f;   // deslocamento máx. em px do mundo virtual
        private const float HitShakeFrequency = 46f;  // rad/s — várias oscilações ao longo de hitDuration

        // Poise / knockdown. A normal blow staggers in place; only once a fighter has
        // absorbed hitsToKnockdown blows in quick succession does it get knocked down.
        // 0 disables poise knockdown (the player only falls when actually defeated).
        protected int hitsToKnockdown = 0;
        protected float poiseResetWindow = 1.5f;   // no hit for this long resets the streak
        protected float knockdownDuration = 0.9f;  // time spent down before trying to rise
        protected float getUpInvulnerability = 0.4f;

        // Jump (a purely visual vertical hop). Position stays the feet on the ground so
        // depth-sorting, corridor/screen clamps and the hurtbox are unaffected; only the
        // sprite is drawn jumpHeight pixels higher, with a shadow left on the ground.
        protected float jumpImpulse = 430f;   // initial upward speed, px/s
        protected float jumpGravity = 1500f;  // downward acceleration, px/s^2
        protected float planarJumpSpeed = 150f;     // ground speed of a directional jump (any of the 8 dirs)
        protected float jumpHeight;                 // current visual height above the ground (>=0)
        private float jumpVerticalSpeed;            // vertical speed of the arc, px/s upward
        private Vector2 jumpPlanarVelocity;         // locked ground velocity (X horizontal + Y depth) for the arc
        private float jumpStartOffset;              // GroundOffset (take-off floor) captured at launch
        private float jumpPeak;                     // highest jumpHeight reached this jump (drives the descent ramp)
        private bool curbDropActive;                // this "jump" is a curb step-down (skips the land recovery)

        // Phased hop animation. The visible jump runs through five phases (see <see cref="JumpPhase"/>):
        // a short grounded crouch (windup) before the launch, the arc itself drawn rise/apex/fall by the
        // sign+magnitude of jumpVerticalSpeed, and a short grounded recovery on landing. The two grounded
        // windows are fixed-length (height-independent) and the arc phases follow the physics, so the
        // animation never desyncs when the jump strength is retuned. Gameplay stays a single Jump state.
        protected float jumpWindup = 0.10f;         // crouch on the ground before the launch impulse, s
        protected float jumpLandRecovery = 0.16f;   // recovery on the ground after touchdown, s
        protected float jumpApexThreshold = 90f;    // |vertical speed| under which the arc reads as apex
        private float jumpPhaseTimer;               // time spent in the current grounded jump window (Start/Land)

        /// <summary>Sub-phase of the current hop; only meaningful while <see cref="State"/> is Jump.</summary>
        public JumpPhase CurrentJumpPhase { get; private set; }

        // Dash: a short, committed burst in one of the eight directions. Far faster than a walk,
        // it grants a sliver of invulnerability at the start so it doubles as a dodge. The fighter
        // is locked out of other actions until it ends (Dash is not a CanAct state).
        protected float dashSpeed = 1050f;          // peak launch speed, px/s (eases out to 0 over the dash)
        protected float dashDuration = 0.40f;       // how long the burst lasts
        protected float dashInvulnerability = 0.16f; // i-frames at the start of the dash
        private Vector2 dashVelocity;               // locked burst velocity for the dash

        // Throw: the finisher kick flings a fighter backward into the Thrown flight. The launch
        // velocity is held and eased to rest over throwDuration (slower than the default momentum
        // bleed, so the body actually travels), then it lands — knocked down, or dead at zero health.
        // While flying it can bowl into other fighters (the arena resolves that collision).
        protected float throwDuration = 0.5f;       // how long the thrown flight lasts before landing
        private Vector2 thrownVelocity;             // locked launch velocity, eased out over the flight

        protected FighterAnimator animator;
        protected Vector2 velocity;

        // Scripted section entrance (the "Door" entry): walk to entryWalkTargetX with no player/AI
        // control, then hand control back. inEntryWalk also forces CanAct false meanwhile.
        private bool inEntryWalk;
        private float entryWalkTargetX;

        private float stateTimer;
        private float invulnTimer;
        private int poiseHits;
        private float poiseResetTimer;
        private readonly HashSet<Fighter> attackHitTargets = new HashSet<Fighter>();

        /// <summary>Targets already struck by the current swing (so each blow lands once).</summary>
        public HashSet<Fighter> AttackHitTargets => attackHitTargets;

        /// <summary>Active damaging hitbox, present only during an attack's active frames.</summary>
        public AttackData? CurrentAttack { get; private set; }

        /// <summary>True while in a jump arc; the curb may be crossed freely while airborne.</summary>
        public bool IsAirborne => State == FighterState.Jump || State == FighterState.JumpAttack;

        /// <summary>
        /// Whether this fighter must jump to climb the curb (cannot walk up the step).
        /// Base fighters (enemies) climb freely; the player overrides this to require a hop.
        /// </summary>
        public virtual bool MustJumpCurb => false;

        /// <summary>
        /// Whether stepping down off the curb plays the hop's fall animation as a small drop
        /// (see <see cref="TryStartCurbDrop"/>) instead of snapping the elevation. Only the player
        /// overrides this to true (enemies have no Jump/fall strip and climb freely).
        /// </summary>
        protected virtual bool AnimatesCurbDrop => false;

        public bool IsAlive => State != FighterState.Dead && State != FighterState.KnockedDown
                               && State != FighterState.Thrown;
        public bool IsDefeated => Health <= 0;
        public bool IsInvulnerable => invulnTimer > 0f;

        /// <summary>True while the fighter is mid-flight from a launch (so the arena can bowl it into others).</summary>
        public bool IsBeingThrown => State == FighterState.Thrown;

        /// <summary>Sign (-1/0/+1) of the launch's horizontal travel, i.e. the way a thrown body flies
        /// (away from the attacker, not where it faces). Used to bowl bystanders in the same direction.</summary>
        public int ThrowDirectionX => System.Math.Sign(thrownVelocity.X);

        /// <summary>True once a dead fighter has finished its death animation and may be removed.</summary>
        public bool IsExpired => State == FighterState.Dead && stateTimer >= deathDuration + deathBlinkDuration;

        /// <summary>How long the fighter has been in its current state.</summary>
        public float StateTimer => stateTimer;

        /// <summary>Body rectangle used for hit detection, in world space.</summary>
        public Rectangle HurtBox =>
            new Rectangle((int)(Position.X - BodyWidth / 2f), (int)(Position.Y - BodyHeight), BodyWidth, BodyHeight);

        protected bool CanAct =>
            !inEntryWalk && (State == FighterState.Idle || State == FighterState.Walk);

        /// <summary>
        /// Applies data-driven combat stats/timings to this fighter. Sets <see cref="Health"/> to
        /// the new maximum, so subclasses call it from their constructor. The per-wave
        /// <c>hitsToKnockdown</c> is deliberately left untouched (it comes from the spawn area).
        /// </summary>
        /// <summary>
        /// Loads a fighter's HUD portrait by convention from <c>Sprites/Portraits/&lt;set&gt;</c>.
        /// Returns null (graceful fallback, like <see cref="FighterAnimator"/>) when the art has
        /// not been added/registered yet. A new hero only needs to drop its portrait PNG and call
        /// <c>Portrait = LoadPortrait(content, "&lt;Set&gt;")</c> in its constructor.
        /// </summary>
        protected static Texture2D LoadPortrait(ContentManager content, string set)
        {
            try { return content.Load<Texture2D>("Sprites/Portraits/" + set); }
            catch (ContentLoadException) { return null; }
        }

        protected void ApplyTuning(FighterTuning t)
        {
            MaxHealth = t.MaxHealth;
            Health = t.MaxHealth;
            attackDamage = t.AttackDamage;
            attackReach = t.AttackReach;
            BodyWidth = t.BodyWidth;
            BodyHeight = t.BodyHeight;
            moveSpeed = t.MoveSpeed;

            attackWindup = t.AttackWindup;
            attackActive = t.AttackActive;
            attackRecovery = t.AttackRecovery;
            hitDuration = t.HitDuration;
            deathDuration = t.DeathDuration;
            deathBlinkDuration = t.DeathBlinkDuration;
            getUpDuration = t.GetUpDuration;
            invulnerabilityOnHit = t.InvulnerabilityOnHit;

            poiseResetWindow = t.PoiseResetWindow;
            knockdownDuration = t.KnockdownDuration;
            getUpInvulnerability = t.GetUpInvulnerability;

            jumpImpulse = t.JumpImpulse;
            jumpGravity = t.JumpGravity;
            planarJumpSpeed = t.PlanarJumpSpeed;
            jumpWindup = t.JumpWindup;
            jumpLandRecovery = t.JumpLandRecovery;
            jumpApexThreshold = t.JumpApexThreshold;
            dashSpeed = t.DashSpeed;
            dashDuration = t.DashDuration;
            dashInvulnerability = t.DashInvulnerability;

            attackBufferDuration = t.AttackBufferDuration;
            comboChain = CombatDefaults.BuildChain(t); // built once here (off the per-frame hot path)
        }

        /// <summary>
        /// Buffers an attack request. The swing itself starts later, when a move can begin: from a
        /// neutral state (see <see cref="Update"/>) or by cancelling the current swing's recovery
        /// (see <see cref="UpdateAttack"/>). Buffering means a press during the previous swing is
        /// never dropped — the source of the responsive, chainable feel.
        /// </summary>
        public void RequestAttack() => inputBuffer.PushAttack(attackBufferDuration);

        /// <summary>
        /// Opens a swing from a neutral state — always at the first move of the chain. The combo
        /// only climbs through the in-swing hit-confirmed cancel (see <see cref="UpdateAttack"/>),
        /// never by resuming a stale index, so a press right after a combo can't throw its finisher
        /// out of nowhere. Consumes the buffered press so one press starts exactly one swing.
        /// </summary>
        protected void StartAttack() => BeginMove(0);

        private void BeginMove(int index)
        {
            comboIndex = index;
            currentMove = comboChain[comboIndex];
            State = currentMove.State;
            stateTimer = 0f;
            CurrentAttack = null;
            attackHitTargets.Clear();
            inputBuffer.ConsumeAttack();
            // Restart (not SetState) so back-to-back swings of the same state — soco1→soco1, or a
            // mashed isolated punch — replay from frame 0 instead of holding the last punch frame.
            animator.Restart(State);
        }

        /// <summary>
        /// Begins a hop. A zero <paramref name="planarVelocity"/> jumps straight up; a ground
        /// component carries the fighter through the air in that direction — X is screen
        /// horizontal (forward/back), Y is corridor depth (up/down), so any of the eight
        /// directions works. Only allowed from a neutral locomotion state (ignored while
        /// attacking, reeling or already airborne).
        /// </summary>
        protected void StartJump(Vector2 planarVelocity)
        {
            // Also reachable mid-dash via TryDashCancelJump (Dash is not a CanAct state). The
            // normal jump path stays gated by CanAct upstream, so this only opens the cancel path.
            if (!CanAct && State != FighterState.Dash)
                return;

            // Begin with the grounded crouch (anticipation). The launch impulse and the planar
            // drift are held until the windup elapses (see UpdateJump), so Sofia plants and coils
            // before leaving the ground. jumpHeight stays 0 here, so she is drawn on the floor.
            State = FighterState.Jump;
            stateTimer = 0f;
            jumpHeight = 0f;
            jumpVerticalSpeed = 0f;
            jumpStartOffset = GroundOffset;   // remember the floor we leave so the arc ramps to the one we land on
            jumpPeak = 0f;
            curbDropActive = false;           // a real jump always runs its full land recovery
            jumpPlanarVelocity = planarVelocity;
            jumpPhaseTimer = 0f;
            velocity = Vector2.Zero;
            SetJumpPhase(JumpPhase.Start);
            animator.SetState(State);
        }

        /// <summary>Sets the hop sub-phase and notifies the animator (which restarts the strip).</summary>
        private void SetJumpPhase(JumpPhase phase)
        {
            CurrentJumpPhase = phase;
            animator.SetJumpPhase(phase);
        }

        /// <summary>
        /// Begins a small "curb drop" when walking off the sidewalk onto the asphalt, reusing the
        /// hop's fall: it starts already airborne <paramref name="dropHeight"/> px up with zero
        /// vertical speed (a step off a ledge), the landing floor being the asphalt, so the body
        /// holds the sidewalk height and falls to the road playing the <see cref="JumpPhase.Fall"/>
        /// strip. The take-off pose is continuous with the previous frame (the feet were drawn
        /// <c>dropHeight</c> up on the raised sidewalk). No-op unless the fighter
        /// <see cref="AnimatesCurbDrop"/> and is in a neutral state. Returns true if it took over.
        /// </summary>
        public bool TryStartCurbDrop(float dropHeight)
        {
            if (!AnimatesCurbDrop || !CanAct)
                return false;

            State = FighterState.Jump;
            stateTimer = 0f;
            jumpHeight = dropHeight;          // body keeps the sidewalk height...
            jumpVerticalSpeed = 0f;           // ...then falls under gravity (no upward impulse)
            jumpStartOffset = 0f;             // landing floor is the asphalt (GroundOffset target 0)
            jumpPeak = dropHeight;
            jumpPlanarVelocity = velocity;    // carry the walk drift so the step continues naturally
            jumpPhaseTimer = 0f;
            GroundOffset = 0f;                // ground reference is now the road; jumpHeight holds the body up
            curbDropActive = true;
            SetJumpPhase(JumpPhase.Fall);
            animator.SetState(State);
            return true;
        }

        /// <summary>
        /// Begins a "fall from the sky" entrance: the fighter starts <paramref name="dropHeight"/> px
        /// above its current <see cref="Position"/> with no vertical speed and drops under gravity to the
        /// ground, playing the hop's <see cref="JumpPhase.Fall"/> strip and a normal landing recovery.
        /// Reuses the jump machinery, so input/AI stays blocked (Jump is not a <see cref="CanAct"/> state)
        /// until touchdown. Used for a section's <c>Fall</c> entry.
        /// </summary>
        public void StartEntryFall(float dropHeight)
        {
            inEntryWalk = false;
            State = FighterState.Jump;
            stateTimer = 0f;
            jumpHeight = dropHeight;        // start high up...
            jumpVerticalSpeed = 0f;         // ...with no upward impulse: it just falls
            jumpStartOffset = 0f;           // ramp the curb elevation up from the road as it lands
            jumpPeak = dropHeight;
            jumpPlanarVelocity = Vector2.Zero;
            jumpPhaseTimer = 0f;
            curbDropActive = false;         // run the normal land recovery on touchdown
            velocity = Vector2.Zero;
            GroundOffset = 0f;
            SetJumpPhase(JumpPhase.Fall);
            animator.SetState(State);
        }

        /// <summary>
        /// Begins a "walk in from a door" entrance: the fighter walks to <paramref name="targetX"/> along
        /// the ground with no player/AI control (<see cref="CanAct"/> is false meanwhile), then returns to
        /// Idle. Reuses the Walk strip (no new asset). Used for a section's <c>Door</c> entry.
        /// </summary>
        public void StartEntryWalk(float targetX)
        {
            entryWalkTargetX = targetX;
            inEntryWalk = true;
            stateTimer = 0f;
            Facing = targetX < Position.X ? FaceDirection.Left : FaceDirection.Right;
            State = FighterState.Walk;
            velocity = Vector2.Zero;
            animator.SetState(State);
        }

        /// <summary>Drives the scripted door walk-in toward <see cref="entryWalkTargetX"/>; ends at Idle.</summary>
        private void UpdateEntryWalk(float dt)
        {
            float dx = entryWalkTargetX - Position.X;
            float step = moveSpeed * dt;
            if (step <= 0f || Math.Abs(dx) <= step)
            {
                Position.X = entryWalkTargetX;
                velocity = Vector2.Zero;
                inEntryWalk = false;
                State = FighterState.Idle;
                return;
            }

            float dir = dx < 0f ? -1f : 1f;
            Facing = dir < 0f ? FaceDirection.Left : FaceDirection.Right;
            Position.X += dir * step;
            velocity = Vector2.Zero;
            State = FighterState.Walk;
        }

        /// <summary>
        /// Begins a dash: a quick committed burst in <paramref name="direction"/> (already
        /// normalised; X is screen horizontal, Y is corridor depth, so any of the eight
        /// directions works). A zero direction dashes the way the fighter faces. Brief
        /// invulnerability at the start makes it a dodge. Only allowed from a neutral
        /// locomotion state (ignored while attacking, reeling, dashing or airborne).
        /// </summary>
        protected void StartDash(Vector2 direction)
        {
            if (!CanAct)
                return;

            if (direction == Vector2.Zero)
                direction = new Vector2((int)Facing, 0f);

            if (direction.X < 0f)
                Facing = FaceDirection.Left;
            else if (direction.X > 0f)
                Facing = FaceDirection.Right;

            State = FighterState.Dash;
            stateTimer = 0f;
            dashVelocity = direction * dashSpeed;
            velocity = dashVelocity;
            invulnTimer = dashInvulnerability;
            animator.SetState(State);
        }

        /// <summary>
        /// Cancels an in-progress dash into a jump, carrying the dash's direction as the planar
        /// drift. No-op unless currently dashing, so the player can leave the dash burst early with
        /// a hop instead of waiting for it to finish. Returns true if it took over.
        /// </summary>
        protected bool TryDashCancelJump()
        {
            if (State != FighterState.Dash)
                return false;

            Vector2 dir = dashVelocity;
            if (dir != Vector2.Zero)
                dir.Normalize();
            else
                dir = new Vector2((int)Facing, 0f);

            StartJump(dir * planarJumpSpeed);
            return true;
        }

        /// <summary>
        /// Throws an air kick: only valid while already airborne (a single kick per hop). The
        /// jump arc and locked ground velocity carry on; an attack hitbox opens for its active
        /// frames, and landing ends the move.
        /// </summary>
        protected void StartJumpAttack()
        {
            // Only mid-flight (not during the grounded crouch or the landing recovery): the kick
            // needs the fighter actually in the air.
            if (State != FighterState.Jump || CurrentJumpPhase == JumpPhase.Start || CurrentJumpPhase == JumpPhase.Land)
                return;

            State = FighterState.JumpAttack;
            stateTimer = 0f;
            CurrentAttack = null;
            attackHitTargets.Clear();
            animator.SetState(State);
        }

        /// <summary>Sets the idle/walk locomotion state (ignored while busy attacking or reeling).</summary>
        protected void SetLocomotion(FighterState locomotion)
        {
            if (CanAct)
                State = locomotion;
        }

        public virtual void TakeDamage(int amount, Vector2 knockback, HitReaction reaction = HitReaction.Normal)
        {
            if (State == FighterState.Dead || State == FighterState.KnockedDown
                || State == FighterState.Thrown || IsInvulnerable)
                return;

            Health -= amount;
            CurrentAttack = null;
            jumpHeight = 0f; // a hit drops an airborne fighter straight to the ground
            invulnTimer = invulnerabilityOnHit;
            stateTimer = 0f;

            // Being hit breaks the combo: the chain restarts and any buffered press is dropped so a
            // staggered fighter doesn't swing the instant it recovers.
            comboIndex = 0;
            inputBuffer.ConsumeAttack();

            if (reaction == HitReaction.Launch)
            {
                // Finisher kick: fling the fighter into the throw flight (full knockback, ignoring
                // poise). Even a lethal launch flies first — the landing (UpdateThrown) resolves death.
                Health = System.Math.Max(Health, 0);
                poiseHits = 0;
                thrownVelocity = knockback;
                velocity = knockback;
                attackHitTargets.Clear(); // reused as the bowl-over dedup while airborne
                State = FighterState.Thrown;
            }
            else if (Health <= 0)
            {
                Health = 0;
                velocity = knockback; // lethal blow: full fling
                State = OnDefeatedState();
            }
            else if (reaction == HitReaction.Knockdown)
            {
                // Bowled over by a thrown body: forced straight down, bypassing the poise counter.
                poiseHits = 0;
                velocity = new Vector2(knockback.X * 0.35f, knockback.Y);
                State = FighterState.KnockedDown;
            }
            else
            {
                // Survived a normal blow: accumulate poise damage. Stagger in place until the streak
                // reaches the knockdown threshold, then drop the fighter to the ground.
                poiseHits++;
                poiseResetTimer = poiseResetWindow;

                if (hitsToKnockdown > 0 && poiseHits >= hitsToKnockdown)
                {
                    poiseHits = 0;
                    velocity = new Vector2(knockback.X * 0.35f, knockback.Y); // falls roughly in place
                    State = FighterState.KnockedDown;
                }
                else
                {
                    velocity = Vector2.Zero; // no shove → the attacker's combo keeps connecting
                    State = FighterState.Hit;
                }
            }

            animator.SetState(State);
        }

        /// <summary>
        /// Bleeds off most of a thrown fighter's remaining flight speed when it bowls into a
        /// bystander, so it settles near the pile instead of plowing on (the "A para / cai junto"
        /// feel). No-op unless currently airborne from a launch.
        /// </summary>
        public void DampenThrow()
        {
            if (State != FighterState.Thrown)
                return;

            thrownVelocity *= 0.3f;
            velocity *= 0.3f;
        }

        /// <summary>State entered when health reaches zero. Enemies die; the player is knocked down.</summary>
        protected virtual FighterState OnDefeatedState() => FighterState.Dead;

        public virtual void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            stateTimer += dt;

            // Scripted door entrance runs its own minimal loop (no combat, no input/AI).
            if (inEntryWalk)
            {
                UpdateEntryWalk(dt);
                animator.SetState(State);
                return;
            }

            if (invulnTimer > 0f)
                invulnTimer -= dt;
            if (poiseResetTimer > 0f)
            {
                poiseResetTimer -= dt;
                if (poiseResetTimer <= 0f)
                    poiseHits = 0;
            }

            inputBuffer.Tick(dt);

            // Release a buffered attack the moment the fighter is free to act. From a neutral state
            // this always opens the first swing; the chain itself only climbs through the in-swing
            // hit-confirmed cancel handled inside UpdateAttack.
            if (CanAct && inputBuffer.HasAttack && comboChain != null)
                StartAttack();

            switch (State)
            {
                case FighterState.Attack:
                case FighterState.Attack2:
                case FighterState.Attack3:
                    UpdateAttack();
                    break;

                case FighterState.Dash:
                    UpdateDash(dt);
                    break;

                case FighterState.Jump:
                    UpdateJump(dt);
                    break;

                case FighterState.JumpAttack:
                    UpdateJumpAttack(dt);
                    break;

                case FighterState.Hit:
                    if (stateTimer >= hitDuration)
                        ReturnToIdle();
                    break;

                case FighterState.Thrown:
                    UpdateThrown(dt);
                    break;

                case FighterState.KnockedDown:
                    // Rise once the down time elapses, but only while still alive; a
                    // fighter knocked down at zero health (the player's defeat) stays down.
                    if (Health > 0)
                    {
                        if (stateTimer >= knockdownDuration)
                        {
                            State = FighterState.Idle;
                            stateTimer = 0f;
                            invulnTimer = getUpInvulnerability;
                            animator.SetState(State);
                        }
                        else if (stateTimer >= knockdownDuration - getUpDuration)
                        {
                            // Fração final: toca a animação de levantar do chão antes de voltar a agir.
                            animator.SetRising(true);
                        }
                    }
                    break;

                case FighterState.Dead:
                    // Stay down; the arena removes expired enemies.
                    break;
            }

            Position += velocity * dt;

            // Knockback / loose momentum bleeds off; deliberate movement is re-set each frame.
            velocity = Vector2.Lerp(velocity, Vector2.Zero, MathHelper.Clamp(dt * 8f, 0f, 1f));

            animator.SetState(State);
        }

        private void ReturnToIdle()
        {
            State = FighterState.Idle;
            stateTimer = 0f;
            CurrentAttack = null;
        }

        private void UpdateDash(float dt)
        {
            // Re-assert the locked burst velocity (the base Update bleeds it toward zero). Ease-out:
            // hold the speed up front (strong launch) and brake smoothly to ~0 by the end, so the
            // dash reads as a committed forward impulse that settles rather than a hard stop.
            float t = MathHelper.Clamp(stateTimer / dashDuration, 0f, 1f);
            velocity = dashVelocity * (1f - t * t);

            if (stateTimer >= dashDuration)
            {
                velocity = Vector2.Zero;
                ReturnToIdle();
            }
        }

        /// <summary>
        /// Drives the launched flight: the locked launch velocity eases linearly to rest over
        /// <see cref="throwDuration"/> (re-asserted each frame, since the base Update bleeds velocity),
        /// so the body actually travels backward instead of stopping at once. When the flight ends it
        /// lands — knocked down to get up, or dead if the launch (or a bowl-over) emptied its health.
        /// </summary>
        private void UpdateThrown(float dt)
        {
            float t = MathHelper.Clamp(stateTimer / throwDuration, 0f, 1f);
            velocity = thrownVelocity * (1f - t);

            if (stateTimer >= throwDuration)
                Land();
        }

        /// <summary>Ends a thrown flight: dead if out of health, otherwise knocked down (then it rises).</summary>
        private void Land()
        {
            velocity = Vector2.Zero;
            stateTimer = 0f;
            State = Health > 0 ? FighterState.KnockedDown : FighterState.Dead;
            animator.SetState(State);
        }

        private void UpdateJump(float dt)
        {
            // Grounded crouch: hold still until the windup elapses, then launch into the arc.
            if (CurrentJumpPhase == JumpPhase.Start)
            {
                velocity = Vector2.Zero;
                jumpPhaseTimer += dt;
                if (jumpPhaseTimer >= jumpWindup)
                {
                    jumpVerticalSpeed = jumpImpulse;
                    velocity = jumpPlanarVelocity;
                    SetJumpPhase(JumpPhase.Rise);
                }
                return;
            }

            // Grounded landing recovery: hold still until it elapses, then return to idle.
            if (CurrentJumpPhase == JumpPhase.Land)
            {
                velocity = Vector2.Zero;
                jumpPhaseTimer += dt;
                if (jumpPhaseTimer >= jumpLandRecovery)
                    ReturnToIdle();
                return;
            }

            // Airborne: advance the arc and pick rise/apex/fall from the vertical speed.
            jumpVerticalSpeed -= jumpGravity * dt;
            jumpHeight += jumpVerticalSpeed * dt;
            if (jumpHeight > jumpPeak) jumpPeak = jumpHeight;

            // Hold the locked ground velocity (horizontal + depth) for the whole arc (the base
            // Update bleeds velocity toward zero each frame, so re-assert it to keep it constant).
            velocity = jumpPlanarVelocity;

            if (jumpHeight <= 0f)
            {
                jumpHeight = 0f;
                velocity = Vector2.Zero;
                // A curb step-down skips the recovery and returns to idle at once, so walking off
                // the curb stays snappy; a real hop drops into its grounded recovery window.
                if (curbDropActive)
                {
                    curbDropActive = false;
                    ReturnToIdle();
                    return;
                }
                jumpPhaseTimer = 0f;
                SetJumpPhase(JumpPhase.Land);
                return;
            }

            UpdateAirPhase();
        }

        /// <summary>Picks Rise/Apex/Fall from the current vertical speed (physics-driven, so the
        /// phase stretches/shrinks naturally with the jump height instead of desyncing).</summary>
        private void UpdateAirPhase()
        {
            JumpPhase phase;
            if (jumpVerticalSpeed > jumpApexThreshold)
                phase = JumpPhase.Rise;
            else if (jumpVerticalSpeed < -jumpApexThreshold)
                phase = JumpPhase.Fall;
            else
                phase = JumpPhase.Apex;

            if (phase != CurrentJumpPhase)
                SetJumpPhase(phase);
        }

        private void UpdateJumpAttack(float dt)
        {
            // Keep arcing and drifting through the air (unlike the grounded attack, which plants
            // the fighter): the kick happens mid-flight and the hop still carries Sofia along.
            jumpVerticalSpeed -= jumpGravity * dt;
            jumpHeight += jumpVerticalSpeed * dt;
            if (jumpHeight > jumpPeak) jumpPeak = jumpHeight;
            velocity = jumpPlanarVelocity;

            // Same active-frame window as a ground swing. The hitbox is built from Position (the
            // feet, on the ground) so the kick reaches grounded enemies on the same depth line.
            if (stateTimer < attackWindup)
                CurrentAttack = null;
            else if (stateTimer < attackWindup + attackActive)
                CurrentAttack = BuildAttack();
            else
                CurrentAttack = null;

            if (jumpHeight <= 0f)
            {
                jumpHeight = 0f;
                velocity = Vector2.Zero;
                CurrentAttack = null;
                ReturnToIdle();
            }
        }

        private void UpdateAttack()
        {
            velocity = Vector2.Zero;
            ComboMove move = currentMove;

            if (stateTimer < move.Startup)
            {
                CurrentAttack = null;
            }
            else if (stateTimer < move.Startup + move.Active)
            {
                CurrentAttack = BuildAttack(move); // active frames: the hitbox is live
            }
            else
            {
                // Recovery. A buffered press past the cancel point cancels straight into the next
                // swing — the snappy, chainable feel. Where it goes depends on the hit confirm:
                //   • connected (and not the last move) → advance the combo (soco1→soco1→soco2→chute);
                //   • whiffed, or this is the last move → a fresh first move (fast soco1 jabs in the
                //     air, and a finished combo loops back to the start while the player keeps mashing).
                // No buffered press → the swing runs out and returns to idle, dropping the combo so the
                // next press opens at soco1 again.
                CurrentAttack = null;

                if (inputBuffer.HasAttack && stateTimer >= move.CancelPoint)
                {
                    bool connected = attackHitTargets.Count > 0; // kept until the next BeginMove
                    bool advance = comboIndex + 1 < comboChain.Count
                                   && (!move.RequiresHitConfirm || connected);
                    BeginMove(advance ? comboIndex + 1 : 0);
                    return;
                }

                if (stateTimer >= move.TotalDuration)
                    ReturnToIdle();
            }
        }

        /// <summary>Builds the transient hitbox for a combo <paramref name="move"/> (its reach,
        /// damage, knockback and launch flag), oriented to <see cref="Facing"/> and anchored on the feet.</summary>
        private AttackData BuildAttack(ComboMove move) =>
            BuildAttack(move.Reach, move.Damage, move.KnockbackX, move.KnockbackY, move.Launches);

        /// <summary>Builds a hitbox from the scalar attack stats (used by the airborne kick).</summary>
        private AttackData BuildAttack() =>
            BuildAttack(attackReach, attackDamage, 220f, -40f, false);

        private AttackData BuildAttack(int reach, int damage, float knockbackX, float knockbackY, bool launches)
        {
            const int height = 40;
            int width = reach;
            int top = (int)(Position.Y - BodyHeight + 10);
            int left = Facing == FaceDirection.Right
                ? (int)(Position.X + BodyWidth / 2f - 6)
                : (int)(Position.X - BodyWidth / 2f - width + 6);

            var hitbox = new Rectangle(left, top, width, height);
            var knockback = new Vector2((int)Facing * knockbackX, knockbackY);
            return new AttackData(hitbox, damage, knockback, launches);
        }

        /// <summary>
        /// Drives <see cref="GroundOffset"/> toward the floor elevation the arena resolved for this
        /// frame. On the ground it snaps (a crisp curb step when walking). While airborne it holds the
        /// take-off floor through the rise, then on the way down ramps to <paramref name="target"/> by
        /// the descent fraction (1 at touchdown), so a jump across the curb arcs smoothly and the feet
        /// meet the landing floor exactly when <see cref="jumpHeight"/> reaches 0 — no step jolt.
        /// </summary>
        public void SetGroundTarget(float target)
        {
            if (!IsAirborne)
            {
                GroundOffset = target;
                return;
            }

            if (jumpVerticalSpeed >= 0f)
            {
                GroundOffset = jumpStartOffset; // rising: stay on the floor we left
                return;
            }

            float fall = jumpPeak > 0f ? MathHelper.Clamp(1f - jumpHeight / jumpPeak, 0f, 1f) : 1f;
            GroundOffset = MathHelper.Lerp(jumpStartOffset, target, fall);
        }

        public virtual void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            // Após a animação de morte, o corpo pisca até a arena removê-lo (IsExpired).
            if (State == FighterState.Dead && stateTimer >= deathDuration)
            {
                float into = stateTimer - deathDuration;
                bool visible = ((int)(into / DeathBlinkInterval) & 1) == 0;
                if (!visible)
                    return; // quadro "apagado" do piscar: não desenha o sprite/placeholder neste frame
            }

            // The ground reference is raised by GroundOffset on the sidewalk (the curb step);
            // the shadow sits on that ground and the sprite is drawn a further jumpHeight up.
            // Position itself stays on the floor so depth sorting and collision are unaffected.
            float groundY = Position.Y - GroundOffset;
            // Tremor lateral só no corpo; a sombra fica parada no chão.
            float shakeX = HitShakeOffsetX();

            if (jumpHeight > 0f)
            {
                animator.DrawShadow(spriteBatch, new Vector2(Position.X, groundY), BodyWidth);
                animator.Draw(gameTime, spriteBatch, new Vector2(Position.X + shakeX, groundY - jumpHeight), Facing);
                return;
            }

            animator.Draw(gameTime, spriteBatch, new Vector2(Position.X + shakeX, groundY), Facing);
        }

        /// <summary>
        /// Deslocamento horizontal de tremor enquanto o fighter está em Hit. Oscila rápido e
        /// decai a zero ao longo de <see cref="hitDuration"/>, então não há "salto" ao voltar ao Idle.
        /// </summary>
        private float HitShakeOffsetX()
        {
            if (State != FighterState.Hit || hitDuration <= 0f)
                return 0f;

            float decay = MathHelper.Clamp(1f - stateTimer / hitDuration, 0f, 1f);
            return (float)System.Math.Sin(stateTimer * HitShakeFrequency) * HitShakeAmplitude * decay;
        }
    }
}
