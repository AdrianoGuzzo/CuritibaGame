using System;
using System.Collections.Generic;
using Curitiba.Core.Inputs;
using Curitiba.Core.Localization;
using Curitiba.ScreenManagers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// A single beat 'em up session: the Capão Raso stage. Owns Sofia, the live
    /// enemies, the scrolling camera and the wave/area progression. Roughly the
    /// beat 'em up analogue of the platformer's <c>Level</c>.
    /// </summary>
    internal class CapaoRasoArena
    {
        // Advancing: scrolling toward the next wave's lock point (scroll sections only).
        // Fighting: a wave is live and the player is penned in. ExitReady: section cleared,
        // waiting for the player to reach the right edge to load the next section.
        private enum ArenaPhase { Advancing, Fighting, ExitReady }

        // Stage geometry, in the fixed 800x480 virtual space. The horizontal extent is now
        // per-section (see sectionWidth); only the vertical corridor is shared by all sections.
        private const float CorridorTop = 300f;
        private const float CorridorBottom = 448f;

        // Curb/step: how many pixels the sidewalk sits above the asphalt (visual only). Tunável.
        private const float CurbHeight = 14f;

        // Background composition (tunable: ajuste a arte pela tela aqui, sem reexportar PNG).
        private const float HorizonY = 300f;          // onde a rua/calçada começa
        private const float SkyScroll = 0.2f;         // parallax lento do céu
        private const float BuildingsScroll = 0.5f;   // parallax médio dos prédios
        private const int BuildingsHeight = 360;      // altura na tela dos prédios; base ancorada no horizonte

        private readonly ScreenManager screenManager;
        private readonly ContentManager content;
        private readonly Texture2D blank;
        private readonly Texture2D sky;          // céu, tileável (null => fallback de cor); backdrop em parallax
        private readonly Texture2D buildings;    // prédios de fundo, transparente, tileável; backdrop em parallax
        private readonly SpriteFont font;
        private readonly float viewWidth;

        private readonly SofiaPlayer sofia;
        private readonly List<PiaLocoEnemy> enemies = new List<PiaLocoEnemy>();
        private readonly List<Fighter> drawOrder = new List<Fighter>();
        private readonly StageSection[] sections;
        private Camera2D camera;             // recreated per section (immutable world bounds)

        private ArenaPhase phase = ArenaPhase.Advancing;
        private int currentSection;
        private int currentWave;             // wave index within the current section
        private float sectionWidth;          // cached width of the current section's world
        private float exitBlink;             // accumulator for the blinking "AVANÇAR" exit cue
        private int defeatedCount;           // session-wide stat; not reset between sections
        private float defeatTimer;

        private static readonly Comparison<Fighter> ByDepth = (a, b) => a.Position.Y.CompareTo(b.Position.Y);

        /// <summary>True once Sofia has reached the end of the stage (the "Fim da Demo" trigger).</summary>
        public bool Completed { get; private set; }

        /// <summary>True once Sofia has been knocked down and stayed down.</summary>
        public bool PlayerDefeated { get; private set; }

        public CapaoRasoArena(ScreenManager screenManager, ContentManager content)
        {
            this.screenManager = screenManager;
            this.content = content;
            this.blank = content.Load<Texture2D>("Sprites/blank");
            // Backdrop em parallax do Stage 1; ausentes => null e cai no fundo de cor chapada.
            this.sky = TryLoadTexture(content, "Backgrounds/Stage1/Sky");
            this.buildings = TryLoadTexture(content, "Backgrounds/Stage1/Buildings");
            this.font = screenManager.Font;
            this.viewWidth = screenManager.BaseScreenSize.X;

            sofia = new SofiaPlayer(content, blank);

            // A hybrid stage: each section's mode (scroll vs frame) is decided automatically from
            // the real width of its background image; missing art falls back to a placeholder width.
            sections = new[]
            {
                // Section 0: the condomínio entrance. Its image scales to one screen (800px) =>
                // a no-scroll FRAME, bounded by the art. Parallax sky/buildings sit behind it
                // (the image has a transparent sky). Two escalating waves are fought in place.
                new StageSection("Backgrounds/Stage1/Gate", fallbackWidth: 800f, waves: new[]
                {
                    new SpawnArea(0f, 2, hitsToKnockdown: 3),
                    new SpawnArea(0f, 3, hitsToKnockdown: 4),
                }, parallaxBackdrop: true,
                    // Calçada elevada atrás, asfalto à frente; a baixada do portão (saída de carros)
                    // é uma rampa de passagem livre. Valores tunáveis — alinhe à arte rodando o jogo.
                    curbY: 325f, drivewayLeft: 320f, drivewayRight: 600f),

                // Section 1: a scrolling corridor built from a horizontally tileable wall, repeated
                // 3x side by side => one continuous ~3-screen scene (width comes from the tile x3).
                // Falls back to the placeholder width/parallax only while the art is missing.
                new StageSection("Backgrounds/Stage1/WallInfinite", fallbackWidth: 1600f, waves: new[]
                {
                    new SpawnArea(400f, 3, hitsToKnockdown: 4),
                    new SpawnArea(1000f, 4, hitsToKnockdown: 5),
                }, parallaxBackdrop: true, repeatX: 3,
                    // Muro corrido: calçada elevada atrás, asfalto à frente, com degrau no trecho
                    // inteiro (sem garagem => sem rampa de passagem). curbY é tunável pela arte.
                    curbY: 335f),
            };

            LoadSection(0);
        }

        /// <summary>
        /// Loads a section: resolves its background and width, recreates the camera with the
        /// section's world bounds, places Sofia at the left, and primes the first wave. Frame
        /// sections (width &lt;= viewport) get their wave immediately; scroll sections start
        /// advancing toward the first lock point.
        /// </summary>
        private void LoadSection(int index)
        {
            currentSection = index;
            StageSection s = sections[index];

            s.Background = s.BackgroundAsset != null ? TryLoadTexture(content, s.BackgroundAsset) : null;
            s.Width = ResolveWidth(s);

            sectionWidth = s.Width;
            camera = new Camera2D(viewWidth, sectionWidth);

            enemies.Clear();
            currentWave = 0;
            exitBlink = 0f;

            sofia.Position = new Vector2(90f, (CorridorTop + CorridorBottom) / 2f);
            camera.Follow(sofia.Position); // snap before the first draw to avoid a one-frame jump

            if (s.Mode == SectionMode.Frame)
            {
                SpawnWave(s.Waves[0]);
                phase = ArenaPhase.Fighting;
            }
            else
            {
                camera.MaxAdvanceX = s.Waves[0].LockCameraX;
                phase = ArenaPhase.Advancing;
            }
        }

        /// <summary>
        /// The section's world width. An image is scaled to screen height so the camera respects its
        /// real extent; a tileable background (<see cref="StageSection.RepeatX"/> &gt; 1) is that many
        /// tiles wide. Only when the art is missing do we fall back to the configured width.
        /// </summary>
        private float ResolveWidth(StageSection s)
        {
            if (s.Background != null)
            {
                float sceneH = screenManager.BaseScreenSize.Y;
                float tileW = (float)Math.Round(s.Background.Width * (sceneH / s.Background.Height));
                return tileW * s.RepeatX;
            }
            return s.FallbackWidth;
        }

        /// <summary>Loads a texture, returning null if it is not registered/built yet (graceful fallback).</summary>
        private static Texture2D TryLoadTexture(ContentManager content, string assetName)
        {
            try
            {
                return content.Load<Texture2D>(assetName);
            }
            catch (ContentLoadException)
            {
                return null;
            }
        }

        public void Update(GameTime gameTime, InputState input, PlayerIndex? controllingPlayer)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            sofia.HandleInput(input, controllingPlayer);
            sofia.Update(gameTime);
            ApplyCurb(sofia);
            ClampToScreen(sofia);

            foreach (var enemy in enemies)
            {
                enemy.Update(gameTime);
                ApplyCurb(enemy);
                ClampToWorld(enemy);
            }

            ResolveCombat();

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i].IsExpired)
                    enemies.RemoveAt(i);
            }

            camera.Follow(sofia.Position);
            UpdatePhase(dt);

            // Give the knockdown a beat to read before declaring defeat.
            if (sofia.IsDefeated)
            {
                defeatTimer += dt;
                if (defeatTimer >= 1.2f)
                    PlayerDefeated = true;
            }
        }

        private void UpdatePhase(float dt)
        {
            SpawnArea[] waves = sections[currentSection].Waves;

            switch (phase)
            {
                case ArenaPhase.Advancing: // scroll sections: roll to the wave's lock point, then spawn
                    if (camera.X >= waves[currentWave].LockCameraX - 1f)
                    {
                        camera.MaxAdvanceX = waves[currentWave].LockCameraX;
                        SpawnWave(waves[currentWave]);
                        phase = ArenaPhase.Fighting;
                    }
                    break;

                case ArenaPhase.Fighting:
                    if (enemies.Count == 0)
                    {
                        currentWave++;
                        if (currentWave < waves.Length)
                        {
                            // More waves in this section: scroll on to the next, or spawn at once.
                            if (sections[currentSection].Mode == SectionMode.Scroll)
                            {
                                camera.MaxAdvanceX = waves[currentWave].LockCameraX;
                                phase = ArenaPhase.Advancing;
                            }
                            else
                            {
                                SpawnWave(waves[currentWave]);
                            }
                        }
                        else
                        {
                            // Section cleared: open the way to the right edge / the exit.
                            camera.MaxAdvanceX = sectionWidth - viewWidth;
                            phase = ArenaPhase.ExitReady;
                        }
                    }
                    break;

                case ArenaPhase.ExitReady:
                    exitBlink += dt;
                    if (sofia.Position.X >= sectionWidth - 60f)
                    {
                        if (currentSection + 1 < sections.Length)
                            LoadSection(currentSection + 1);
                        else
                            Completed = true;
                    }
                    break;
            }
        }

        private void SpawnWave(SpawnArea area)
        {
            // Spawn span scales with the section so a narrow frame doesn't push enemies off-screen.
            float right = Math.Min(camera.Right, sectionWidth);
            float spanLeft = MathHelper.Lerp(camera.Left, right, 0.55f);
            float spanRight = right - 40f;

            int count = area.EnemyCount;
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float x = MathHelper.Lerp(spanLeft, spanRight, t);
                float y = MathHelper.Lerp(CorridorTop + 12f, CorridorBottom - 6f, (i % 2 == 0) ? 0.3f : 0.78f);
                enemies.Add(new PiaLocoEnemy(content, blank, new Vector2(x, y), sofia, area.HitsToKnockdown));
            }
        }

        private void ResolveCombat()
        {
            // Sofia's swing against every enemy (once per swing per enemy).
            if (sofia.CurrentAttack.HasValue)
            {
                AttackData attack = sofia.CurrentAttack.Value;
                foreach (var enemy in enemies)
                {
                    if (!enemy.IsAlive || sofia.AttackHitTargets.Contains(enemy))
                        continue;

                    if (attack.Hitbox.Intersects(enemy.HurtBox))
                    {
                        enemy.TakeDamage(attack.Damage, attack.Knockback);
                        sofia.AttackHitTargets.Add(enemy);
                        if (enemy.IsDefeated)
                            defeatedCount++;
                    }
                }
            }

            // Enemy swings against Sofia.
            if (sofia.IsAlive && !sofia.IsInvulnerable)
            {
                foreach (var enemy in enemies)
                {
                    if (!enemy.CurrentAttack.HasValue || enemy.AttackHitTargets.Contains(sofia))
                        continue;

                    AttackData attack = enemy.CurrentAttack.Value;
                    if (attack.Hitbox.Intersects(sofia.HurtBox))
                    {
                        sofia.TakeDamage(attack.Damage, attack.Knockback);
                        enemy.AttackHitTargets.Add(sofia);
                        break; // one hit per frame is plenty
                    }
                }
            }
        }

        /// <summary>
        /// Keeps a fighter in the vertical corridor and resolves the curb/step. The sidewalk
        /// (Y &lt; CurbY) draws raised by <see cref="CurbHeight"/>; the asphalt is the floor.
        /// Walking off the front edge steps down smoothly; climbing back up the curb requires a
        /// jump for fighters that <see cref="Fighter.MustJumpCurb"/> (the player) — except at the
        /// gate driveway, a ramp where both floors connect. Enemies climb freely.
        /// </summary>
        private void ApplyCurb(Fighter fighter)
        {
            fighter.Position.Y = MathHelper.Clamp(fighter.Position.Y, CorridorTop, CorridorBottom);

            StageSection s = sections[currentSection];
            if (s.CurbY <= 0f)
            {
                fighter.GroundOffset = 0f;
                return;
            }

            // Sem garagem (DrivewayRight <= DrivewayLeft) => vão vazio, degrau contínuo no trecho todo.
            bool inDriveway = s.DrivewayRight > s.DrivewayLeft &&
                fighter.Position.X >= s.DrivewayLeft && fighter.Position.X <= s.DrivewayRight;

            // Entrada dos carros: rampa, sem degrau. A elevação desce suave da altura da calçada
            // (fundo, CorridorTop) até o nível do asfalto (CurbY em diante), então passa-se livre
            // e sem o "tranco" do meio-fio.
            if (inDriveway)
            {
                float span = s.CurbY - CorridorTop;
                float t = span > 0f ? MathHelper.Clamp((fighter.Position.Y - CorridorTop) / span, 0f, 1f) : 1f;
                fighter.GroundOffset = MathHelper.Lerp(CurbHeight, 0f, t);
                return;
            }

            bool wantsSidewalk = fighter.Position.Y < s.CurbY;
            bool onAsphaltNow = fighter.GroundOffset == 0f;
            bool grounded = !fighter.IsAirborne;

            // The only special case (the driveway already returned above): on the ground, a
            // must-jump fighter (Sofia) on the asphalt cannot walk up the step — penned at its base.
            if (grounded && fighter.MustJumpCurb && onAsphaltNow && wantsSidewalk)
            {
                fighter.Position.Y = s.CurbY;
                fighter.GroundOffset = 0f;
            }
            else
            {
                // Everything else resolves elevation by depth: stepping down is smooth, enemies
                // climb freely, the ramp passes through, and a jump lands on whichever floor it
                // crosses to (GroundOffset already matches Y when jumpHeight reaches 0).
                fighter.GroundOffset = wantsSidewalk ? CurbHeight : 0f;
            }
        }

        private void ClampToScreen(Fighter fighter)
        {
            float pad = fighter.BodyWidth / 2f;
            // Respect the real right edge of the loaded image (matters for frames narrower than
            // the viewport). While Fighting the camera is locked, so this also pens the player in.
            float right = Math.Min(camera.Right, sectionWidth) - pad;
            fighter.Position.X = MathHelper.Clamp(fighter.Position.X, camera.Left + pad, right);
        }

        private void ClampToWorld(Fighter fighter)
        {
            fighter.Position.X = MathHelper.Clamp(fighter.Position.X, 20f, sectionWidth - 20f);
        }

        // ----------------------------------------------------------------- Drawing

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            // Far/mid background with parallax (screen space) — only sections that opt in.
            if (sections[currentSection].ParallaxBackdrop)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, screenManager.GlobalTransformation);
                DrawBackground(spriteBatch);
                spriteBatch.End();
            }

            // World (camera space).
            Matrix world = camera.GetTransform() * screenManager.GlobalTransformation;
            spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, world);
            DrawGroundAndSetPieces(spriteBatch);

            drawOrder.Clear();
            drawOrder.AddRange(enemies);
            drawOrder.Add(sofia);
            drawOrder.Sort(ByDepth);
            foreach (var fighter in drawOrder)
                fighter.Draw(gameTime, spriteBatch);

            foreach (var enemy in enemies)
                DrawEnemyHealthBar(spriteBatch, enemy);

            spriteBatch.End();

            // HUD (screen space).
            spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, screenManager.GlobalTransformation);
            DrawHud(spriteBatch);
            spriteBatch.End();
        }

        private void DrawRect(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color)
        {
            spriteBatch.Draw(blank, new Rectangle(x, y, width, height), color);
        }

        private void DrawBackground(SpriteBatch spriteBatch)
        {
            int w = (int)screenManager.BaseScreenSize.X;
            int h = (int)screenManager.BaseScreenSize.Y;

            // Far: sky (tiled, slow parallax) or flat-colour fallback.
            if (sky != null)
                DrawParallaxBand(spriteBatch, sky, SkyScroll, 0, h);
            else
                DrawRect(spriteBatch, 0, 0, w, (int)HorizonY, new Color(126, 182, 220));

            // Mid: distant buildings (tiled, medium parallax) anchored at the horizon.
            if (buildings != null)
                DrawParallaxBand(spriteBatch, buildings, BuildingsScroll, (int)HorizonY - BuildingsHeight, BuildingsHeight);
            else
                DrawRect(spriteBatch, 0, 150, w, (int)HorizonY - 150, new Color(120, 116, 128));
        }

        /// <summary>
        /// Tiles a texture horizontally across the view, scaled to <paramref name="destHeight"/>
        /// (preserving aspect) and offset by the camera scaled by <paramref name="scrollFactor"/>.
        /// Drawn in screen space, so callers wrap it in a GlobalTransformation batch.
        /// </summary>
        private void DrawParallaxBand(SpriteBatch spriteBatch, Texture2D texture, float scrollFactor, int destY, int destHeight)
        {
            int viewW = (int)screenManager.BaseScreenSize.X;
            int tileW = (int)Math.Ceiling(texture.Width * (destHeight / (float)texture.Height));
            if (tileW <= 0)
                return;

            float offset = -(camera.X * scrollFactor) % tileW;
            if (offset > 0f)
                offset -= tileW;

            for (float x = offset; x < viewW; x += tileW)
                spriteBatch.Draw(texture, new Rectangle((int)Math.Round(x), destY, tileW, destHeight), Color.White);
        }

        private void DrawGroundAndSetPieces(SpriteBatch spriteBatch)
        {
            int h = (int)screenManager.BaseScreenSize.Y;
            int w = (int)sectionWidth;
            StageSection s = sections[currentSection];

            if (s.Background != null)
            {
                // The image is scaled to screen height; its (tiled) width matches the camera's world
                // bounds, so it aligns pixel-for-pixel with the section limits. A tileable background
                // (RepeatX > 1) is drawn that many times side by side — w/RepeatX divides evenly since
                // the world width was built as tileW * RepeatX, so there is no seam or gap on the right.
                int tileW = w / s.RepeatX;
                for (int i = 0; i < s.RepeatX; i++)
                    spriteBatch.Draw(s.Background, new Rectangle(i * tileW, 0, tileW, h), Color.White);
            }
            else
            {
                // No foreground image: a ground band across the section. The sky comes from the
                // parallax backdrop when present; otherwise draw a flat sky band too (fallback).
                if (!s.ParallaxBackdrop)
                    DrawRect(spriteBatch, 0, 0, w, (int)HorizonY, new Color(126, 182, 220));
                DrawRect(spriteBatch, 0, (int)HorizonY, w, h - (int)HorizonY, new Color(96, 96, 102));
                DrawRect(spriteBatch, 0, (int)HorizonY, w, 4, new Color(60, 60, 66)); // curb line
            }
        }

        private void DrawEnemyHealthBar(SpriteBatch spriteBatch, PiaLocoEnemy enemy)
        {
            if (!enemy.IsAlive)
                return;

            const int barWidth = 40, barHeight = 5;
            int x = (int)(enemy.Position.X - barWidth / 2f);
            int y = (int)(enemy.Position.Y - enemy.BodyHeight - 12);
            int fill = (int)(barWidth * (enemy.Health / (float)enemy.MaxHealth));

            DrawRect(spriteBatch, x - 1, y - 1, barWidth + 2, barHeight + 2, new Color(0, 0, 0, 160));
            DrawRect(spriteBatch, x, y, barWidth, barHeight, new Color(70, 20, 20));
            DrawRect(spriteBatch, x, y, fill, barHeight, new Color(214, 64, 48));
        }

        private void DrawHud(SpriteBatch spriteBatch)
        {
            // Stage name.
            DrawShadowedString(spriteBatch, Resources.StageCapaoRaso, new Vector2(20f, 12f), Color.White);

            // Sofia's health bar.
            const int barWidth = 180, barHeight = 16;
            int barX = 20, barY = 42;
            int fill = (int)(barWidth * (sofia.Health / (float)sofia.MaxHealth));
            DrawRect(spriteBatch, barX - 2, barY - 2, barWidth + 4, barHeight + 4, new Color(0, 0, 0, 180));
            DrawRect(spriteBatch, barX, barY, barWidth, barHeight, new Color(60, 24, 24));
            DrawRect(spriteBatch, barX, barY, fill, barHeight, new Color(70, 200, 90));

            // Defeated count (top-right).
            string defeated = Resources.Defeated + ": " + defeatedCount;
            Vector2 size = font.MeasureString(defeated);
            DrawShadowedString(spriteBatch, defeated, new Vector2(screenManager.BaseScreenSize.X - size.X - 20f, 12f), Color.White);

            DrawExitCue(spriteBatch);
        }

        /// <summary>
        /// Temporary exit indicator: once the section is cleared and another one follows, a blinking
        /// "AVANÇAR" cue with a chevron appears at the right edge. Placeholder for a future sprite.
        /// </summary>
        private void DrawExitCue(SpriteBatch spriteBatch)
        {
            if (phase != ArenaPhase.ExitReady || currentSection + 1 >= sections.Length)
                return;

            if ((int)(exitBlink * 2f) % 2 != 0) // ~1 Hz blink (on/off each half-second)
                return;

            string txt = Resources.Advance;
            Vector2 size = font.MeasureString(txt);
            float cx = screenManager.BaseScreenSize.X - 30f;
            float midY = (CorridorTop + CorridorBottom) / 2f;
            DrawShadowedString(spriteBatch, txt, new Vector2(cx - size.X, midY - size.Y / 2f), new Color(255, 224, 64));

            // Simple chevron made of two stacked bars, above the text.
            int ax = (int)(cx - 16f);
            int ay = (int)(midY - size.Y / 2f - 16f);
            DrawRect(spriteBatch, ax, ay, 18, 5, new Color(255, 224, 64));
            DrawRect(spriteBatch, ax, ay + 6, 18, 5, new Color(255, 224, 64));
        }

        private void DrawShadowedString(SpriteBatch spriteBatch, string text, Vector2 position, Color color)
        {
            spriteBatch.DrawString(font, text, position + new Vector2(1f, 1f), Color.Black);
            spriteBatch.DrawString(font, text, position, color);
        }
    }
}
