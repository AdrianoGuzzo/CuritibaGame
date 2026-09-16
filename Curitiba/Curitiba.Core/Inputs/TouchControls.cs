using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace Curitiba.Core.Inputs
{
    /// <summary>
    /// On-screen controls for the beat 'em up on touch devices: a floating stick on the left
    /// half of the screen and three action buttons on the right.
    /// </summary>
    /// <remarks>
    /// The whole thing is expressed as a synthesized <see cref="GamePadState"/> (see
    /// <see cref="Merge"/>), so gameplay code stays untouched — <c>SofiaPlayer</c> already reads
    /// the D-pad and face buttons off <c>InputState.CurrentGamePadStates[0]</c>.
    /// <para>
    /// Unlike <see cref="VirtualGamePad"/> (two directions and one button, built for the dormant
    /// platformer), this covers the eight directions plus attack, jump and dash. Jump is not
    /// optional: <c>SofiaPlayer.MustJumpCurb</c> means she cannot walk up the curb.
    /// </para>
    /// Coordinates are in the virtual <c>BaseScreenSize</c> space, so callers transform touches
    /// with <c>InputState.TransformCursorLocation</c> and draw inside a
    /// <c>GlobalTransformation</c> batch.
    /// </remarks>
    internal sealed class TouchControls : IDisposable
    {
        /// <summary>Radius, in virtual pixels, at which the stick reads as fully deflected.</summary>
        private const float StickRadius = 52f;

        /// <summary>Deflection below this is treated as "no direction", so a resting thumb doesn't walk.</summary>
        private const float StickDeadzone = 14f;

        /// <summary>sin(22.5°) — splits the stick into eight even octants.</summary>
        private const float OctantThreshold = 0.3827f;

        private const float IdleOpacity = 0.45f;
        private const float ActiveOpacity = 0.85f;

        /// <summary>Diameter of the generated disc texture; every control scales it down.</summary>
        private const int DiscSize = 128;

        /// <summary>Buttons copied straight through from a physical pad, if one is connected.</summary>
        private static readonly Buttons[] PassThroughButtons =
        {
            Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
            Buttons.Start, Buttons.Back, Buttons.BigButton,
            Buttons.LeftShoulder, Buttons.RightShoulder,
            Buttons.LeftStick, Buttons.RightStick,
            Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
        };

        private readonly Vector2 baseScreenSize;
        private readonly SpriteFont font;
        private readonly Texture2D disc;
        private readonly Vector2 discOrigin;

        private readonly Vector2 stickHome;
        private readonly Vector2 attackCenter;
        private readonly Vector2 jumpCenter;
        private readonly Vector2 dashCenter;

        private const float AttackRadius = 44f;
        private const float JumpRadius = 34f;
        private const float DashRadius = 30f;

        private int stickTouchId = -1;
        private Vector2 stickOrigin;
        private Vector2 stickPosition;
        private Vector2 direction;

        private bool attackHeld;
        private bool jumpHeld;
        private bool dashHeld;

        public TouchControls(GraphicsDevice graphicsDevice, Vector2 baseScreenSize, SpriteFont font)
        {
            this.baseScreenSize = baseScreenSize;
            this.font = font;

            disc = CreateDisc(graphicsDevice, DiscSize);
            discOrigin = new Vector2(DiscSize / 2f, DiscSize / 2f);

            stickHome = new Vector2(120f, baseScreenSize.Y - 104f);
            attackCenter = new Vector2(baseScreenSize.X - 72f, baseScreenSize.Y - 102f);
            jumpCenter = new Vector2(baseScreenSize.X - 164f, baseScreenSize.Y - 56f);
            dashCenter = new Vector2(baseScreenSize.X - 164f, baseScreenSize.Y - 150f);
        }

        /// <summary>
        /// Re-reads every active touch. The stick is claimed by the first touch landing on the left
        /// half and keeps its id until released, so the player can steer and hit a button at once.
        /// </summary>
        public void Update(TouchCollection touches, InputState input)
        {
            attackHeld = false;
            jumpHeld = false;
            dashHeld = false;

            bool stickStillDown = false;

            foreach (TouchLocation touch in touches)
            {
                if (touch.State != TouchLocationState.Pressed && touch.State != TouchLocationState.Moved)
                    continue;

                Vector2 position = input.TransformCursorLocation(touch.Position);

                if (touch.Id == stickTouchId)
                {
                    stickStillDown = true;
                    stickPosition = position;
                    continue;
                }

                if (position.X < baseScreenSize.X * 0.5f)
                {
                    if (stickTouchId < 0)
                    {
                        stickTouchId = touch.Id;
                        stickOrigin = position;
                        stickPosition = position;
                        stickStillDown = true;
                    }

                    continue;
                }

                if (IsInside(position, attackCenter, AttackRadius))
                    attackHeld = true;
                else if (IsInside(position, jumpCenter, JumpRadius))
                    jumpHeld = true;
                else if (IsInside(position, dashCenter, DashRadius))
                    dashHeld = true;
            }

            if (!stickStillDown)
                stickTouchId = -1;

            direction = Vector2.Zero;

            if (stickTouchId >= 0)
            {
                Vector2 offset = stickPosition - stickOrigin;

                if (offset.LengthSquared() > StickDeadzone * StickDeadzone)
                {
                    offset.Normalize();
                    direction = offset;
                }
            }
        }

        /// <summary>
        /// Folds the on-screen controls into <paramref name="physical"/>, so a connected gamepad
        /// keeps working alongside touch.
        /// </summary>
        public GamePadState Merge(GamePadState physical)
        {
            Buttons pressed = 0;

            if (direction.X < -OctantThreshold)
                pressed |= Buttons.DPadLeft;
            else if (direction.X > OctantThreshold)
                pressed |= Buttons.DPadRight;

            if (direction.Y < -OctantThreshold)
                pressed |= Buttons.DPadUp;
            else if (direction.Y > OctantThreshold)
                pressed |= Buttons.DPadDown;

            if (attackHeld)
                pressed |= Buttons.A;
            if (jumpHeld)
                pressed |= Buttons.B;
            if (dashHeld)
                pressed |= Buttons.RightShoulder;

            foreach (Buttons button in PassThroughButtons)
            {
                if (physical.IsButtonDown(button))
                    pressed |= button;
            }

            return new GamePadState(physical.ThumbSticks, physical.Triggers, new GamePadButtons(pressed), physical.DPad);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            bool steering = stickTouchId >= 0;
            Vector2 center = steering ? stickOrigin : stickHome;
            float stickAlpha = steering ? ActiveOpacity : IdleOpacity;

            DrawDisc(spriteBatch, center, StickRadius, Color.Black * (stickAlpha * 0.45f));
            DrawDisc(spriteBatch, center, StickRadius - 5f, Color.White * (stickAlpha * 0.22f));

            Vector2 knob = steering
                ? center + LimitToStickRadius(stickPosition - center)
                : center;
            DrawDisc(spriteBatch, knob, 24f, Color.White * stickAlpha);

            DrawButton(spriteBatch, attackCenter, AttackRadius, "A", attackHeld);
            DrawButton(spriteBatch, jumpCenter, JumpRadius, "P", jumpHeld);
            DrawButton(spriteBatch, dashCenter, DashRadius, "D", dashHeld);
        }

        private void DrawButton(SpriteBatch spriteBatch, Vector2 center, float radius, string label, bool held)
        {
            float alpha = held ? ActiveOpacity : IdleOpacity;

            DrawDisc(spriteBatch, center, radius, Color.Black * (alpha * 0.5f));
            DrawDisc(spriteBatch, center, radius - 4f, Color.White * (alpha * 0.28f));

            if (font == null)
                return;

            Vector2 size = font.MeasureString(label);
            spriteBatch.DrawString(font, label, center - size * 0.5f, Color.White * alpha);
        }

        private void DrawDisc(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
        {
            float scale = radius * 2f / DiscSize;
            spriteBatch.Draw(disc, center, null, color, 0f, discOrigin, scale, SpriteEffects.None, 0f);
        }

        private static Vector2 LimitToStickRadius(Vector2 offset)
        {
            float lengthSquared = offset.LengthSquared();

            if (lengthSquared <= StickRadius * StickRadius)
                return offset;

            return offset * (StickRadius / (float)Math.Sqrt(lengthSquared));
        }

        private static bool IsInside(Vector2 point, Vector2 center, float radius)
        {
            return Vector2.DistanceSquared(point, center) <= radius * radius;
        }

        /// <summary>
        /// Builds the one texture every control is drawn from, so the controls need no art in the
        /// content pipeline. Colors are premultiplied to match the default alpha blending.
        /// </summary>
        private static Texture2D CreateDisc(GraphicsDevice graphicsDevice, int diameter)
        {
            var pixels = new Color[diameter * diameter];
            float radius = diameter / 2f;

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x - radius + 0.5f;
                    float dy = y - radius + 0.5f;
                    float coverage = MathHelper.Clamp(radius - (float)Math.Sqrt(dx * dx + dy * dy), 0f, 1f);
                    pixels[y * diameter + x] = Color.White * coverage;
                }
            }

            var texture = new Texture2D(graphicsDevice, diameter, diameter);
            texture.SetData(pixels);
            return texture;
        }

        public void Dispose()
        {
            disc?.Dispose();
        }
    }
}
