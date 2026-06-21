using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Drives the per-state animation of a <see cref="Fighter"/>.
    /// <para>
    /// When real sprite strips exist under <c>Content/Sprites/&lt;set&gt;/</c> they are
    /// played through the shared <see cref="AnimationPlayer"/>. Until the art from
    /// <c>Curitiba.Art</c> is sliced into those strips, a readable coloured placeholder
    /// is drawn instead, so the demo stays fully playable. Dropping the PNGs in and
    /// registering them in the content pipeline upgrades the visuals with no gameplay
    /// change.
    /// </para>
    /// </summary>
    internal class FighterAnimator
    {
        private readonly Texture2D blank;
        private readonly Color baseColor;
        private readonly Dictionary<FighterState, Animation> animations = new Dictionary<FighterState, Animation>();
        private FighterState currentState = FighterState.Idle;
        private int frameIndex;
        private float frameTimer;

        // Sprite frames can be authored at any square size (64, 96, 128…); these decouple
        // the source resolution from the on-screen size and put the feet on the ground.
        // Tune these if the character looks too big/small or floats above the floor.
        private const float TargetRenderHeight = 116f; // on-screen height of a full frame, in virtual px
        private const float FootAnchor = 0.93f;         // fraction of the frame where the feet sit

        /// <summary>True when at least the idle strip was found and real sprites are in use.</summary>
        public bool HasSprites { get; }

        public FighterAnimator(ContentManager content, Texture2D blank, string spriteSet,
                               Color baseColor, IReadOnlyDictionary<FighterState, string> assetNames)
        {
            this.blank = blank;
            this.baseColor = baseColor;

            foreach (var pair in assetNames)
            {
                var animation = TryLoad(content, "Sprites/" + spriteSet + "/" + pair.Value, pair.Key);
                if (animation != null)
                    animations[pair.Key] = animation;
            }

            HasSprites = animations.ContainsKey(FighterState.Idle);
        }

        private static Animation TryLoad(ContentManager content, string assetName, FighterState state)
        {
            try
            {
                var texture = content.Load<Texture2D>(assetName);
                return new Animation(texture, FrameTimeFor(state), IsLooping(state));
            }
            catch (ContentLoadException)
            {
                // Strip not sliced/registered yet: fall back to the placeholder.
                return null;
            }
        }

        private static float FrameTimeFor(FighterState state) => state switch
        {
            FighterState.Walk => 0.10f,
            FighterState.Attack => 0.06f,
            FighterState.Attack2 => 0.06f,
            FighterState.Jump => 0.10f,
            FighterState.JumpAttack => 0.06f,
            FighterState.Hit => 0.08f,
            _ => 0.12f,
        };

        private static bool IsLooping(FighterState state) =>
            state == FighterState.Idle || state == FighterState.Walk;

        /// <summary>Switches the active animation when the fighter changes state.</summary>
        public void SetState(FighterState state)
        {
            if (state == currentState)
                return;

            currentState = state;
            frameIndex = 0;
            frameTimer = 0f;
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch, Vector2 position, FaceDirection facing)
        {
            var effects = facing == FaceDirection.Left ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            if (HasSprites && animations.TryGetValue(currentState, out var animation))
            {
                AdvanceFrame(gameTime, animation);

                int frameSize = animation.FrameHeight;
                var source = new Rectangle(frameIndex * frameSize, 0, frameSize, frameSize);
                float scale = TargetRenderHeight / frameSize;
                // Anchor on the feet (not the bottom edge of the frame) so the sprite stands on the ground.
                var origin = new Vector2(frameSize / 2f, frameSize * FootAnchor);

                spriteBatch.Draw(animation.Texture, position, source, Color.White, 0f, origin, scale, effects, 0f);
                return;
            }

            DrawPlaceholder(spriteBatch, position, facing);
        }

        /// <summary>
        /// Draws a flattened shadow on the ground at the fighter's feet, used while the
        /// fighter is airborne to read the jump height and the landing spot.
        /// </summary>
        public void DrawShadow(SpriteBatch spriteBatch, Vector2 footPosition, int bodyWidth)
        {
            const int height = 10;
            int width = bodyWidth;
            var rectangle = new Rectangle(
                (int)(footPosition.X - width / 2f),
                (int)(footPosition.Y - height / 2f),
                width, height);
            DrawRect(spriteBatch, rectangle, new Color(0, 0, 0, 90));
        }

        /// <summary>Advances the current frame using the animation's own frame time and looping.</summary>
        private void AdvanceFrame(GameTime gameTime, Animation animation)
        {
            frameTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            while (frameTimer > animation.FrameTime)
            {
                frameTimer -= animation.FrameTime;

                if (animation.IsLooping)
                    frameIndex = (frameIndex + 1) % animation.FrameCount;
                else
                    frameIndex = Math.Min(frameIndex + 1, animation.FrameCount - 1);
            }
        }

        private void DrawRect(SpriteBatch spriteBatch, Rectangle rectangle, Color color)
        {
            spriteBatch.Draw(blank, rectangle, color);
        }

        /// <summary>
        /// Draws a simple, state-readable stand-in for the fighter: a body whose
        /// posture reflects the current action (an extended arm during an attack,
        /// a white flash when hit, a flattened shape when knocked down).
        /// </summary>
        private void DrawPlaceholder(SpriteBatch spriteBatch, Vector2 position, FaceDirection facing)
        {
            int px = (int)position.X;
            int py = (int)position.Y;
            int dir = facing == FaceDirection.Right ? 1 : -1;

            Color skin = new Color(214, 162, 124);
            Color outline = new Color(30, 25, 30);

            if (currentState == FighterState.Dead || currentState == FighterState.KnockedDown)
            {
                const int w = 70, h = 22;
                DrawRect(spriteBatch, new Rectangle(px - w / 2, py - h, w, h), Color.Lerp(baseColor, Color.Gray, 0.5f));
                int headX = dir > 0 ? px + w / 2 - 18 : px - w / 2 + 2;
                DrawRect(spriteBatch, new Rectangle(headX, py - h - 14, 16, 16), skin);
                return;
            }

            const int bw = 30, bh = 46, legH = 16;
            int torsoTop = py - legH - bh;

            // Legs.
            DrawRect(spriteBatch, new Rectangle(px - 12, py - legH, 9, legH), outline);
            DrawRect(spriteBatch, new Rectangle(px + 3, py - legH, 9, legH), outline);

            // Torso (flashes white while taking a hit).
            Color torso = currentState == FighterState.Hit ? Color.White : baseColor;
            DrawRect(spriteBatch, new Rectangle(px - bw / 2, torsoTop, bw, bh), torso);

            // Head + facing eye.
            const int hs = 20;
            DrawRect(spriteBatch, new Rectangle(px - hs / 2, torsoTop - hs, hs, hs), skin);
            DrawRect(spriteBatch, new Rectangle(px - 1 + dir * 4, torsoTop - hs + 7, 3, 3), outline);

            if (currentState == FighterState.Attack || currentState == FighterState.Attack2
                || currentState == FighterState.JumpAttack)
            {
                const int armW = 26, armH = 9;
                int armX = dir > 0 ? px + bw / 2 - 2 : px - bw / 2 - armW + 2;
                DrawRect(spriteBatch, new Rectangle(armX, torsoTop + 12, armW, armH), Color.Lerp(baseColor, Color.White, 0.25f));
                int fistX = dir > 0 ? armX + armW - 6 : armX;
                DrawRect(spriteBatch, new Rectangle(fistX, torsoTop + 9, 8, 14), skin);
            }
            else
            {
                const int armW = 8, armH = 24;
                int armX = dir > 0 ? px + bw / 2 - 2 : px - bw / 2 - armW + 2;
                DrawRect(spriteBatch, new Rectangle(armX, torsoTop + 10, armW, armH), Color.Lerp(baseColor, outline, 0.2f));
            }
        }
    }
}
