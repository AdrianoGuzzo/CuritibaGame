using System.Collections.Generic;
using Microsoft.Xna.Framework;
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

        public int Health { get; protected set; }
        public int MaxHealth { get; protected set; }
        public FighterState State { get; protected set; } = FighterState.Idle;

        /// <summary>Collision body, in world space, used both as a hurtbox and for clamping.</summary>
        public int BodyWidth = 40;
        public int BodyHeight = 72;

        // Attack timing (seconds). Subclasses tune these.
        protected float attackWindup = 0.12f;
        protected float attackActive = 0.10f;
        protected float attackRecovery = 0.18f;
        protected int attackDamage;
        protected int attackReach = 46;

        // Reaction timing.
        protected float hitDuration = 0.30f;
        protected float deathDuration = 0.70f;
        protected float invulnerabilityOnHit = 0.25f;

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
        protected const float JumpImpulse = 430f;   // initial upward speed, px/s
        protected const float JumpGravity = 1500f;  // downward acceleration, px/s^2
        protected float planarJumpSpeed = 150f;     // ground speed of a directional jump (any of the 8 dirs)
        protected float jumpHeight;                 // current visual height above the ground (>=0)
        private float jumpVerticalSpeed;            // vertical speed of the arc, px/s upward
        private Vector2 jumpPlanarVelocity;         // locked ground velocity (X horizontal + Y depth) for the arc

        // Dash: a short, committed burst in one of the eight directions. Far faster than a walk,
        // it grants a sliver of invulnerability at the start so it doubles as a dodge. The fighter
        // is locked out of other actions until it ends (Dash is not a CanAct state).
        protected float dashSpeed = 1050f;          // peak launch speed, px/s (eases out to 0 over the dash)
        protected float dashDuration = 0.40f;       // how long the burst lasts
        protected float dashInvulnerability = 0.16f; // i-frames at the start of the dash
        private Vector2 dashVelocity;               // locked burst velocity for the dash

        protected FighterAnimator animator;
        protected Vector2 velocity;

        private float stateTimer;
        private float invulnTimer;
        private int poiseHits;
        private float poiseResetTimer;
        private readonly HashSet<Fighter> attackHitTargets = new HashSet<Fighter>();

        /// <summary>Targets already struck by the current swing (so each blow lands once).</summary>
        public HashSet<Fighter> AttackHitTargets => attackHitTargets;

        /// <summary>Active damaging hitbox, present only during an attack's active frames.</summary>
        public AttackData? CurrentAttack { get; private set; }

        public bool IsAlive => State != FighterState.Dead && State != FighterState.KnockedDown;
        public bool IsDefeated => Health <= 0;
        public bool IsInvulnerable => invulnTimer > 0f;

        /// <summary>True once a dead fighter has finished its death animation and may be removed.</summary>
        public bool IsExpired => State == FighterState.Dead && stateTimer >= deathDuration;

        /// <summary>How long the fighter has been in its current state.</summary>
        public float StateTimer => stateTimer;

        /// <summary>Body rectangle used for hit detection, in world space.</summary>
        public Rectangle HurtBox =>
            new Rectangle((int)(Position.X - BodyWidth / 2f), (int)(Position.Y - BodyHeight), BodyWidth, BodyHeight);

        protected bool CanAct =>
            State == FighterState.Idle || State == FighterState.Walk;

        protected void StartAttack()
        {
            State = NextSwingState();
            stateTimer = 0f;
            CurrentAttack = null;
            attackHitTargets.Clear();
            animator.SetState(State);
        }

        /// <summary>
        /// Which attack state this swing uses. The base fighter always throws the single
        /// <see cref="FighterState.Attack"/>; subclasses can override to alternate variants
        /// (e.g. Sofia's Punch/Punch2 combo). All attack states share the same timing and
        /// hitbox, so this choice is purely which animation plays.
        /// </summary>
        protected virtual FighterState NextSwingState() => FighterState.Attack;

        /// <summary>
        /// Begins a hop. A zero <paramref name="planarVelocity"/> jumps straight up; a ground
        /// component carries the fighter through the air in that direction — X is screen
        /// horizontal (forward/back), Y is corridor depth (up/down), so any of the eight
        /// directions works. Only allowed from a neutral locomotion state (ignored while
        /// attacking, reeling or already airborne).
        /// </summary>
        protected void StartJump(Vector2 planarVelocity)
        {
            if (!CanAct)
                return;

            State = FighterState.Jump;
            stateTimer = 0f;
            jumpHeight = 0f;
            jumpVerticalSpeed = JumpImpulse;
            jumpPlanarVelocity = planarVelocity;
            velocity = planarVelocity;
            animator.SetState(State);
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
        /// Throws an air kick: only valid while already airborne (a single kick per hop). The
        /// jump arc and locked ground velocity carry on; an attack hitbox opens for its active
        /// frames, and landing ends the move.
        /// </summary>
        protected void StartJumpAttack()
        {
            if (State != FighterState.Jump)
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

        public virtual void TakeDamage(int amount, Vector2 knockback)
        {
            if (State == FighterState.Dead || State == FighterState.KnockedDown || IsInvulnerable)
                return;

            Health -= amount;
            CurrentAttack = null;
            jumpHeight = 0f; // a hit drops an airborne fighter straight to the ground
            invulnTimer = invulnerabilityOnHit;
            stateTimer = 0f;

            if (Health <= 0)
            {
                Health = 0;
                velocity = knockback; // lethal blow: full fling
                State = OnDefeatedState();
            }
            else
            {
                // Survived: accumulate poise damage. Stagger in place until the streak
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

        /// <summary>State entered when health reaches zero. Enemies die; the player is knocked down.</summary>
        protected virtual FighterState OnDefeatedState() => FighterState.Dead;

        public virtual void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            stateTimer += dt;
            if (invulnTimer > 0f)
                invulnTimer -= dt;
            if (poiseResetTimer > 0f)
            {
                poiseResetTimer -= dt;
                if (poiseResetTimer <= 0f)
                    poiseHits = 0;
            }

            switch (State)
            {
                case FighterState.Attack:
                case FighterState.Attack2:
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

                case FighterState.KnockedDown:
                    // Rise once the down time elapses, but only while still alive; a
                    // fighter knocked down at zero health (the player's defeat) stays down.
                    if (stateTimer >= knockdownDuration && Health > 0)
                    {
                        State = FighterState.Idle;
                        stateTimer = 0f;
                        invulnTimer = getUpInvulnerability;
                        animator.SetState(State);
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

        private void UpdateJump(float dt)
        {
            jumpVerticalSpeed -= JumpGravity * dt;
            jumpHeight += jumpVerticalSpeed * dt;

            // Hold the locked ground velocity (horizontal + depth) for the whole arc (the base
            // Update bleeds velocity toward zero each frame, so re-assert it to keep it constant).
            velocity = jumpPlanarVelocity;

            if (jumpHeight <= 0f)
            {
                jumpHeight = 0f;
                velocity = Vector2.Zero;
                ReturnToIdle();
            }
        }

        private void UpdateJumpAttack(float dt)
        {
            // Keep arcing and drifting through the air (unlike the grounded attack, which plants
            // the fighter): the kick happens mid-flight and the hop still carries Sofia along.
            jumpVerticalSpeed -= JumpGravity * dt;
            jumpHeight += jumpVerticalSpeed * dt;
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

            if (stateTimer < attackWindup)
            {
                CurrentAttack = null;
            }
            else if (stateTimer < attackWindup + attackActive)
            {
                CurrentAttack = BuildAttack();
            }
            else if (stateTimer < attackWindup + attackActive + attackRecovery)
            {
                CurrentAttack = null;
            }
            else
            {
                ReturnToIdle();
            }
        }

        private AttackData BuildAttack()
        {
            const int height = 40;
            int width = attackReach;
            int top = (int)(Position.Y - BodyHeight + 10);
            int left = Facing == FaceDirection.Right
                ? (int)(Position.X + BodyWidth / 2f - 6)
                : (int)(Position.X - BodyWidth / 2f - width + 6);

            var hitbox = new Rectangle(left, top, width, height);
            var knockback = new Vector2((int)Facing * 220f, -40f);
            return new AttackData(hitbox, attackDamage, knockback);
        }

        public virtual void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            // While airborne, leave a shadow on the ground (the feet point) and draw the
            // sprite jumpHeight pixels higher; Position itself stays on the floor so depth
            // sorting and collision are unaffected.
            if (jumpHeight > 0f)
            {
                animator.DrawShadow(spriteBatch, Position, BodyWidth);
                animator.Draw(gameTime, spriteBatch, new Vector2(Position.X, Position.Y - jumpHeight), Facing);
                return;
            }

            animator.Draw(gameTime, spriteBatch, Position, Facing);
        }
    }
}
