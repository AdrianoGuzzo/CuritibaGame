using System;
using Curitiba.Core.Inputs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// The player character, Sofia. Moves in eight directions and throws a single
    /// simple attack (Space / gamepad A or X), as specified for the demo.
    /// </summary>
    internal class SofiaPlayer : Fighter
    {
        private const float MoveSpeed = 175f;
        private const float StickDeadzone = 0.3f;

        // Combo: pressing attack again within this window after a swing chains into the
        // other punch; letting it lapse restarts the combo from the first punch.
        private const float ComboChainWindow = 0.75f; // > swing duration (~0.40s), leaves ~0.35s of grace
        private float comboTimer;     // >0 while the chain is still open
        private bool secondPunchNext; // which punch variant the next swing plays

        public SofiaPlayer(ContentManager content, Texture2D blank)
        {
            MaxHealth = 100;
            Health = 100;
            attackDamage = 10;
            attackReach = 48;
            BodyWidth = 40;
            BodyHeight = 74;
            animator = new FighterAnimator(content, blank, "Sofia", new Color(208, 210, 216), FighterSprites.Sofia);
        }

        /// <summary>The player is "knocked down" rather than removed when defeated.</summary>
        protected override FighterState OnDefeatedState() => FighterState.KnockedDown;

        /// <summary>Alternates Punch/Punch2 each swing, reopening the chain window.</summary>
        protected override FighterState NextSwingState()
        {
            FighterState swing = secondPunchNext ? FighterState.Attack2 : FighterState.Attack;
            secondPunchNext = !secondPunchNext;
            comboTimer = ComboChainWindow;
            return swing;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (comboTimer > 0f)
            {
                comboTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (comboTimer <= 0f)
                    secondPunchNext = false; // chain lapsed: next swing restarts the combo
            }
        }

        /// <summary>True on a fresh attack press (J / gamepad A or X).</summary>
        private static bool AttackPressed(InputState input, PlayerIndex? controllingPlayer) =>
            input.IsNewKeyPress(Keys.J, controllingPlayer, out _)
            || input.IsNewButtonPress(Buttons.A, controllingPlayer, out _)
            || input.IsNewButtonPress(Buttons.X, controllingPlayer, out _);

        public void HandleInput(InputState input, PlayerIndex? controllingPlayer)
        {
            // Air kick: pressing attack mid-hop kicks. Handled before the CanAct gate, which
            // otherwise blocks all input while airborne.
            if (State == FighterState.Jump && AttackPressed(input, controllingPlayer))
            {
                StartJumpAttack();
                return;
            }

            if (!CanAct)
                return;

            KeyboardState keyboard = input.CurrentKeyboardStates[0];
            GamePadState gamePad = input.CurrentGamePadStates[0];

            Vector2 direction = Vector2.Zero;

            if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A) || gamePad.IsButtonDown(Buttons.DPadLeft))
                direction.X -= 1f;
            if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D) || gamePad.IsButtonDown(Buttons.DPadRight))
                direction.X += 1f;
            if (keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.W) || gamePad.IsButtonDown(Buttons.DPadUp))
                direction.Y -= 1f;
            if (keyboard.IsKeyDown(Keys.Down) || keyboard.IsKeyDown(Keys.S) || gamePad.IsButtonDown(Buttons.DPadDown))
                direction.Y += 1f;

            Vector2 stick = gamePad.ThumbSticks.Left;
            if (Math.Abs(stick.X) > StickDeadzone)
                direction.X += Math.Sign(stick.X);
            if (Math.Abs(stick.Y) > StickDeadzone)
                direction.Y -= Math.Sign(stick.Y); // stick Y is inverted relative to screen space

            if (direction != Vector2.Zero)
            {
                direction.Normalize();
                velocity = direction * MoveSpeed;

                if (direction.X < 0f)
                    Facing = FaceDirection.Left;
                else if (direction.X > 0f)
                    Facing = FaceDirection.Right;

                SetLocomotion(FighterState.Walk);
            }
            else
            {
                velocity = Vector2.Zero;
                SetLocomotion(FighterState.Idle);
            }

            // Jump: Space / gamepad B. The held direction (already normalised above) carries the
            // jump along the ground: X = forward/back, Y = corridor depth (up/down). No direction
            // held → a straight-up hop. All eight directions work.
            if (input.IsNewKeyPress(Keys.Space, controllingPlayer, out _)
                || input.IsNewButtonPress(Buttons.B, controllingPlayer, out _))
            {
                StartJump(direction * planarJumpSpeed);
            }

            // Attack: J / gamepad A or X (new press only).
            if (AttackPressed(input, controllingPlayer))
            {
                StartAttack();
            }
        }
    }
}
