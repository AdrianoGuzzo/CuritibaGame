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
        // Optional per-phase hop strips (Sofia). When present and the fighter is in the Jump
        // state, the strip for the current JumpPhase is drawn instead of the single Jump strip.
        private readonly Dictionary<JumpPhase, Animation> jumpPhases = new Dictionary<JumpPhase, Animation>();
        // Variações da reação a dano (Hit, Hit2, Hit3…). Quando há mais de uma, uma é sorteada a
        // cada nova entrada no estado Hit, dando variedade visual ao apanhar. Fica vazia para
        // fighters sem essas tiras (ex.: PiaLoco), preservando o comportamento de strip única.
        private readonly List<Animation> hitVariants = new List<Animation>();
        private int currentHitVariant;
        private FighterState currentState = FighterState.Idle;
        private JumpPhase currentJumpPhase = JumpPhase.Start;
        private int frameIndex;
        private float frameTimer;

        // Sprite frames can be authored at any square size (64, 96, 128…); these decouple
        // the source resolution from the on-screen size and put the feet on the ground.
        // Tune these if the character looks too big/small or floats above the floor.
        private const float TargetRenderHeight = 116f; // on-screen height of a full frame, in virtual px
        private const float FootAnchor = 0.93f;         // fraction of the frame where the feet sit

        // Tiras extras da reação a dano, carregadas por convenção (além da base "Hit").
        private static readonly string[] HitVariantSuffixes = { "Hit2", "Hit3", "Hit4" };

        /// <summary>True when at least the idle strip was found and real sprites are in use.</summary>
        public bool HasSprites { get; }

        public FighterAnimator(ContentManager content, Texture2D blank, string spriteSet,
                               Color baseColor, IReadOnlyDictionary<FighterState, string> assetNames,
                               IReadOnlyDictionary<JumpPhase, string> jumpPhaseNames = null)
        {
            this.blank = blank;
            this.baseColor = baseColor;

            foreach (var pair in assetNames)
            {
                var animation = TryLoad(content, "Sprites/" + spriteSet + "/" + pair.Value, spriteSet, pair.Key);
                if (animation != null)
                    animations[pair.Key] = animation;
            }

            // Coleta as variações da reação a dano: a tira base Hit (se existir) vira a variação 0,
            // e Hit2/Hit3/Hit4 entram por convenção quando os PNGs estão registrados. Herdam o
            // frame time / largura / looping do estado Hit. Tiras ausentes são ignoradas.
            if (animations.TryGetValue(FighterState.Hit, out var baseHit))
                hitVariants.Add(baseHit);
            foreach (var suffix in HitVariantSuffixes)
            {
                var variant = TryLoad(content, "Sprites/" + spriteSet + "/" + suffix, spriteSet, FighterState.Hit);
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

            HasSprites = animations.ContainsKey(FighterState.Idle);
        }

        private static Animation TryLoad(ContentManager content, string assetName, string spriteSet, FighterState state)
        {
            try
            {
                var texture = content.Load<Texture2D>(assetName);
                return new Animation(texture, FrameTimeFor(state), IsLooping(state), FrameWidthFor(spriteSet, state));
            }
            catch (ContentLoadException)
            {
                // Strip not sliced/registered yet: fall back to the placeholder.
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

        // Per-phase frame time. The grounded windows (Start/Land) play quickly to fit their short
        // fixed duration; Rise/Fall play once and hold their last pose (they're cut short or held by
        // the physics, never desynced); Apex loops slowly so a floaty top reads well at any height.
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
            // 12 dash frames over the ~0.40s dash (Fighter.dashDuration) → the whole stretch slides
            // out together with the impulse, no leftover/missing frame.
            FighterState.Dash => 0.050f,
            FighterState.Attack => 0.06f,
            FighterState.Attack2 => 0.06f,
            FighterState.Attack3 => 0.06f,
            FighterState.Jump => 0.10f,
            FighterState.JumpAttack => 0.06f,
            FighterState.Hit => 0.08f,
            _ => 0.12f,
        };

        // States whose frames are authored wider than tall (FrameWidth != FrameHeight).
        // 0 = square (the frame width is derived from the strip height). The frame width is
        // per sprite-set because the same FighterState can be square for one fighter and wide
        // for another (Sofia's Attack is square 128px; the Mook's Punch reaches right at 171px).
        private static int FrameWidthFor(string spriteSet, FighterState state)
        {
            if (spriteSet == "PiaLoco" && state == FighterState.Attack)
                return 171;

            return state switch
            {
                FighterState.Dash => 178,
                // Sofia's air kick is authored wider than tall (176x128) to reach with the leg.
                FighterState.JumpAttack => 176,
                // Sofia's grounded kick finisher is authored wider than tall (206x135) for the leg reach.
                FighterState.Attack3 => 206,
                _ => 0,
            };
        }

        private static bool IsLooping(FighterState state) =>
            state == FighterState.Idle || state == FighterState.Walk;

        /// <summary>Switches the active animation when the fighter changes state.</summary>
        public void SetState(FighterState state)
        {
            if (state == currentState)
                return;

            // Só na transição real para Hit (não a cada frame): sorteia qual variação de reação a
            // dano será mostrada. Mantém a tira estável durante o stagger.
            if (state == FighterState.Hit && hitVariants.Count > 1)
                currentHitVariant = System.Random.Shared.Next(hitVariants.Count);

            currentState = state;
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

            // While hopping, prefer the per-phase strip (crouch/rise/apex/fall/land) when authored.
            if (currentState == FighterState.Jump && jumpPhases.TryGetValue(currentJumpPhase, out var phaseAnimation))
            {
                DrawStrip(gameTime, spriteBatch, position, effects, phaseAnimation);
                return;
            }

            // Reação a dano: desenha a variação sorteada (Hit/Hit2/Hit3/Hit4) em vez da strip fixa.
            if (HasSprites && currentState == FighterState.Hit && hitVariants.Count > 0)
            {
                DrawStrip(gameTime, spriteBatch, position, effects, hitVariants[currentHitVariant]);
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
            // Scale by height so a fighter keeps the same on-screen size regardless of frame
            // width (e.g. the wider dash frames don't make Sofia shrink).
            float scale = TargetRenderHeight / frameH;
            // Anchor on the feet (not the bottom edge of the frame) so the sprite stands on the ground.
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
                || currentState == FighterState.Attack3 || currentState == FighterState.JumpAttack)
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
