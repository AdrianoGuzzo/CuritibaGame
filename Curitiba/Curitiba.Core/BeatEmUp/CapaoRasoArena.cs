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
        private enum ArenaPhase { Advancing, Fighting, Cleared }

        // Stage geometry, in the fixed 800x480 virtual space.
        private const float WorldWidth = 800f * 4f;
        private const float CorridorTop = 300f;
        private const float CorridorBottom = 448f;

        // Background composition (tunable: ajuste a arte pela tela aqui, sem reexportar PNG).
        private const float HorizonY = 300f;          // onde a rua/calçada começa
        private const float SkyScroll = 0.2f;         // parallax lento do céu
        private const float BuildingsScroll = 0.5f;   // parallax médio dos prédios
        private const int BuildingsHeight = 360;      // altura na tela dos prédios; base ancorada no horizonte (topo opaco ~y84, espia por cima do condomínio)
        private const float CondominioWorldX = 0f;    // set-piece de entrada (não repete)
        private const float CondominioFootOffset = 0f; // ajuste vertical fino da cena de entrada

        private readonly ScreenManager screenManager;
        private readonly ContentManager content;
        private readonly Texture2D blank;
        private readonly Texture2D sky;          // céu, tileável (null => fallback de cor)
        private readonly Texture2D buildings;    // prédios de fundo, transparente, tileável
        private readonly Texture2D condominio;   // cena de entrada, não repete
        private readonly SpriteFont font;
        private readonly float viewWidth;

        private readonly SofiaPlayer sofia;
        private readonly List<PiaLocoEnemy> enemies = new List<PiaLocoEnemy>();
        private readonly List<Fighter> drawOrder = new List<Fighter>();
        private readonly Camera2D camera;
        private readonly SpawnArea[] areas;

        private ArenaPhase phase = ArenaPhase.Advancing;
        private int currentArea;
        private int defeatedCount;
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
            // Artes do Stage 1; ausentes => null e cai no fundo de cor chapada (degradação graciosa).
            this.sky = TryLoadTexture(content, "Backgrounds/Stage1/Sky");
            this.buildings = TryLoadTexture(content, "Backgrounds/Stage1/Buildings");
            this.condominio = TryLoadTexture(content, "Backgrounds/Stage1/Condominio");
            this.font = screenManager.Font;
            this.viewWidth = screenManager.BaseScreenSize.X;

            camera = new Camera2D(viewWidth, WorldWidth);

            // Three areas with escalating waves (2, 3, 4 enemies) and escalating toughness
            // (3, 4, 5 blows before a knockdown).
            areas = new[]
            {
                new SpawnArea(400f, 2, hitsToKnockdown: 3),
                new SpawnArea(1200f, 3, hitsToKnockdown: 4),
                new SpawnArea(2000f, 4, hitsToKnockdown: 5),
            };

            sofia = new SofiaPlayer(content, blank)
            {
                Position = new Vector2(90f, (CorridorTop + CorridorBottom) / 2f),
            };

            camera.MaxAdvanceX = areas[0].LockCameraX;
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
            sofia.HandleInput(input, controllingPlayer);
            sofia.Update(gameTime);
            ClampToCorridor(sofia);
            ClampToScreen(sofia);

            foreach (var enemy in enemies)
            {
                enemy.Update(gameTime);
                ClampToCorridor(enemy);
                ClampToWorld(enemy);
            }

            ResolveCombat();

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i].IsExpired)
                    enemies.RemoveAt(i);
            }

            camera.Follow(sofia.Position);
            UpdatePhase();

            // Give the knockdown a beat to read before declaring defeat.
            if (sofia.IsDefeated)
            {
                defeatTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (defeatTimer >= 1.2f)
                    PlayerDefeated = true;
            }
        }

        private void UpdatePhase()
        {
            switch (phase)
            {
                case ArenaPhase.Advancing:
                    if (currentArea < areas.Length && camera.X >= areas[currentArea].LockCameraX - 1f)
                    {
                        camera.MaxAdvanceX = areas[currentArea].LockCameraX;
                        SpawnWave(areas[currentArea]);
                        phase = ArenaPhase.Fighting;
                    }
                    break;

                case ArenaPhase.Fighting:
                    if (enemies.Count == 0)
                    {
                        currentArea++;
                        if (currentArea < areas.Length)
                        {
                            camera.MaxAdvanceX = areas[currentArea].LockCameraX;
                            phase = ArenaPhase.Advancing;
                        }
                        else
                        {
                            camera.MaxAdvanceX = WorldWidth - viewWidth;
                            phase = ArenaPhase.Cleared;
                        }
                    }
                    break;

                case ArenaPhase.Cleared:
                    if (sofia.Position.X >= WorldWidth - 60f)
                        Completed = true;
                    break;
            }
        }

        private void SpawnWave(SpawnArea area)
        {
            int count = area.EnemyCount;
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float x = MathHelper.Lerp(camera.Left + 500f, camera.Right - 40f, t);
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

        private static void ClampToCorridor(Fighter fighter)
        {
            fighter.Position.Y = MathHelper.Clamp(fighter.Position.Y, CorridorTop, CorridorBottom);
        }

        private void ClampToScreen(Fighter fighter)
        {
            float pad = fighter.BodyWidth / 2f;
            fighter.Position.X = MathHelper.Clamp(fighter.Position.X, camera.Left + pad, camera.Right - pad);
        }

        private static void ClampToWorld(Fighter fighter)
        {
            fighter.Position.X = MathHelper.Clamp(fighter.Position.X, 20f, WorldWidth - 20f);
        }

        // ----------------------------------------------------------------- Drawing

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            // Far/mid background with parallax (screen space).
            spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, screenManager.GlobalTransformation);
            DrawBackground(spriteBatch);
            spriteBatch.End();

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

            // Ground/road across the whole world so screens past the entrance still have a floor.
            DrawRect(spriteBatch, 0, (int)HorizonY, (int)WorldWidth, h - (int)HorizonY, new Color(96, 96, 102));
            DrawRect(spriteBatch, 0, (int)HorizonY, (int)WorldWidth, 4, new Color(60, 60, 66)); // curb line

            // Entrance set-piece: single draw at the start, scrolls 1:1, never repeats.
            if (condominio != null)
            {
                int sceneH = h;
                int sceneW = (int)Math.Round(condominio.Width * (sceneH / (float)condominio.Height));
                var dest = new Rectangle((int)CondominioWorldX, (int)CondominioFootOffset, sceneW, sceneH);
                spriteBatch.Draw(condominio, dest, Color.White);
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
        }

        private void DrawShadowedString(SpriteBatch spriteBatch, string text, Vector2 position, Color color)
        {
            spriteBatch.DrawString(font, text, position + new Vector2(1f, 1f), Color.Black);
            spriteBatch.DrawString(font, text, position, color);
        }
    }
}
