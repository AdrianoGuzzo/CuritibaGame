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
    /// enemies, the scrolling camera and the wave/area progression.
    /// </summary>
    internal class CapaoRasoArena
    {
        // Advancing: scrolling toward the next wave's lock point (scroll sections only).
        // Fighting: a wave is live and the player is penned in. ExitReady: section cleared,
        // waiting for the player to reach the right edge to load the next section.
        private enum ArenaPhase { Advancing, Fighting, ExitReady }

        // Stage geometry, in the fixed 800x480 virtual space. The horizontal extent is
        // per-section (see sectionWidth); only the vertical corridor is shared by all sections.
        // These now come from the StageDefinition (see the constructor) so they can be edited as
        // data; the values default to the original hardcoded ones.
        private readonly float CorridorTop;
        private readonly float CorridorBottom;

        // Curb/step: how many pixels the sidewalk sits above the asphalt (visual only).
        private readonly float CurbHeight;

        // Background composition.
        private readonly float HorizonY;          // onde a rua/calçada começa
        private readonly float SkyScroll;         // parallax lento do céu
        private readonly float BuildingsScroll;   // parallax médio dos prédios
        private readonly int BuildingsHeight;     // altura na tela dos prédios; base ancorada no horizonte

        private readonly StageDefinition def;
        private readonly FighterTuning piaLocoTuning;

        private readonly ScreenManager screenManager;
        private readonly ContentManager content;
        private readonly Texture2D blank;
        private readonly Texture2D sky;          // céu, tileável (null => fallback de cor); backdrop em parallax
        private readonly Texture2D buildings;    // prédios de fundo, transparente, tileável; backdrop em parallax
        private readonly SpriteFont font;
        private readonly float viewWidth;

        private readonly SofiaPlayer sofia;
        private readonly List<PiaLocoEnemy> enemies = new List<PiaLocoEnemy>();
        // Coordinates the live crowd: a ring of slots around Sofia + a cap of simultaneous
        // attackers, so enemies surround her and take turns instead of stacking and mobbing.
        private readonly AttackSlotManager slots = new AttackSlotManager(maxAttackers: 2);
        private readonly List<Fighter> drawOrder = new List<Fighter>();
        private readonly StageSection[] sections;

        // Spawning is split out (SOLID): the factory builds enemies, the spawn manager resolves where
        // they enter from and walk to, and the wave manager sequences the section's waves.
        private readonly EnemyFactory enemyFactory;
        private readonly SpawnManager spawnManager;
        private readonly WaveManager waveManager = new WaveManager();
        private Camera2D camera;             // recreated per section (immutable world bounds)

        private ArenaPhase phase = ArenaPhase.Advancing;
        private int currentSection;
        private float sectionWidth;          // cached width of the current section's world
        private float cueBlink;              // accumulator for the blinking "AVANÇAR" advance cue (Advancing/ExitReady)
        private int defeatedCount;           // session-wide stat; not reset between sections
        private float defeatTimer;
        private Vector2 lastExitPosition;    // Sofia's feet when she left the previous section (Carry entry)

        // Dano que um inimigo arremessado causa ao "boliche" — derrubar outro inimigo no caminho.
        private const int ChainCollisionDamage = 10;

        // Vida do inimigo recém-atingido, exibida temporariamente no HUD abaixo da Sofia.
        private const float EnemyHealthDisplayDuration = 3f; // segundos — ajuste aqui
        private PiaLocoEnemy lastHitEnemy;                   // inimigo a exibir (o mais próximo da Sofia no acerto)
        private float enemyHealthTimer;                      // contagem regressiva até sumir

        private static readonly Comparison<Fighter> ByDepth = (a, b) => a.Position.Y.CompareTo(b.Position.Y);

        /// <summary>True once Sofia has reached the end of the stage (the "Fim da Demo" trigger).</summary>
        public bool Completed { get; private set; }

        /// <summary>True once Sofia has been knocked down and stayed down.</summary>
        public bool PlayerDefeated { get; private set; }

        // ----- Editor hooks (dev tools) -----
        internal float CameraX => camera.X;
        internal int CurrentSectionIndex => currentSection;
        internal int SectionCount => sections.Length;
        internal float SectionWidth => sectionWidth;
        internal float ViewWidth => viewWidth;

        /// <summary>Editor-only: jumps to a section so its layout can be edited/previewed.</summary>
        internal void EditorLoadSection(int index)
        {
            if (index >= 0 && index < sections.Length)
                LoadSection(index);
        }

        /// <summary>Editor-only: free camera pan within the current section.</summary>
        internal void EditorSetCameraX(float x) => camera.SetX(x);

        /// <summary>Builds the original Capão Raso stage from the hardcoded defaults.</summary>
        public CapaoRasoArena(ScreenManager screenManager, ContentManager content)
            : this(screenManager, content, StageDefinition.CapaoRasoDefault())
        {
        }

        /// <summary>Builds the stage described by <paramref name="definition"/> (loaded from JSON).</summary>
        public CapaoRasoArena(ScreenManager screenManager, ContentManager content, StageDefinition definition)
        {
            this.screenManager = screenManager;
            this.content = content;
            this.def = definition ?? StageDefinition.CapaoRasoDefault();

            // Geometry / backdrop tuning, data-driven.
            CorridorTop = def.Corridor.Top;
            CorridorBottom = def.Corridor.Bottom;
            CurbHeight = def.Corridor.CurbHeight;
            HorizonY = def.Backdrop.HorizonY;
            SkyScroll = def.Backdrop.SkyScroll;
            BuildingsScroll = def.Backdrop.BuildingsScroll;
            BuildingsHeight = def.Backdrop.BuildingsHeight;

            this.blank = content.Load<Texture2D>("Sprites/blank");
            // Backdrop em parallax; ausentes => null e cai no fundo de cor chapada.
            this.sky = TryLoadTexture(content, def.Backdrop.SkyAsset);
            this.buildings = TryLoadTexture(content, def.Backdrop.BuildingsAsset);
            this.font = screenManager.Font;
            this.viewWidth = screenManager.BaseScreenSize.X;

            this.piaLocoTuning = def.Tuning?.PiaLoco ?? FighterTuning.PiaLocoDefaults();
            sofia = new SofiaPlayer(content, blank, def.Tuning?.Sofia);

            // Spawning pipeline: factory (creates enemies) ← spawn manager (resolves entry/walk-in)
            // ← wave manager (sequences waves). The arena keeps the camera and phase.
            enemyFactory = new EnemyFactory(content, blank, sofia, enemies, slots);
            spawnManager = new SpawnManager(enemyFactory, enemies, slots, ResolveProfileByName, ResolveTemplateTuning);

            // A hybrid stage: each section's mode (scroll vs frame) is decided automatically from
            // the real width of its background image; missing art falls back to a placeholder width.
            sections = BuildSections();

            LoadSection(0);
        }

        // ----------------------------------------------------------------- Data → runtime mapping

        private StageSection[] BuildSections()
        {
            var result = new StageSection[def.Sections.Count];
            for (int i = 0; i < def.Sections.Count; i++)
            {
                SectionDef sd = def.Sections[i];
                result[i] = new StageSection(
                    sd.BackgroundAsset, sd.FallbackWidth, BuildWaves(sd.Waves),
                    sd.ParallaxBackdrop, sd.CurbY, sd.DrivewayLeft, sd.DrivewayRight, sd.RepeatX,
                    BuildSetPieces(sd.SetPieces), BuildSpawnPoints(sd.SpawnPoints), sd.Entry);
            }
            return result;
        }

        private static SpawnArea[] BuildWaves(List<WaveDef> waveDefs)
        {
            if (waveDefs == null || waveDefs.Count == 0)
                return System.Array.Empty<SpawnArea>();

            var result = new SpawnArea[waveDefs.Count];
            for (int i = 0; i < waveDefs.Count; i++)
            {
                WaveDef w = waveDefs[i];
                // Keep the authored spawns raw: position resolution (off-screen birth + walk-in target)
                // needs the live camera, so it happens later in SpawnManager.
                SpawnDef[] spawns = (w.Spawns != null && w.Spawns.Count > 0) ? w.Spawns.ToArray() : null;
                result[i] = new SpawnArea(w.LockCameraX, w.EnemyCount, w.HitsToKnockdown, w.Delay, spawns);
            }
            return result;
        }

        private static SpawnPoint[] BuildSpawnPoints(List<SpawnPointDef> defs)
        {
            if (defs == null || defs.Count == 0)
                return System.Array.Empty<SpawnPoint>();

            var result = new SpawnPoint[defs.Count];
            for (int i = 0; i < defs.Count; i++)
            {
                SpawnPointDef d = defs[i];
                result[i] = new SpawnPoint(d.Id, d.Name, new Vector2(d.X, d.Y), ParseSpawnType(d.Type));
            }
            return result;
        }

        private static SpawnPointType ParseSpawnType(string name) =>
            System.Enum.TryParse(name, true, out SpawnPointType t) ? t : SpawnPointType.Custom;

        private static SetPiece[] BuildSetPieces(List<SetPieceDef> defs)
        {
            if (defs == null || defs.Count == 0)
                return System.Array.Empty<SetPiece>();

            var result = new SetPiece[defs.Count];
            for (int i = 0; i < defs.Count; i++)
            {
                SetPieceDef d = defs[i];
                result[i] = new SetPiece
                {
                    Asset = d.Asset,
                    Position = new Vector2(d.X, d.Y),
                    DepthSortByY = d.DepthSortByY,
                    Solid = d.Solid,
                };
            }
            return result;
        }

        private static EnemyPersonality ParsePersonality(string name) =>
            System.Enum.TryParse(name, out EnemyPersonality p) ? p : EnemyPersonality.Balanced;

        private EnemyProfile ResolveProfile(EnemyPersonality personality)
        {
            PersonalityDef pd = null;
            def.Personalities?.TryGetValue(personality.ToString(), out pd);
            return EnemyProfile.From(personality, pd);
        }

        // String → profile, for the spawn manager (authored spawns carry a personality name).
        private EnemyProfile ResolveProfileByName(string personality) =>
            ResolveProfile(ParsePersonality(personality));

        // Only one enemy template for now; future templates would map to their own tuning here.
        private FighterTuning ResolveTemplateTuning(string template) => piaLocoTuning;

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

            foreach (SetPiece piece in s.SetPieces)
                piece.Texture = piece.Asset != null ? TryLoadTexture(content, piece.Asset) : null;

            sectionWidth = s.Width;
            camera = new Camera2D(viewWidth, sectionWidth);

            enemies.Clear();
            slots.Reset();
            cueBlink = 0f;

            waveManager.Reset(s.Waves);
            spawnManager.Configure(camera, sectionWidth, CorridorTop, CorridorBottom, s.SpawnPoints);

            PlaceSofiaForEntry(s, index);
            camera.Snap(sofia.Position); // snap before the first draw to avoid a one-frame jump

            if (!waveManager.HasWaves)
            {
                // No waves authored: open straight to the exit.
                camera.MaxAdvanceX = sectionWidth - viewWidth;
                phase = ArenaPhase.ExitReady;
            }
            else if (s.Mode == SectionMode.Frame)
            {
                // Single screen: arm the first wave now; it spawns after its delay.
                waveManager.Arm();
                phase = ArenaPhase.Fighting;
            }
            else
            {
                camera.MaxAdvanceX = waveManager.CurrentLockX;
                phase = ArenaPhase.Advancing;
            }
        }

        /// <summary>
        /// Places Sofia for the section's authored entry. The default (<c>Fixed</c>, X=90, mid-corridor)
        /// reproduces the original hardcoded spot. <c>Carry</c> keeps the lane she left the previous
        /// section on; <c>Fall</c> drops her in from above; <c>Door</c> walks her in from the entry point.
        /// </summary>
        private void PlaceSofiaForEntry(StageSection s, int index)
        {
            EntryDef e = s.Entry ?? new EntryDef();
            float mid = (CorridorTop + CorridorBottom) / 2f;
            float targetY = e.Y > 0f ? e.Y : mid;
            float targetX = e.X;

            bool carry = string.Equals(e.Mode, "Carry", StringComparison.OrdinalIgnoreCase) && index > 0;
            if (carry)
            {
                // Keep the lane (Y) Sofia left the previous section on so the transition reads as one
                // continuous walk. Proportional carries the same corridor fraction (ready for per-section
                // corridors); absolute carries the world Y directly (identical while the corridor is shared).
                if (e.CarryProportional)
                {
                    float prevFrac = MathHelper.Clamp(
                        (lastExitPosition.Y - CorridorTop) / Math.Max(1f, CorridorBottom - CorridorTop), 0f, 1f);
                    targetY = CorridorTop + prevFrac * (CorridorBottom - CorridorTop);
                }
                else
                {
                    targetY = MathHelper.Clamp(lastExitPosition.Y, CorridorTop, CorridorBottom);
                }
            }

            sofia.Facing = string.Equals(e.Facing, "Left", StringComparison.OrdinalIgnoreCase)
                ? FaceDirection.Left
                : FaceDirection.Right;
            sofia.Position = new Vector2(targetX, targetY);

            // Coloca a Sofia já na elevação da faixa de destino. Sem isso, uma entrada no chão sobre a
            // calçada começa "no asfalto" (GroundOffset 0) e o ApplyCurb — vendo o player na rua querendo
            // a calçada — a prende na base do meio-fio (parece "jogada no asfalto"). O Fall sobe a própria
            // elevação ao descer, então começa do asfalto.
            bool isFall = string.Equals(e.Mode, "Fall", StringComparison.OrdinalIgnoreCase);
            if (!isFall)
                sofia.GroundOffset = (s.CurbY > 0f && targetY < s.CurbY) ? CurbHeight : 0f;

            if (isFall)
            {
                sofia.StartEntryFall(Math.Max(0f, e.FallHeight));
            }
            else if (string.Equals(e.Mode, "Door", StringComparison.OrdinalIgnoreCase))
            {
                float dir = sofia.Facing == FaceDirection.Left ? -1f : 1f;
                sofia.StartEntryWalk(targetX + dir * Math.Max(0f, e.WalkInDistance));
            }
        }

        /// <summary>Editor-only: re-runs the current section's entry placement so it can be previewed
        /// in the frozen scene (drag the gizmo / change the mode, then watch Sofia fall / walk in).</summary>
        internal void EditorReplayEntry()
        {
            StageSection s = sections[currentSection];
            PlaceSofiaForEntry(s, currentSection);
            camera.Snap(sofia.Position);
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
                {
                    if (enemies[i] == lastHitEnemy) lastHitEnemy = null; // não apontar para um slot reciclado
                    slots.Release(enemies[i]); // free its ring slot/token before despawning
                    enemies.RemoveAt(i);
                }
            }

            // Conta regressiva da barra de vida do inimigo no HUD; some sozinha ou se o alvo morrer.
            if (enemyHealthTimer > 0f)
            {
                enemyHealthTimer -= dt;
                if (enemyHealthTimer <= 0f || lastHitEnemy == null || !lastHitEnemy.IsAlive)
                    lastHitEnemy = null;
            }

            camera.Follow(sofia.Position, dt); // eased follow: unlocks pan smoothly, walking keeps up
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
            switch (phase)
            {
                case ArenaPhase.Advancing: // scroll sections: roll to the wave's lock point, then arm it
                    cueBlink += dt; // tick the "AVANÇAR" arrow guiding the player to the next fight
                    if (camera.X >= waveManager.CurrentLockX - 1f)
                    {
                        camera.MaxAdvanceX = waveManager.CurrentLockX;
                        waveManager.Arm();
                        phase = ArenaPhase.Fighting;
                    }
                    break;

                case ArenaPhase.Fighting:
                    // Spawn the armed wave once its delay elapses; then wait until it is cleared.
                    if (waveManager.TickReadyToSpawn(dt))
                    {
                        spawnManager.SpawnWave(waveManager.Current);
                    }
                    else if (waveManager.HasSpawnedCurrent && enemies.Count == 0)
                    {
                        if (waveManager.Advance())
                        {
                            // More waves in this section: scroll on to the next, or arm it in place.
                            if (sections[currentSection].Mode == SectionMode.Scroll)
                            {
                                camera.MaxAdvanceX = waveManager.CurrentLockX; // unlock; Follow eases the pan
                                phase = ArenaPhase.Advancing;
                            }
                            else
                            {
                                waveManager.Arm();
                            }
                        }
                        else
                        {
                            // Section cleared: open the way to the right edge / the exit.
                            camera.MaxAdvanceX = sectionWidth - viewWidth; // unlock; Follow eases the pan
                            phase = ArenaPhase.ExitReady;
                        }
                    }
                    break;

                case ArenaPhase.ExitReady:
                    cueBlink += dt;
                    if (sofia.Position.X >= sectionWidth - 60f)
                    {
                        if (currentSection + 1 < sections.Length)
                        {
                            lastExitPosition = sofia.Position; // remember the lane for a Carry entry
                            LoadSection(currentSection + 1);
                        }
                        else
                            Completed = true;
                    }
                    break;
            }
        }

        private void ResolveCombat()
        {
            // Sofia's swing against every enemy (once per swing per enemy).
            if (sofia.CurrentAttack.HasValue)
            {
                AttackData attack = sofia.CurrentAttack.Value;
                PiaLocoEnemy closest = null;
                float closestDist = float.MaxValue;
                foreach (var enemy in enemies)
                {
                    if (!enemy.IsAlive || sofia.AttackHitTargets.Contains(enemy))
                        continue;

                    if (attack.Hitbox.Intersects(enemy.HurtBox))
                    {
                        enemy.TakeDamage(attack.Damage, attack.Knockback,
                            attack.Launches ? HitReaction.Launch : HitReaction.Normal);
                        sofia.AttackHitTargets.Add(enemy);
                        if (enemy.IsDefeated)
                            defeatedCount++;

                        // Entre os atingidos neste frame, exibe a vida do mais próximo da Sofia (eixo X = corredor).
                        float dist = Math.Abs(enemy.Position.X - sofia.Position.X);
                        if (dist < closestDist) { closestDist = dist; closest = enemy; }
                    }
                }
                if (closest != null)
                {
                    lastHitEnemy = closest;
                    enemyHealthTimer = EnemyHealthDisplayDuration;
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

            ResolveThrowCollisions();
        }

        /// <summary>
        /// "Boliche": um inimigo arremessado pelo chute finalizador derruba (e fere) qualquer outro
        /// inimigo no caminho. O arremessado perde a maior parte da força ao colidir (assenta perto),
        /// e cada alvo é atingido uma vez por voo (dedup via <see cref="Fighter.AttackHitTargets"/>,
        /// reaproveitado enquanto o corpo voa).
        /// </summary>
        private void ResolveThrowCollisions()
        {
            foreach (var thrown in enemies)
            {
                if (!thrown.IsBeingThrown)
                    continue;

                // Direção do voo (não a do Facing — o arremessado encara o atacante), para
                // empurrar o atingido no mesmo sentido. Sinal 0 (parado) cai para a direita.
                float dir = thrown.ThrowDirectionX >= 0 ? 1f : -1f;
                Vector2 knockback = new Vector2(dir * 260f, -40f);

                foreach (var other in enemies)
                {
                    if (other == thrown || !other.IsAlive || thrown.AttackHitTargets.Contains(other))
                        continue;

                    if (!thrown.HurtBox.Intersects(other.HurtBox))
                        continue;

                    other.TakeDamage(ChainCollisionDamage, knockback, HitReaction.Knockdown);
                    thrown.AttackHitTargets.Add(other);
                    if (other.IsDefeated)
                        defeatedCount++;
                    thrown.DampenThrow(); // o arremessado perde força e cai junto
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
                fighter.SetGroundTarget(0f);
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
                fighter.SetGroundTarget(0f);
            }
            else
            {
                // Stepping off the front edge of the sidewalk onto the asphalt (was raised last frame,
                // now past the curb line): the player plays the hop's fall as a small drop instead of
                // snapping down. TryStartCurbDrop takes over the elevation (and is a no-op for enemies).
                bool steppingDown = grounded && !wantsSidewalk && fighter.GroundOffset >= CurbHeight - 0.5f;
                if (!(steppingDown && fighter.TryStartCurbDrop(CurbHeight)))
                {
                    // Everything else resolves elevation by depth. On the ground SetGroundTarget snaps
                    // (a crisp step), enemies climb freely. While airborne it ramps the elevation from the
                    // take-off floor to whichever floor the jump crosses to, so the arc is smooth and the
                    // feet land flush (no curb jolt mid-air or on touchdown).
                    fighter.SetGroundTarget(wantsSidewalk ? CurbHeight : 0f);
                }
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
            // Enemies still walking in may sit off-screen (beyond the world edges) until they arrive;
            // clamping them here would teleport them onto the field and defeat the entrance.
            if (fighter is PiaLocoEnemy enemy && enemy.IsEntering)
                return;

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

        /// <summary>Draws a hero's HUD portrait inside a framed box (drop-shadow + light frame),
        /// scaled to <paramref name="size"/>×<paramref name="size"/>. Hero-agnostic: works for any
        /// <see cref="Fighter.Portrait"/>.</summary>
        private void DrawPortrait(SpriteBatch spriteBatch, Texture2D portrait, int x, int y, int size)
        {
            DrawRect(spriteBatch, x - 2, y - 2, size + 4, size + 4, new Color(0, 0, 0, 180)); // outer shadow/border
            DrawRect(spriteBatch, x - 1, y - 1, size + 2, size + 2, new Color(230, 230, 235)); // light frame
            spriteBatch.Draw(portrait, new Rectangle(x, y, size, size), Color.White);
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

            // Set pieces (cars/props), drawn bottom-centred at their world position. Depth-sorting
            // with the fighters is a later refinement; for now they sit behind the combatants.
            foreach (SetPiece piece in s.SetPieces)
                DrawSetPiece(spriteBatch, piece);
        }

        private void DrawSetPiece(SpriteBatch spriteBatch, SetPiece piece)
        {
            if (piece.Texture != null)
            {
                int pw = piece.Texture.Width;
                int ph = piece.Texture.Height;
                var dest = new Rectangle((int)(piece.Position.X - pw / 2f), (int)(piece.Position.Y - ph), pw, ph);
                spriteBatch.Draw(piece.Texture, dest, Color.White);
            }
            else
            {
                // Missing art: a legible placeholder so the piece is visible in the editor.
                DrawRect(spriteBatch, (int)(piece.Position.X - 32f), (int)(piece.Position.Y - 40f), 64, 40, new Color(80, 80, 90, 200));
            }
        }

        private void DrawHud(SpriteBatch spriteBatch)
        {
            // Optional hero portrait at the top-left; when present the stage name and health bar
            // shift right to make room. Without it (art not registered) the HUD keeps its old layout.
            int hudLeft = 20;
            if (sofia.Portrait != null)
            {
                const int portraitSize = 56;
                DrawPortrait(spriteBatch, sofia.Portrait, 20, 12, portraitSize);
                hudLeft = 20 + portraitSize + 10;
            }

            // Hero name, above the health bar.
            if (sofia.Name != null)
                DrawShadowedString(spriteBatch, sofia.Name, new Vector2(hudLeft, 12f), Color.White);

            // Sofia's health bar (below the name).
            const int barWidth = 180, barHeight = 16;
            int barX = hudLeft, barY = 40;
            int fill = (int)(barWidth * (sofia.Health / (float)sofia.MaxHealth));
            DrawRect(spriteBatch, barX - 2, barY - 2, barWidth + 4, barHeight + 4, new Color(0, 0, 0, 180));
            DrawRect(spriteBatch, barX, barY, barWidth, barHeight, new Color(60, 24, 24));
            DrawRect(spriteBatch, barX, barY, fill, barHeight, new Color(70, 200, 90));

            // Vida do inimigo recém-atingido (logo abaixo da barra da Sofia), some após o timer.
            if (lastHitEnemy != null && lastHitEnemy.IsAlive && enemyHealthTimer > 0f)
            {
                const int eBarWidth = 180, eBarHeight = 16;
                int eBarX = barX, eBarY = barY + barHeight + 8;
                int eFill = (int)(eBarWidth * (lastHitEnemy.Health / (float)lastHitEnemy.MaxHealth));

                DrawRect(spriteBatch, eBarX - 2, eBarY - 2, eBarWidth + 4, eBarHeight + 4, new Color(0, 0, 0, 180));
                DrawRect(spriteBatch, eBarX, eBarY, eBarWidth, eBarHeight, new Color(70, 20, 20));
                DrawRect(spriteBatch, eBarX, eBarY, eFill, eBarHeight, new Color(214, 64, 48));

                // Nome do inimigo escrito dentro da barra, centralizado verticalmente.
                if (lastHitEnemy.Name != null)
                {
                    float nameH = font.MeasureString(lastHitEnemy.Name).Y;
                    DrawShadowedString(spriteBatch, lastHitEnemy.Name,
                        new Vector2(eBarX + 4f, eBarY + (eBarHeight - nameH) / 2f), Color.White);
                }
            }

            // Stage name, centred at the top.
            Vector2 stageSize = font.MeasureString(Resources.StageCapaoRaso);
            DrawShadowedString(spriteBatch, Resources.StageCapaoRaso,
                new Vector2((screenManager.BaseScreenSize.X - stageSize.X) / 2f, 12f), Color.White);

            // Defeated count (top-right).
            string defeated = Resources.Defeated + ": " + defeatedCount;
            Vector2 size = font.MeasureString(defeated);
            DrawShadowedString(spriteBatch, defeated, new Vector2(screenManager.BaseScreenSize.X - size.X - 20f, 12f), Color.White);

            DrawAdvanceCue(spriteBatch);
        }

        /// <summary>True whenever the player is free to move on: scrolling to the next wave
        /// (Advancing) or heading to the right edge once the section is cleared (ExitReady) — shown
        /// for every wave, including the final one (where the edge leads to the end of the demo).</summary>
        private bool ShowAdvanceCue =>
            phase == ArenaPhase.Advancing || phase == ArenaPhase.ExitReady;

        /// <summary>
        /// "Continue" indicator: while the area is cleared and the way is open, a blinking "AVANÇAR"
        /// cue with a right-pointing arrow appears at the right edge, guiding the player to the next
        /// fight. It disappears once the next wave/section spawns enemies. Placeholder for a future sprite.
        /// </summary>
        private void DrawAdvanceCue(SpriteBatch spriteBatch)
        {
            if (!ShowAdvanceCue)
                return;

            if ((int)(cueBlink * 2f) % 2 != 0) // ~1 Hz blink (on/off each half-second)
                return;

            var color = new Color(255, 224, 64);
            string txt = Resources.Advance;
            Vector2 size = font.MeasureString(txt);
            float cx = screenManager.BaseScreenSize.X - 30f;
            float midY = (CorridorTop + CorridorBottom) / 2f;
            // Gentle horizontal bob to reinforce "keep going right".
            float bob = (float)Math.Sin(cueBlink * 4f) * 4f;

            DrawShadowedString(spriteBatch, txt, new Vector2(cx - size.X + bob, midY - size.Y / 2f), color);

            // Right-pointing arrow above the text, built from axis-aligned bars: a shaft plus a
            // chevron head (two short bars stepping toward the tip).
            int ax = (int)(cx - 22f + bob);
            int ay = (int)(midY - size.Y / 2f - 18f);
            DrawRect(spriteBatch, ax, ay + 4, 16, 5, color);        // shaft
            DrawRect(spriteBatch, ax + 12, ay, 5, 13, color);       // head: vertical back
            DrawRect(spriteBatch, ax + 16, ay + 2, 5, 9, color);    //       step in
            DrawRect(spriteBatch, ax + 20, ay + 4, 5, 5, color);    //       tip
        }

        private void DrawShadowedString(SpriteBatch spriteBatch, string text, Vector2 position, Color color)
        {
            spriteBatch.DrawString(font, text, position + new Vector2(1f, 1f), Color.Black);
            spriteBatch.DrawString(font, text, position, color);
        }
    }
}
