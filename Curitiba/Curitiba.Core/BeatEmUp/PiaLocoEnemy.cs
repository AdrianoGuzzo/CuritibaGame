using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Basic enemy, "Pia Loco". Rather than charging straight at Sofia, it claims a slot in the
    /// ring the <see cref="AttackSlotManager"/> keeps around her, holds its spacing from the
    /// other enemies, and only steps in to swing when it wins one of the limited attack tokens —
    /// so the crowd surrounds the player and takes turns instead of piling on. A
    /// <see cref="EnemyPersonality"/> tunes how eager, cautious or fast it behaves.
    ///
    /// AI intent (this class) only sets <c>velocity</c> and requests attacks; the base
    /// <see cref="Fighter"/> applies movement and the animation follows via the shared
    /// <see cref="FighterState"/> machine.
    /// </summary>
    internal class PiaLocoEnemy : Fighter
    {
        private const float AttackRange = 52f;
        private const float VerticalTolerance = 16f;

        // Crowd spacing: enemies closer than this shove apart (bodies are ~42px wide).
        private const float MinimumEnemyDistance = 44f;
        private const float SeparationStrength = 2.0f;
        private const float MaxSeparationSpeed = 90f;

        // How close to its ring slot counts as "settled" (ready to bid for a turn).
        private const float SlotArriveRadius = 10f;
        // Abandon a committed lunge that never connects (the player walked off).
        private const float MaxEngageTime = 1.6f;

        // Shared RNG for personality attack rolls. Single-threaded update loop, so no locking
        // needed, and NextDouble() does not allocate (safe for the per-frame hot path).
        private static readonly Random Rng = new Random();

        private readonly SofiaPlayer target;
        private readonly AttackSlotManager slots;
        private readonly IReadOnlyList<PiaLocoEnemy> neighbors;
        private readonly EnemyProfile profile;

        private EnemyAiState aiState = EnemyAiState.Idle;
        private float attackCooldown;   // min time between this enemy's own swings
        private float cooldownTimer;    // post-swing recovery before rejoining the rotation
        private float engageTimer;      // guards a committed lunge from stalling forever
        private Vector2 entryTarget;    // walk-in destination while in EnemyAiState.Entering

        public PiaLocoEnemy(ContentManager content, Texture2D blank, Vector2 position, SofiaPlayer target,
                            int hitsToKnockdown, AttackSlotManager slots,
                            IReadOnlyList<PiaLocoEnemy> neighbors,
                            EnemyProfile profile, FighterTuning tuning = null)
        {
            Position = position;
            this.target = target;
            this.slots = slots;
            this.neighbors = neighbors;
            this.profile = profile;

            Name = "Piá Loco";
            ApplyTuning(tuning ?? FighterTuning.PiaLocoDefaults());
            this.hitsToKnockdown = hitsToKnockdown; // blows in a row before this enemy falls (per wave)
            animator = new FighterAnimator(content, blank, "PiaLoco", new Color(150, 112, 82), FighterSprites.PiaLoco);
        }

        /// <summary>True while the enemy is still walking in from its off-screen birth point. The arena
        /// lets it sit outside the world bounds until it arrives (no edge clamp while entering).</summary>
        public bool IsEntering => aiState == EnemyAiState.Entering;

        /// <summary>Starts this enemy in the walk-in state, heading for <paramref name="target"/> in the
        /// play area before its combat AI engages. Called by the factory right after construction.</summary>
        public void BeginEntry(Vector2 target)
        {
            entryTarget = target;
            aiState = EnemyAiState.Entering;
        }

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (attackCooldown > 0f)
                attackCooldown -= dt;

            // Knocked down or dead: give up the slot and token so the others can move up.
            if (!IsAlive)
            {
                ReleaseCombatClaims();
                base.Update(gameTime);
                return;
            }

            // Staggered mid-turn: drop the attack token so a stunned enemy doesn't hog it,
            // and rejoin from Positioning once it recovers.
            if (State == FighterState.Hit)
            {
                slots.ReleaseAttackToken(this);
                aiState = EnemyAiState.Positioning;
            }

            // The base state machine has carried a swing to its end (back to a CanAct state):
            // begin the post-attack recovery, still holding the token until it lapses.
            if (aiState == EnemyAiState.Attack && CanAct)
            {
                aiState = EnemyAiState.Cooldown;
                cooldownTimer = profile.AttackCooldown;
            }

            if (CanAct)
                UpdateAi(dt);

            base.Update(gameTime);
        }

        private void UpdateAi(float dt)
        {
            Vector2 toTarget = target.Position - Position;
            Facing = toTarget.X < 0f ? FaceDirection.Left : FaceDirection.Right;
            float distance = toTarget.Length();

            if (!target.IsAlive)
            {
                ReleaseCombatClaims();
                velocity = Separation();
                SetLocomotion(velocity.LengthSquared() > 1f ? FighterState.Walk : FighterState.Idle);
                return;
            }

            switch (aiState)
            {
                case EnemyAiState.Entering:
                    UpdateEntering();
                    break;

                case EnemyAiState.Cooldown:
                    UpdateCooldown(dt);
                    break;

                case EnemyAiState.Engaging:
                    UpdateEngaging(dt, toTarget, distance);
                    break;

                default: // Idle / Positioning
                    UpdatePositioning(distance);
                    break;
            }
        }

        // Walk in from the off-screen birth point to the entry target; hand off to the combat AI on
        // arrival. No ring slot or attack token is claimed while entering, so the crowd only forms up
        // once the newcomers are actually on the field.
        private void UpdateEntering()
        {
            float distanceToPlayer = (target.Position - Position).Length();
            float distToTarget = MoveToward(entryTarget, distanceToPlayer, moveSpeed);
            if (distToTarget <= SlotArriveRadius * 3f)
                aiState = EnemyAiState.Positioning;
        }

        // Walk to the reserved ring slot, keeping spacing; bid for a turn once settled there.
        private void UpdatePositioning(float distanceToPlayer)
        {
            Vector2 ringPoint = ReserveRingPoint();
            float distToSlot = MoveToward(ringPoint, distanceToPlayer, moveSpeed);

            bool settled = distToSlot <= SlotArriveRadius * 3f;
            if (settled && attackCooldown <= 0f && WantsToAttack() && slots.TryAcquireAttackToken(this))
            {
                aiState = EnemyAiState.Engaging;
                engageTimer = 0f;
            }
            else
            {
                aiState = EnemyAiState.Positioning;
            }
        }

        // Token in hand: step straight in and swing once aligned in depth and within reach.
        private void UpdateEngaging(float dt, Vector2 toTarget, float distance)
        {
            engageTimer += dt;
            bool aligned = Math.Abs(toTarget.Y) <= VerticalTolerance;

            if (distance <= AttackRange && aligned && attackCooldown <= 0f)
            {
                StartAttack();
                attackCooldown = profile.AttackCooldown;
                aiState = EnemyAiState.Attack;
                velocity = Vector2.Zero;
                return;
            }

            // Lost the opening (player moved away, or too slow to connect): yield the turn.
            if (engageTimer > MaxEngageTime)
            {
                slots.ReleaseAttackToken(this);
                aiState = EnemyAiState.Positioning;
                return;
            }

            // Close in on the player directly (depth first, then across), nudged by spacing.
            MoveToward(target.Position, distance, moveSpeed);
        }

        // Recover after a swing: drift back toward the ring, then rejoin the rotation.
        private void UpdateCooldown(float dt)
        {
            cooldownTimer -= dt;
            Vector2 ringPoint = ReserveRingPoint();
            float distanceToPlayer = (target.Position - Position).Length();
            MoveToward(ringPoint, distanceToPlayer, moveSpeed * 0.8f);

            if (cooldownTimer <= 0f)
            {
                slots.ReleaseAttackToken(this);
                aiState = EnemyAiState.Positioning;
            }
        }

        private Vector2 ReserveRingPoint()
        {
            int slot = slots.Reserve(this, target.Position);
            return slot >= 0 ? slots.WorldPosition(slot, target.Position) : target.Position;
        }

        /// <summary>
        /// Moves toward <paramref name="point"/> the beat 'em up way: line up the Y (corridor
        /// depth) first, then advance on X. Runners sprint to close a big gap. Separation from the
        /// other enemies is blended into the velocity so bodies never stack. All motion goes through
        /// the base <c>velocity</c> field (no teleporting). Returns the remaining distance.
        /// </summary>
        private float MoveToward(Vector2 point, float distanceToPlayer, float baseSpeed)
        {
            Vector2 to = point - Position;
            float dist = to.Length();

            float speed = baseSpeed;
            if (profile.Profile == EnemyPersonality.Runner && distanceToPlayer > profile.RunDistance)
                speed = baseSpeed * profile.RunSpeedMultiplier;

            Vector2 move = Vector2.Zero;
            if (dist > SlotArriveRadius)
            {
                // Prioritise depth alignment first for the classic arcade shuffle: damp the
                // horizontal component until the depth (Y) gap is within striking tolerance.
                Vector2 dir = Math.Abs(to.Y) > VerticalTolerance ? new Vector2(to.X * 0.35f, to.Y) : to;
                if (dir != Vector2.Zero)
                    dir.Normalize();
                move = dir * speed;
            }

            velocity = move + Separation();
            SetLocomotion(velocity.LengthSquared() > 1f ? FighterState.Walk : FighterState.Idle);
            return dist;
        }

        // Sum of pushes away from every neighbour that is too close (gentle, capped) so enemies
        // never overlap even while converging on adjacent slots.
        private Vector2 Separation()
        {
            Vector2 push = Vector2.Zero;
            for (int i = 0; i < neighbors.Count; i++)
            {
                PiaLocoEnemy other = neighbors[i];
                if (other == this || !other.IsAlive)
                    continue;

                Vector2 away = Position - other.Position;
                float d = away.Length();
                if (d > 0.001f && d < MinimumEnemyDistance)
                {
                    away /= d; // normalise
                    push += away * (MinimumEnemyDistance - d); // stronger the closer they are
                }
            }

            push *= SeparationStrength;
            if (push.LengthSquared() > MaxSeparationSpeed * MaxSeparationSpeed)
            {
                push.Normalize();
                push *= MaxSeparationSpeed;
            }
            return push;
        }

        // Personality eagerness roll. On a miss, hesitate briefly so the roll isn't spammed
        // every frame while the enemy sits in range.
        private bool WantsToAttack()
        {
            if (Rng.NextDouble() <= profile.AttackChance)
                return true;

            attackCooldown = 0.35f;
            return false;
        }

        private void ReleaseCombatClaims()
        {
            slots.Release(this); // frees both the ring slot and any attack token
            aiState = EnemyAiState.Idle;
        }
    }
}
