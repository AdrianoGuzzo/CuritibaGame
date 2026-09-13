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
        private readonly Dictionary<JumpPhase, Animation> jumpPhases = new Dictionary<JumpPhase, Animation>();
        private readonly List<Animation> hitVariants = new List<Animation>();
        private int currentHitVariant;
        private readonly Animation getUp;
        private bool rising;
        private FighterState currentState = FighterState.Idle;
        private JumpPhase currentJumpPhase = JumpPhase.Start;
        private int frameIndex;
        private float frameTimer;

        private const float BaseRenderHeight = 116f;
        private const float FootAnchor = 0.93f;

        /// <summary>On-screen height the strip is scaled to. <see cref="BaseRenderHeight"/> times the
        /// fighter's size multiplier, so a larger fighter draws bigger without re-exporting art.</summary>
        private readonly float renderHeight;

        private static readonly string[] HitVariantSuffixes = { "Hit2", "Hit3", "Hit4" };

        /// <summary>True when at least the idle strip was found and real sprites are in use.</summary>
        public bool HasSprites { get; }

        public FighterAnimator(ContentManager content, Texture2D blank, string spriteSet,
                               Color baseColor, IReadOnlyDictionary<FighterState, string> assetNames,
                               IReadOnlyDictionary<JumpPhase, string> jumpPhaseNames = null,
                               float renderScale = 1f)
        {
            this.blank = blank;
            this.baseColor = baseColor;
            renderHeight = BaseRenderHeight * (renderScale <= 0f ? 1f : renderScale);

            foreach (var pair in assetNames)
            {
                var animation = TryLoad(content, "Sprites/" + spriteSet + "/" + pair.Value, pair.Key);
                if (animation != null)
                    animations[pair.Key] = animation;
            }

            if (animations.TryGetValue(FighterState.Hit, out var baseHit))
                hitVariants.Add(baseHit);
            foreach (var suffix in HitVariantSuffixes)
            {
                var variant = TryLoad(content, "Sprites/" + spriteSet + "/" + suffix, FighterState.Hit);
                if (variant != null)
                    hitVariants.Add(variant);
            }

            if (jumpPhaseNames != null)
            {
                foreach (var pair in jumpPhaseNames)
                {
                    var animation = TryLoadJump(content, "Sprites/" + spriteSet + "/" + pair.Value, pair.Key);
                    if (animation != null)
                        jumpPhases[pair.Key] = animation;
                }
            }

            getUp = TryLoadGetUp(content, "Sprites/" + spriteSet + "/GetUp");

            HasSprites = animations.ContainsKey(FighterState.Idle);
        }

        private static Animation TryLoad(ContentManager content, string assetName, FighterState state)
        {
            try
            {
                var texture = content.Load<Texture2D>(assetName);
                return new Animation(texture, FrameTimeFor(state), IsLooping(state), FrameWidthFor(state));
            }
            catch (ContentLoadException)
            {
                return null;
            }
        }

        private static Animation TryLoadGetUp(ContentManager content, string assetName)
        {
            try
            {
                var texture = content.Load<Texture2D>(assetName);
                // Frame time fits the whole strip inside the get-up window (FighterTuning.GetUpDuration).
                return new Animation(texture, 0.055f, false);
            }
            catch (ContentLoadException)
            {
                return null;
            }
        }

        private static Animation TryLoadJump(ContentManager content, string assetName, JumpPhase phase)
        {
            try
            {
                var texture = content.Load<Texture2D>(assetName);
                return new Animation(texture, FrameTimeForJump(phase), IsLoopingJump(phase));
            }
            catch (ContentLoadException)
            {
                return null;
            }
        }

        private static float FrameTimeForJump(JumpPhase phase) => phase switch
        {
            JumpPhase.Start => 0.018f,
            JumpPhase.Rise => 0.045f,
            JumpPhase.Apex => 0.08f,
            JumpPhase.Fall => 0.045f,
            JumpPhase.Land => 0.020f,
            _ => 0.05f,
        };

        private static bool IsLoopingJump(JumpPhase phase) => phase == JumpPhase.Apex;

        private static float FrameTimeFor(FighterState state) => state switch
        {
            FighterState.Walk => 0.10f,
            FighterState.Dash => 0.050f,
            FighterState.Attack => 0.06f,
            FighterState.Attack2 => 0.06f,
            FighterState.Attack3 => 0.06f,
            FighterState.Jump => 0.10f,
            FighterState.JumpAttack => 0.06f,
            FighterState.Hit => 0.08f,
            FighterState.Thrown => 0.06f,
            _ => 0.12f,
        };

        private static int FrameWidthFor(FighterState state) => state switch
        {
            FighterState.Dash => 178,
            FighterState.JumpAttack => 176,
            FighterState.Attack3 => 206,
            _ => 0,
        };

        private static bool IsLooping(FighterState state) =>
            state == FighterState.Idle || state == FighterState.Walk;

        /// <summary>Switches the active animation when the fighter changes state.</summary>
        public void SetState(FighterState state)
        {
            if (state == currentState)
                return;

            rising = false;

            if (state == FighterState.Hit && hitVariants.Count > 1)
                currentHitVariant = System.Random.Shared.Next(hitVariants.Count);

            currentState = state;
            frameIndex = 0;
            frameTimer = 0f;
        }

        /// <summary>Enters/exits the knockdown get-up sub-phase (restarts the GetUp strip on the transition).</summary>
        public void SetRising(bool value)
        {
            if (rising == value)
                return;

            rising = value;
            frameIndex = 0;
            frameTimer = 0f;
        }

        /// <summary>
        /// Forces the strip for <paramref name="state"/> to play from frame 0, even when it is
        /// already the current state. Used when a new swing reuses the previous swing's state
        /// (e.g. Sofia's back-to-back Attack punches) so the animation replays instead of holding
        /// the previous swing's final frame.
        /// </summary>
        public void Restart(FighterState state)
        {
            currentState = state;
            frameIndex = 0;
            frameTimer = 0f;
        }

        /// <summary>Switches the active hop sub-phase strip (restarting it), while in the Jump state.</summary>
        public void SetJumpPhase(JumpPhase phase)
        {
            if (phase == currentJumpPhase)
                return;

            currentJumpPhase = phase;
            frameIndex = 0;
            frameTimer = 0f;
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch, Vector2 position, FaceDirection facing)
        {
            var effects = facing == FaceDirection.Left ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            if (currentState == FighterState.Jump && jumpPhases.TryGetValue(currentJumpPhase, out var phaseAnimation))
            {
                DrawStrip(gameTime, spriteBatch, position, effects, phaseAnimation);
                return;
            }

            if (HasSprites && currentState == FighterState.Hit && hitVariants.Count > 0)
            {
                DrawStrip(gameTime, spriteBatch, position, effects, hitVariants[currentHitVariant]);
                return;
            }

            if (currentState == FighterState.KnockedDown && rising && getUp != null)
            {
                DrawStrip(gameTime, spriteBatch, position, effects, getUp);
                return;
            }

            if (HasSprites && animations.TryGetValue(currentState, out var animation))
            {
                DrawStrip(gameTime, spriteBatch, position, effects, animation);
                return;
            }

            DrawPlaceholder(spriteBatch, position, facing);
        }

        /// <summary>Advances and draws one frame of a strip, scaled by height and anchored on the feet.</summary>
        private void DrawStrip(GameTime gameTime, SpriteBatch spriteBatch, Vector2 position,
                               SpriteEffects effects, Animation animation)
        {
            AdvanceFrame(gameTime, animation);

            int frameW = animation.FrameWidth;
            int frameH = animation.FrameHeight;
            var source = new Rectangle(frameIndex * frameW, 0, frameW, frameH);
            float scale = renderHeight / frameH;
            var origin = new Vector2(frameW / 2f, frameH * FootAnchor);

            spriteBatch.Draw(animation.Texture, position, source, Color.White, 0f, origin, scale, effects, 0f);
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

            // Scale every dimension/offset by the same factor the real sprites use, so the
            // placeholder grows or shrinks with the fighter's size multiplier too.
            float s = renderHeight / BaseRenderHeight;
            int S(int v) => (int)(v * s);

            Color skin = new Color(214, 162, 124);
            Color outline = new Color(30, 25, 30);

            if (currentState == FighterState.Dead || currentState == FighterState.KnockedDown)
            {
                int w = S(70), h = S(22);
                DrawRect(spriteBatch, new Rectangle(px - w / 2, py - h, w, h), Color.Lerp(baseColor, Color.Gray, 0.5f));
                int headSize = S(16);
                int headX = dir > 0 ? px + w / 2 - S(18) : px - w / 2 + S(2);
                DrawRect(spriteBatch, new Rectangle(headX, py - h - S(14), headSize, headSize), skin);
                return;
            }

            int bw = S(30), bh = S(46), legH = S(16);
            int torsoTop = py - legH - bh;

            int legW = S(9);
            DrawRect(spriteBatch, new Rectangle(px - S(12), py - legH, legW, legH), outline);
            DrawRect(spriteBatch, new Rectangle(px + S(3), py - legH, legW, legH), outline);

            Color torso = currentState == FighterState.Hit ? Color.White : baseColor;
            DrawRect(spriteBatch, new Rectangle(px - bw / 2, torsoTop, bw, bh), torso);

            int hs = S(20);
            DrawRect(spriteBatch, new Rectangle(px - hs / 2, torsoTop - hs, hs, hs), skin);
            int eye = S(3);
            DrawRect(spriteBatch, new Rectangle(px - 1 + dir * S(4), torsoTop - hs + S(7), eye, eye), outline);

            if (currentState == FighterState.Attack || currentState == FighterState.Attack2
                || currentState == FighterState.Attack3 || currentState == FighterState.JumpAttack)
            {
                int armW = S(26), armH = S(9);
                int armX = dir > 0 ? px + bw / 2 - S(2) : px - bw / 2 - armW + S(2);
                DrawRect(spriteBatch, new Rectangle(armX, torsoTop + S(12), armW, armH), Color.Lerp(baseColor, Color.White, 0.25f));
                int fistX = dir > 0 ? armX + armW - S(6) : armX;
                DrawRect(spriteBatch, new Rectangle(fistX, torsoTop + S(9), S(8), S(14)), skin);
            }
            else
            {
                int armW = S(8), armH = S(24);
                int armX = dir > 0 ? px + bw / 2 - S(2) : px - bw / 2 - armW + S(2);
                DrawRect(spriteBatch, new Rectangle(armX, torsoTop + S(10), armW, armH), Color.Lerp(baseColor, outline, 0.2f));
            }
        }
    }
}
