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
        private const float StickDeadzone = 0.3f;

        public SofiaPlayer(ContentManager content, Texture2D blank, FighterTuning tuning = null)
        {
            ApplyTuning(tuning ?? FighterTuning.SofiaDefaults());
            animator = new FighterAnimator(content, blank, "Sofia", new Color(208, 210, 216),
                FighterSprites.Sofia, FighterSprites.SofiaJumpPhases);
            Portrait = LoadPortrait(content, "Sofia");
            Name = "Sofia";
        }

        /// <summary>The player is "knocked down" rather than removed when defeated.</summary>
        protected override FighterState OnDefeatedState() => FighterState.KnockedDown;

        /// <summary>Sofia cannot walk up the curb: she must jump to climb back onto the sidewalk.</summary>
        public override bool MustJumpCurb => true;

        /// <summary>Sofia plays the hop's fall as a small drop when she steps down off the curb.</summary>
        protected override bool AnimatesCurbDrop => true;

        /// <summary>True on a fresh attack press (J / gamepad A or X).</summary>
        private static bool AttackPressed(InputState input, PlayerIndex? controllingPlayer) =>
            input.IsNewKeyPress(Keys.J, controllingPlayer, out _)
            || input.IsNewButtonPress(Buttons.A, controllingPlayer, out _)
            || input.IsNewButtonPress(Buttons.X, controllingPlayer, out _);

        /// <summary>True on a fresh dash press (Left/Right Shift / gamepad shoulder buttons).</summary>
        private static bool DashPressed(InputState input, PlayerIndex? controllingPlayer) =>
            input.IsNewKeyPress(Keys.LeftShift, controllingPlayer, out _)
            || input.IsNewKeyPress(Keys.RightShift, controllingPlayer, out _)
            || input.IsNewButtonPress(Buttons.RightShoulder, controllingPlayer, out _)
            || input.IsNewButtonPress(Buttons.LeftShoulder, controllingPlayer, out _);

        /// <summary>True on a fresh jump press (Space / gamepad B).</summary>
        private static bool JumpPressed(InputState input, PlayerIndex? controllingPlayer) =>
            input.IsNewKeyPress(Keys.Space, controllingPlayer, out _)
            || input.IsNewButtonPress(Buttons.B, controllingPlayer, out _);

        public void HandleInput(InputState input, PlayerIndex? controllingPlayer)
        {
            // Air kick: pressing attack mid-hop kicks. Handled before the CanAct gate, which
            // otherwise blocks all input while airborne.
            if (State == FighterState.Jump && AttackPressed(input, controllingPlayer))
            {
                StartJumpAttack();
                return;
            }

            // Attack: buffer the press unconditionally (even mid-swing/dash) so it is never dropped;
            // Fighter releases it the instant a move can start or cancels the current recovery into
            // the next combo move. This is what fixes the "press didn't come out" stiffness.
            if (AttackPressed(input, controllingPlayer))
                RequestAttack();

            // Dash-cancel: a jump press during the dash burst leaves it early into a hop that
            // carries the dash's direction. Handled before the CanAct gate (Dash is not CanAct).
            if (JumpPressed(input, controllingPlayer) && TryDashCancelJump())
                return;

            // Movement, dash and jump still require a neutral state.
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
                velocity = direction * moveSpeed;

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

            // Dash: Shift / gamepad shoulder. A quick committed burst (and a dodge, thanks to its
            // start-up i-frames) in the held direction; with no direction held it dashes forward.
            if (DashPressed(input, controllingPlayer))
            {
                StartDash(direction);
                return;
            }

            // Jump: Space / gamepad B. The held direction (already normalised above) carries the
            // jump along the ground: X = forward/back, Y = corridor depth (up/down). No direction
            // held → a straight-up hop. All eight directions work.
            if (JumpPressed(input, controllingPlayer))
            {
                StartJump(direction * planarJumpSpeed);
            }
        }
    }
}
