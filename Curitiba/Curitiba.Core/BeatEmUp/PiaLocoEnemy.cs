using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Basic enemy, "Pia Loco". Walks toward Sofia and attacks once she is in range,
    /// with a short cooldown between swings. Removed from the arena once its death
    /// animation finishes.
    /// </summary>
    internal class PiaLocoEnemy : Fighter
    {
        private const float MoveSpeed = 72f;
        private const float AttackRange = 52f;
        private const float VerticalTolerance = 16f;
        private const float AttackCooldownTime = 1.3f;

        private readonly SofiaPlayer target;
        private float attackCooldown;

        public PiaLocoEnemy(ContentManager content, Texture2D blank, Vector2 position, SofiaPlayer target,
                            int hitsToKnockdown)
        {
            Position = position;
            this.target = target;

            MaxHealth = 30;
            Health = 30;
            attackDamage = 5;
            attackReach = 40;
            BodyWidth = 42;
            BodyHeight = 72;
            this.hitsToKnockdown = hitsToKnockdown; // blows in a row before this enemy falls (per wave)
            animator = new FighterAnimator(content, blank, "PiaLoco", new Color(150, 112, 82), FighterSprites.PiaLoco);
        }

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (attackCooldown > 0f)
                attackCooldown -= dt;

            if (CanAct)
            {
                Vector2 toTarget = target.Position - Position;
                Facing = toTarget.X < 0f ? FaceDirection.Left : FaceDirection.Right;

                float distance = toTarget.Length();
                bool inAttackRange = distance <= AttackRange && Math.Abs(toTarget.Y) <= VerticalTolerance;

                if (inAttackRange && attackCooldown <= 0f && target.IsAlive)
                {
                    StartAttack();
                    attackCooldown = AttackCooldownTime;
                }
                else if (distance > AttackRange - 6f && target.IsAlive)
                {
                    Vector2 step = toTarget;
                    if (step != Vector2.Zero)
                        step.Normalize();
                    velocity = step * MoveSpeed;
                    SetLocomotion(FighterState.Walk);
                }
                else
                {
                    velocity = Vector2.Zero;
                    SetLocomotion(FighterState.Idle);
                }
            }

            base.Update(gameTime);
        }
    }
}
