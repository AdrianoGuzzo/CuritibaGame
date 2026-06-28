#if CURITIBA_DEVTOOLS
using System;
using System.Collections.Generic;
using System.IO;
using Curitiba.Core.BeatEmUp;
using ImGuiNET;
using ImGuiNET.SampleProgram.XNA;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Num2 = System.Numerics.Vector2;
using Num4 = System.Numerics.Vector4;

namespace Curitiba.Core.DevTools
{
    /// <summary>
    /// In-game scene editor (desktop Debug only). Draws after the ScreenManager so ImGui owns the
    /// full backbuffer. F1 toggles it; while open the host screen freezes gameplay. Edits the live
    /// <see cref="StageDefinition"/>; "Aplicar" rebuilds the arena, "Salvar" writes the JSON (which
    /// the Fase 1 hot-reloader also picks up). Spawns and set pieces can be dragged in the world.
    /// </summary>
    internal sealed class ImGuiDevEditor : DrawableGameComponent, IDevEditor
    {
        private ImGuiRenderer renderer;
        private EditorContext ctx;
        private bool open;
        private bool wantsCapture;
        private KeyboardState prevKb;
        private string status = "";

        private int selSection;
        private int selWave;
        private float editorCamX;
        private int dragSpawn = -1;
        private int dragSetPiece = -1;
        private int dragSpawnPoint = -1;
        private bool dragEntry;
        private bool dragCurb;
        private string tmjFile = "capao-raso.tmj";

        private static readonly string[] PersonalityNames = { "Aggressive", "Defensive", "Balanced", "Runner" };
        private static readonly string[] SpawnTypeNames = { "Left", "Right", "Custom" };

        private static readonly string[] EntryModeNames = { "Fixed", "Carry", "Fall", "Door" };
        private static readonly string[] EntryModeLabels = { "Ponto fixo", "Herdar anterior", "Caindo do céu", "Pela porta" };
        private static readonly string[] FacingNames = { "Right", "Left" };
        private static readonly string[] FacingLabels = { "Direita", "Esquerda" };

        public ImGuiDevEditor(Game game) : base(game)
        {
            UpdateOrder = int.MaxValue;
            DrawOrder = int.MaxValue;
        }

        public bool IsOpen => open;

        public bool WantsCaptureInput => open && wantsCapture;

        public void SetContext(EditorContext context) => ctx = context;

        protected override void LoadContent()
        {
            renderer = new ImGuiRenderer(Game);
            renderer.RebuildFontAtlas();
            base.LoadContent();
        }

        public override void Update(GameTime gameTime)
        {
            KeyboardState kb = Keyboard.GetState();
            if (kb.IsKeyDown(Keys.F1) && prevKb.IsKeyUp(Keys.F1))
            {
                open = !open;
                if (open && ctx?.Arena != null)
                {
                    selSection = ctx.Arena.CurrentSectionIndex;
                    selWave = 0;
                    editorCamX = ctx.Arena.CameraX;
                }
            }
            prevKb = kb;

            EnforceSection();

            base.Update(gameTime);
        }

        /// <summary>
        /// Keeps the (frozen) arena pinned to the section and camera being edited. Runs after the
        /// ScreenManager (UpdateOrder = int.MaxValue), so it corrects any arena recreation —
        /// Aplicar, Salvar/hot-reload or Recarregar — in the same frame, before drawing. Without
        /// this the rebuilt arena (which always starts at section 0) would snap the view back.
        /// </summary>
        private void EnforceSection()
        {
            if (!open || ctx?.Arena == null || ctx.Arena.SectionCount <= 0)
                return;

            if (selSection < 0) selSection = 0;
            if (selSection >= ctx.Arena.SectionCount) selSection = ctx.Arena.SectionCount - 1;

            if (ctx.Arena.CurrentSectionIndex != selSection)
            {
                ctx.Arena.EditorLoadSection(selSection);
                ctx.Arena.EditorSetCameraX(editorCamX);
            }
            else
            {
                editorCamX = ctx.Arena.CameraX;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!open || renderer == null)
            {
                base.Draw(gameTime);
                return;
            }

            renderer.BeforeLayout(gameTime);

            if (ctx?.Definition != null && ctx.Arena != null)
            {
                DrawPanels();
                DrawGizmos();
            }
            else
            {
                if (ImGui.Begin("Editor de Cena (F1)"))
                    ImGui.TextDisabled("Abra o beat 'em up para editar a cena.");
                ImGui.End();
            }

            ImGuiIOPtr io = ImGui.GetIO();
            wantsCapture = io.WantCaptureMouse || io.WantCaptureKeyboard;

            renderer.AfterLayout();
            base.Draw(gameTime);
        }

        private void DrawPanels()
        {
            StageDefinition def = ctx.Definition;
            ClampSelection(def);

            ImGui.SetNextWindowSize(new Num2(440, 600), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Num2(20, 20), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Editor de Cena — Capão Raso (F1)"))
            {
                if (ImGui.Button("Aplicar")) ctx.Rebuild?.Invoke();
                ImGui.SameLine();
                if (ImGui.Button("Salvar")) Save();
                ImGui.SameLine();
                if (ImGui.Button("Recarregar")) Reload();
                if (!string.IsNullOrEmpty(status))
                    ImGui.TextDisabled(status);
                ImGui.Separator();

                int sec = selSection;
                if (def.Sections.Count > 0 && ImGui.SliderInt("Seção", ref sec, 0, def.Sections.Count - 1))
                {
                    selSection = sec;
                    selWave = 0;
                    ctx.Arena.EditorLoadSection(selSection);
                }

                float camX = ctx.Arena.CameraX;
                float maxCam = Math.Max(0f, ctx.Arena.SectionWidth - ctx.Arena.ViewWidth);
                if (ImGui.SliderFloat("Câmera X", ref camX, 0f, maxCam))
                    ctx.Arena.EditorSetCameraX(camX);

                ImGui.Separator();
                if (ImGui.CollapsingHeader("Corredor & Fundo")) DrawCorridorBackdrop(def);
                if (ImGui.CollapsingHeader("Tuning")) DrawTuning(def);
                if (ImGui.CollapsingHeader("Personalidades")) DrawPersonalities(def);
                if (ImGui.CollapsingHeader("Seção atual")) DrawSection(def);
                if (ImGui.CollapsingHeader("Spawn Points")) DrawSpawnPoints(def);
                if (ImGui.CollapsingHeader("Importar do Tiled")) DrawTiledImport();
            }
            ImGui.End();
        }

        private static void DrawCorridorBackdrop(StageDefinition def)
        {
            def.Corridor.Top = Drag("Corredor: topo", def.Corridor.Top);
            def.Corridor.Bottom = Drag("Corredor: base", def.Corridor.Bottom);
            def.Corridor.CurbHeight = Drag("Altura do meio-fio", def.Corridor.CurbHeight);
            ImGui.Spacing();
            def.Backdrop.SkyAsset = InputStr("Asset céu", def.Backdrop.SkyAsset);
            def.Backdrop.BuildingsAsset = InputStr("Asset prédios", def.Backdrop.BuildingsAsset);
            def.Backdrop.HorizonY = Drag("Horizonte Y", def.Backdrop.HorizonY);
            def.Backdrop.SkyScroll = Drag("Parallax céu", def.Backdrop.SkyScroll, 0.01f, 0f, 2f);
            def.Backdrop.BuildingsScroll = Drag("Parallax prédios", def.Backdrop.BuildingsScroll, 0.01f, 0f, 2f);
            def.Backdrop.BuildingsHeight = DragI("Altura prédios", def.Backdrop.BuildingsHeight);
        }

        private static void DrawTuning(StageDefinition def)
        {
            if (ImGui.TreeNode("Sofia")) { DrawFighterTuning("sofia", def.Tuning.Sofia); ImGui.TreePop(); }
            if (ImGui.TreeNode("Pia Loco")) { DrawFighterTuning("piaLoco", def.Tuning.PiaLoco); ImGui.TreePop(); }
        }

        private static void DrawFighterTuning(string id, FighterTuning t)
        {
            ImGui.PushID(id);
            t.MaxHealth = DragI("Vida máx", t.MaxHealth);
            t.AttackDamage = DragI("Dano", t.AttackDamage);
            t.AttackReach = DragI("Alcance", t.AttackReach);
            t.BodyWidth = DragI("Largura corpo", t.BodyWidth);
            t.BodyHeight = DragI("Altura corpo", t.BodyHeight);
            t.MoveSpeed = Drag("Velocidade", t.MoveSpeed);
            t.AttackWindup = Drag("Windup", t.AttackWindup, 0.01f);
            t.AttackActive = Drag("Ativo", t.AttackActive, 0.01f);
            t.AttackRecovery = Drag("Recuperação", t.AttackRecovery, 0.01f);
            t.HitDuration = Drag("Stagger", t.HitDuration, 0.01f);
            t.KnockdownDuration = Drag("Queda", t.KnockdownDuration, 0.01f);
            t.DashSpeed = Drag("Dash veloc.", t.DashSpeed);
            t.JumpImpulse = Drag("Pulo impulso", t.JumpImpulse);
            t.JumpGravity = Drag("Pulo gravidade", t.JumpGravity);

            ImGui.Separator();
            t.AttackBufferDuration = Drag("Buffer de input", t.AttackBufferDuration, 0.01f);
            if (t.ComboChain != null && ImGui.TreeNode("Combo"))
            {
                for (int i = 0; i < t.ComboChain.Count; i++)
                {
                    ComboMoveDef m = t.ComboChain[i];
                    if (!ImGui.TreeNode($"{i}: {m.Id}"))
                        continue;
                    ImGui.PushID(i);
                    m.Startup = Drag("Windup", m.Startup, 0.01f);
                    m.Active = Drag("Ativo", m.Active, 0.01f);
                    m.Recovery = Drag("Recuperação", m.Recovery, 0.01f);
                    m.CancelPoint = Drag("Cancel point", m.CancelPoint, 0.01f);
                    m.Damage = DragI("Dano", m.Damage);
                    m.Reach = DragI("Alcance", m.Reach);
                    m.KnockbackX = Drag("Knockback X", m.KnockbackX);
                    m.KnockbackY = Drag("Knockback Y", m.KnockbackY);
                    bool hitConfirm = m.RequiresHitConfirm;
                    if (ImGui.Checkbox("Exige acerto", ref hitConfirm)) m.RequiresHitConfirm = hitConfirm;
                    ImGui.PopID();
                    ImGui.TreePop();
                }
                ImGui.TreePop();
            }
            ImGui.PopID();
        }

        private static void DrawPersonalities(StageDefinition def)
        {
            foreach (KeyValuePair<string, PersonalityDef> kv in def.Personalities)
            {
                if (!ImGui.TreeNode(kv.Key))
                    continue;
                PersonalityDef p = kv.Value;
                p.AttackChance = Drag("Chance de ataque", p.AttackChance, 0.01f, 0f, 1f);
                p.AttackCooldown = Drag("Cooldown", p.AttackCooldown, 0.01f);
                p.PreferredDistance = Drag("Distância preferida", p.PreferredDistance);
                p.RunDistance = Drag("Distância corrida", p.RunDistance);
                p.RunSpeedMultiplier = Drag("Mult. corrida", p.RunSpeedMultiplier, 0.01f);
                ImGui.TreePop();
            }
        }

        private void DrawSection(StageDefinition def)
        {
            SectionDef s = def.Sections[selSection];
            s.BackgroundAsset = InputStr("Fundo (asset)", s.BackgroundAsset);
            s.FallbackWidth = Drag("Largura fallback", s.FallbackWidth);
            bool par = s.ParallaxBackdrop;
            if (ImGui.Checkbox("Parallax backdrop", ref par)) s.ParallaxBackdrop = par;
            s.RepeatX = DragI("RepeatX", s.RepeatX, 0.1f, 1, 16);
            s.CurbY = Drag("CurbY", s.CurbY);
            s.DrivewayLeft = Drag("Driveway esquerda", s.DrivewayLeft);
            s.DrivewayRight = Drag("Driveway direita", s.DrivewayRight);

            ImGui.Separator();
            ImGui.Text("Entrada da Sofia");
            DrawEntry(s);

            ImGui.Separator();
            ImGui.Text("Ondas");
            DrawWaves(s);

            ImGui.Separator();
            ImGui.Text("Set pieces");
            DrawSetPieces(s);
        }

        private void DrawEntry(SectionDef s)
        {
            EntryDef e = s.Entry ??= new EntryDef();

            int mi = Array.IndexOf(EntryModeNames, e.Mode);
            if (mi < 0) mi = 0;
            if (ImGui.Combo("Modo", ref mi, EntryModeLabels, EntryModeLabels.Length))
                e.Mode = EntryModeNames[mi];

            bool isCarry = mi == 1;
            if (isCarry)
                ImGui.TextDisabled("Herda a faixa (Y) em que a Sofia saiu da seção anterior; X é a borda de entrada. A 1ª seção cai para Ponto fixo.");

            e.X = Drag("Entrada X", e.X);
            float yEdited = Drag("Entrada Y (0=meio)", e.Y);
            e.Y = yEdited <= 0f ? 0f : ClampLane(yEdited);

            if (mi == 2)
                e.FallHeight = Drag("Altura da queda", e.FallHeight, 1f, 0f, 2000f);

            if (mi == 3)
            {
                e.WalkInDistance = Drag("Caminhar p/ dentro", e.WalkInDistance, 1f, 0f, 1000f);
                int fi = Array.IndexOf(FacingNames, e.Facing);
                if (fi < 0) fi = 0;
                if (ImGui.Combo("Direção", ref fi, FacingLabels, FacingLabels.Length))
                    e.Facing = FacingNames[fi];
            }

            if (isCarry)
            {
                bool prop = e.CarryProportional;
                if (ImGui.Checkbox("Proporcional ao corredor", ref prop)) e.CarryProportional = prop;
            }

            if (ImGui.Button("Testar entrada"))
            {
                if (ctx.Arena.CurrentSectionIndex != selSection)
                    ctx.Arena.EditorLoadSection(selSection);
                else
                    ctx.Arena.EditorReplayEntry();
                status = "Entrada armada — feche o editor (F1) p/ ver a animação";
            }
            ImGui.TextDisabled("Posiciona a Sofia na entrada (marcador magenta). A cena fica congelada no editor: a queda/caminhada só anima ao fechar com F1.");
        }

        private void DrawWaves(SectionDef s)
        {
            for (int w = 0; w < s.Waves.Count; w++)
            {
                ImGui.PushID(w);
                bool isSel = selWave == w;
                if (ImGui.RadioButton("##sel", isSel)) selWave = w;
                ImGui.SameLine();
                bool openNode = ImGui.TreeNode($"Onda {w}");
                ImGui.SameLine();
                if (ImGui.SmallButton("remover"))
                {
                    s.Waves.RemoveAt(w);
                    if (openNode) ImGui.TreePop();
                    ImGui.PopID();
                    w--;
                    continue;
                }

                if (openNode)
                {
                    WaveDef wave = s.Waves[w];
                    wave.LockCameraX = Drag("LockCameraX", wave.LockCameraX);
                    wave.Delay = Drag("Delay (s)", wave.Delay, 0.05f, 0f, 30f);

                    bool hasSpawns = wave.Spawns.Count > 0;
                    if (hasSpawns) ImGui.BeginDisabled(true);
                    wave.EnemyCount = DragI("EnemyCount (procedural)", wave.EnemyCount, 0.1f, 0, 32);
                    if (hasSpawns) ImGui.EndDisabled();

                    wave.HitsToKnockdown = DragI("HitsToKnockdown", wave.HitsToKnockdown, 0.1f, 1, 20);
                    if (hasSpawns)
                        ImGui.TextDisabled($"EnemyCount ignorado: {wave.Spawns.Count} spawn(s) explícito(s). Remova-os para usar o spread procedural.");
                    else
                        ImGui.TextDisabled("Sem spawns explícitos: usa EnemyCount (spread procedural, entra pela direita).");
                    if (ImGui.SmallButton("+ spawn")) wave.Spawns.Add(NewSpawn());
                    DrawSpawns(s, wave);
                    ImGui.TreePop();
                }
                ImGui.PopID();
            }
            if (ImGui.Button("+ onda"))
                s.Waves.Add(new WaveDef { LockCameraX = 0f, EnemyCount = 1, HitsToKnockdown = 3 });
        }

        private static void DrawSpawns(SectionDef section, WaveDef wave)
        {
            string[] refOptions = BuildSpawnRefOptions(section);
            for (int i = 0; i < wave.Spawns.Count; i++)
            {
                ImGui.PushID(i);
                SpawnDef sp = wave.Spawns[i];

                sp.Type = InputStr("Tipo inimigo", string.IsNullOrEmpty(sp.Type) ? sp.Template : sp.Type);

                int idx = Array.IndexOf(PersonalityNames, sp.Personality);
                if (idx < 0) idx = 2;
                if (ImGui.Combo("Personalidade", ref idx, PersonalityNames, PersonalityNames.Length))
                    sp.Personality = PersonalityNames[idx];

                int refIdx = IndexOfRef(refOptions, sp.SpawnPoint);
                if (ImGui.Combo("Spawn point", ref refIdx, refOptions, refOptions.Length))
                    sp.SpawnPoint = RefFromOption(refOptions[refIdx]);

                bool usesPoint = !string.IsNullOrEmpty(sp.SpawnPoint);
                sp.X = Drag(usesPoint ? "Alvo X (0=auto)" : "Destino X", sp.X);
                sp.Y = Drag(usesPoint ? "Alvo Y (0=auto)" : "Destino Y", sp.Y);

                if (ImGui.SmallButton("remover spawn"))
                {
                    wave.Spawns.RemoveAt(i);
                    ImGui.PopID();
                    i--;
                    continue;
                }
                ImGui.Separator();
                ImGui.PopID();
            }
        }

        private static string[] BuildSpawnRefOptions(SectionDef section)
        {
            var list = new List<string> { "(X/Y livre)", "random", "random:Left", "random:Right" };
            foreach (SpawnPointDef p in section.SpawnPoints)
                if (!string.IsNullOrEmpty(p.Name))
                    list.Add(p.Name);
            return list.ToArray();
        }

        private static int IndexOfRef(string[] options, string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            int i = Array.IndexOf(options, value);
            return i < 0 ? 0 : i;
        }

        private static string RefFromOption(string option) => option == "(X/Y livre)" ? "" : option;

        private void DrawSpawnPoints(StageDefinition def)
        {
            SectionDef s = def.Sections[selSection];
            ImGui.TextDisabled("Pontos de entrada da seção. Left/Right = borda da tela (só Y importa); Custom = ponto no mundo.");
            for (int i = 0; i < s.SpawnPoints.Count; i++)
            {
                ImGui.PushID(2000 + i);
                SpawnPointDef p = s.SpawnPoints[i];
                p.Name = InputStr("Nome", p.Name);
                int ti = Array.IndexOf(SpawnTypeNames, p.Type);
                if (ti < 0) ti = 2;
                if (ImGui.Combo("Tipo", ref ti, SpawnTypeNames, SpawnTypeNames.Length)) p.Type = SpawnTypeNames[ti];

                bool edge = IsEdgeType(p.Type);
                if (edge) ImGui.BeginDisabled(true);
                p.X = Drag(edge ? "X (ignorado p/ Left/Right)" : "X", p.X);
                if (edge) ImGui.EndDisabled();
                float yEdited = Drag(edge ? "Y (lane)" : "Y", p.Y);
                p.Y = edge ? ClampLane(yEdited) : yEdited;
                if (ImGui.SmallButton("remover ponto"))
                {
                    s.SpawnPoints.RemoveAt(i);
                    ImGui.PopID();
                    i--;
                    continue;
                }
                ImGui.Separator();
                ImGui.PopID();
            }
            if (ImGui.Button("+ spawn point"))
                s.SpawnPoints.Add(NewSpawnPoint(s));
        }

        private SpawnPointDef NewSpawnPoint(SectionDef s)
        {
            int n = s.SpawnPoints.Count + 1;
            return new SpawnPointDef
            {
                Id = "sp" + n,
                Name = "SpawnPoint" + n,
                Type = "Custom",
                X = (float)Math.Round(ctx.Arena.CameraX + ctx.Arena.ViewWidth / 2f),
                Y = (float)Math.Round(MidCorridor()),
            };
        }

        private void DrawSetPieces(SectionDef s)
        {
            for (int k = 0; k < s.SetPieces.Count; k++)
            {
                ImGui.PushID(1000 + k);
                SetPieceDef p = s.SetPieces[k];
                p.Asset = InputStr("Asset", p.Asset);
                p.X = Drag("X", p.X);
                p.Y = Drag("Y", p.Y);
                bool depth = p.DepthSortByY;
                if (ImGui.Checkbox("DepthSortByY", ref depth)) p.DepthSortByY = depth;
                bool solid = p.Solid;
                if (ImGui.Checkbox("Solid", ref solid)) p.Solid = solid;
                if (ImGui.SmallButton("remover peça"))
                {
                    s.SetPieces.RemoveAt(k);
                    ImGui.PopID();
                    k--;
                    continue;
                }
                ImGui.Separator();
                ImGui.PopID();
            }
            if (ImGui.Button("+ set piece"))
                s.SetPieces.Add(new SetPieceDef { Asset = "", X = ctx.Arena.CameraX + ctx.Arena.ViewWidth / 2f, Y = MidCorridor() });
        }

        private void DrawTiledImport()
        {
            ImGui.TextDisabled("Importa um .tmj para a SEÇÃO atual (regenera fundo, ondas, spawns e set pieces; preserva tuning/personalidades).");
            tmjFile = InputStr("Arquivo .tmj", tmjFile);
            if (ImGui.Button("Importar do Tiled (seção atual)"))
                ImportTiled();
        }

        private void ImportTiled()
        {
            if (string.IsNullOrEmpty(ctx.SavePath))
            {
                status = "Pasta de dados não resolvida";
                return;
            }

            string dir = Path.GetDirectoryName(ctx.SavePath);
            string path = Path.Combine(dir ?? "", tmjFile);
            if (TiledImporter.TryImportFile(path, ctx.Definition, selSection, out string err))
            {
                ctx.Rebuild?.Invoke();
                selWave = 0;
                status = "Importado de " + tmjFile;
            }
            else
            {
                status = "Falha import: " + err;
            }
        }

        private void DrawGizmos()
        {
            StageDefinition def = ctx.Definition;
            if (selSection < 0 || selSection >= def.Sections.Count)
                return;

            SectionDef section = def.Sections[selSection];
            List<SpawnDef> spawns = (selWave >= 0 && selWave < section.Waves.Count) ? section.Waves[selWave].Spawns : null;

            ImDrawListPtr dl = ImGui.GetBackgroundDrawList();
            uint colSpawn = ImGui.GetColorU32(new Num4(1f, 0.45f, 0.35f, 1f));
            uint colSpawnSel = ImGui.GetColorU32(new Num4(1f, 0.9f, 0.25f, 1f));
            uint colPiece = ImGui.GetColorU32(new Num4(0.4f, 0.8f, 1f, 1f));
            uint colPoint = ImGui.GetColorU32(new Num4(0.4f, 1f, 0.5f, 1f));
            uint colEntry = ImGui.GetColorU32(new Num4(1f, 0.3f, 0.9f, 1f));

            if (spawns != null)
            {
                for (int i = 0; i < spawns.Count; i++)
                {
                    bool free = IsFreeSpawn(spawns[i]);
                    Num2 c = WorldToScreen(SpawnMarkerWorld(section, spawns[i]));
                    dl.AddCircleFilled(c, free ? 7f : 5f, (free && dragSpawn == i) ? colSpawnSel : colSpawn);
                    dl.AddText(new Num2(c.X + 9f, c.Y - 8f), colSpawn, spawns[i].Personality);
                }
            }

            for (int k = 0; k < section.SetPieces.Count; k++)
            {
                Num2 c = WorldToScreen(section.SetPieces[k].X, section.SetPieces[k].Y);
                dl.AddRect(new Num2(c.X - 12f, c.Y - 16f), new Num2(c.X + 12f, c.Y), colPiece);
                dl.AddText(new Num2(c.X + 13f, c.Y - 16f), colPiece, "obj");
            }

            for (int i = 0; i < section.SpawnPoints.Count; i++)
            {
                SpawnPointDef p = section.SpawnPoints[i];
                Num2 c = WorldToScreen(GizmoWorld(p));
                dl.AddCircle(c, 9f, dragSpawnPoint == i ? colSpawnSel : colPoint);
                dl.AddText(new Num2(c.X + 11f, c.Y + 2f), colPoint, (p.Name ?? "") + " [" + p.Type + "]");
            }

            {
                EntryDef e = section.Entry ?? new EntryDef();
                Num2 ec = WorldToScreen(EntryWorld(section));
                if (string.Equals(e.Mode, "Fall", StringComparison.OrdinalIgnoreCase))
                    dl.AddLine(new Num2(ec.X, ec.Y - e.FallHeight * WorldScale), ec, colEntry);
                dl.AddCircle(ec, dragEntry ? 10f : 8f, dragEntry ? colSpawnSel : colEntry);
                dl.AddText(new Num2(ec.X + 11f, ec.Y - 10f), colEntry, "Sofia [" + e.Mode + "]");
            }

            DrawCorridorGizmos(dl, section);

            HandleDrag(spawns, section, section.SetPieces, section.SpawnPoints);
        }

        /// <summary>
        /// Draws the walkable corridor bounds, the curb (sidewalk/asphalt division) with a draggable
        /// handle, the curb-height edge and the driveway ramp span for the selected section — so the
        /// gameplay curb can be aligned to the painted art. Corridor top/bottom are global, shown as
        /// reference only; CurbY/Driveway are per-section and edited here (handle) or in "Seção atual".
        /// </summary>
        private void DrawCorridorGizmos(ImDrawListPtr dl, SectionDef section)
        {
            CorridorDef corridor = ctx.Definition.Corridor;
            Viewport vp = ctx.ScreenManager.PresentationViewport;
            float left = vp.X;
            float right = vp.X + vp.Width;

            uint colCorridor = ImGui.GetColorU32(new Num4(0.6f, 0.6f, 0.65f, 0.7f));
            uint colCurb = ImGui.GetColorU32(new Num4(1f, 0.55f, 0.15f, 1f));
            uint colCurbDim = ImGui.GetColorU32(new Num4(1f, 0.55f, 0.15f, 0.5f));
            uint colDrive = ImGui.GetColorU32(new Num4(0.45f, 1f, 0.85f, 0.9f));
            uint colSel = ImGui.GetColorU32(new Num4(1f, 0.9f, 0.25f, 1f));

            // Corridor top/bottom — walkable bounds (global; visual reference only).
            float topY = WorldToScreen(0f, corridor.Top).Y;
            float botY = WorldToScreen(0f, corridor.Bottom).Y;
            dl.AddLine(new Num2(left, topY), new Num2(right, topY), colCorridor);
            dl.AddText(new Num2(left + 6f, topY + 2f), colCorridor, "corredor topo");
            dl.AddLine(new Num2(left, botY), new Num2(right, botY), colCorridor);
            dl.AddText(new Num2(left + 6f, botY - 14f), colCorridor, "corredor base");

            if (section.CurbY <= 0f)
                return;

            // Curb-height edge — top of the raised sidewalk.
            if (corridor.CurbHeight > 0f)
            {
                float curbTopY = WorldToScreen(0f, section.CurbY - corridor.CurbHeight).Y;
                dl.AddLine(new Num2(left, curbTopY), new Num2(right, curbTopY), colCurbDim);
                dl.AddText(new Num2(right - 64f, curbTopY - 14f), colCurbDim, "meio-fio");
            }

            // Curb line — sidewalk/asphalt division, with draggable handle near the left edge.
            Num2 handle = WorldToScreen(CurbHandleWorld(section));
            dl.AddLine(new Num2(left, handle.Y), new Num2(right, handle.Y), colCurb, 2f);
            dl.AddRectFilled(new Num2(handle.X - 6f, handle.Y - 6f), new Num2(handle.X + 6f, handle.Y + 6f),
                dragCurb ? colSel : colCurb);
            dl.AddText(new Num2(handle.X + 10f, handle.Y - 16f), colCurb,
                "calçada/asfalto (CurbY=" + (int)section.CurbY + ")");

            // Driveway ramp span (where the curb flattens — no jump required).
            if (section.DrivewayRight > section.DrivewayLeft)
            {
                Num2 lTop = WorldToScreen(section.DrivewayLeft, corridor.Top);
                Num2 lBot = WorldToScreen(section.DrivewayLeft, section.CurbY);
                Num2 rTop = WorldToScreen(section.DrivewayRight, corridor.Top);
                Num2 rBot = WorldToScreen(section.DrivewayRight, section.CurbY);
                dl.AddLine(lTop, lBot, colDrive);
                dl.AddLine(rTop, rBot, colDrive);
                dl.AddText(new Num2(lTop.X + 4f, lTop.Y + 2f), colDrive, "driveway");
            }
        }

        private Vector2 CurbHandleWorld(SectionDef section) =>
            new Vector2(ctx.Arena.CameraX + 40f, section.CurbY);

        private void HandleDrag(List<SpawnDef> spawns, SectionDef section, List<SetPieceDef> pieces, List<SpawnPointDef> points)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            if (io.WantCaptureMouse)
                return;

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                dragSpawn = -1;
                dragSetPiece = -1;
                dragSpawnPoint = -1;
                dragEntry = false;
                dragCurb = false;
                float best = 14f;
                if (spawns != null)
                {
                    for (int i = 0; i < spawns.Count; i++)
                    {
                        if (!IsFreeSpawn(spawns[i]))
                            continue;
                        float d = Dist(io.MousePos, WorldToScreen(spawns[i].X, spawns[i].Y));
                        if (d < best) { best = d; dragSpawn = i; dragSetPiece = -1; dragSpawnPoint = -1; }
                    }
                }
                for (int k = 0; k < pieces.Count; k++)
                {
                    float d = Dist(io.MousePos, WorldToScreen(pieces[k].X, pieces[k].Y));
                    if (d < best) { best = d; dragSetPiece = k; dragSpawn = -1; dragSpawnPoint = -1; }
                }
                for (int i = 0; i < points.Count; i++)
                {
                    float d = Dist(io.MousePos, WorldToScreen(GizmoWorld(points[i])));
                    if (d < best) { best = d; dragSpawnPoint = i; dragSpawn = -1; dragSetPiece = -1; }
                }
                float de = Dist(io.MousePos, WorldToScreen(EntryWorld(section)));
                if (de < best) { best = de; dragEntry = true; dragSpawn = -1; dragSetPiece = -1; dragSpawnPoint = -1; }
                if (section.CurbY > 0f)
                {
                    float dc = Dist(io.MousePos, WorldToScreen(CurbHandleWorld(section)));
                    if (dc < best) { best = dc; dragCurb = true; dragSpawn = -1; dragSetPiece = -1; dragSpawnPoint = -1; dragEntry = false; }
                }
            }

            bool down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            if (down)
            {
                Vector2 w = ScreenToWorld(io.MousePos);
                if (dragSpawn >= 0 && spawns != null && dragSpawn < spawns.Count)
                {
                    spawns[dragSpawn].X = (float)Math.Round(w.X);
                    spawns[dragSpawn].Y = (float)Math.Round(w.Y);
                }
                else if (dragSetPiece >= 0 && dragSetPiece < pieces.Count)
                {
                    pieces[dragSetPiece].X = (float)Math.Round(w.X);
                    pieces[dragSetPiece].Y = (float)Math.Round(w.Y);
                }
                else if (dragSpawnPoint >= 0 && dragSpawnPoint < points.Count)
                {
                    SpawnPointDef p = points[dragSpawnPoint];
                    if (IsEdgeType(p.Type))
                    {
                        p.Y = (float)Math.Round(ClampLane(w.Y));
                    }
                    else
                    {
                        p.X = (float)Math.Round(w.X);
                        p.Y = (float)Math.Round(w.Y);
                    }
                }
                else if (dragEntry && section.Entry != null)
                {
                    section.Entry.X = (float)Math.Round(w.X);
                    section.Entry.Y = (float)Math.Round(ClampLane(w.Y));
                }
                else if (dragCurb)
                {
                    section.CurbY = (float)Math.Round(
                        MathHelper.Clamp(w.Y, ctx.Definition.Corridor.Top, ctx.Definition.Corridor.Bottom));
                }
            }
            else
            {
                dragSpawn = -1;
                dragSetPiece = -1;
                dragSpawnPoint = -1;
                dragEntry = false;
                dragCurb = false;
            }
        }

        private Vector2 GizmoWorld(SpawnPointDef p)
        {
            const float inset = 16f;
            float lane = ClampLane(p.Y);
            if (string.Equals(p.Type, "Left", StringComparison.OrdinalIgnoreCase))
                return new Vector2(ctx.Arena.CameraX + inset, lane);
            if (string.Equals(p.Type, "Right", StringComparison.OrdinalIgnoreCase))
                return new Vector2(ctx.Arena.CameraX + ctx.Arena.ViewWidth - inset, lane);
            return new Vector2(p.X, p.Y);
        }

        private Vector2 SpawnMarkerWorld(SectionDef section, SpawnDef sp)
        {
            if (IsFreeSpawn(sp))
                return new Vector2(sp.X, sp.Y);

            if (sp.SpawnPoint.StartsWith("random", StringComparison.OrdinalIgnoreCase))
                return new Vector2(ctx.Arena.CameraX + ctx.Arena.ViewWidth / 2f, ctx.Definition.Corridor.Top + 6f);

            foreach (SpawnPointDef p in section.SpawnPoints)
                if (string.Equals(p.Name, sp.SpawnPoint, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Id, sp.SpawnPoint, StringComparison.OrdinalIgnoreCase))
                    return GizmoWorld(p);

            return new Vector2(ctx.Arena.CameraX + ctx.Arena.ViewWidth / 2f, MidCorridor());
        }

        private Vector2 EntryWorld(SectionDef s)
        {
            EntryDef e = s.Entry ?? new EntryDef();
            float y = e.Y > 0f ? e.Y : MidCorridor();
            return new Vector2(e.X, ClampLane(y));
        }

        private static bool IsFreeSpawn(SpawnDef sp) => string.IsNullOrEmpty(sp.SpawnPoint);

        private static bool IsEdgeType(string type) =>
            string.Equals(type, "Left", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "Right", StringComparison.OrdinalIgnoreCase);

        private float ClampLane(float y)
        {
            float top = ctx.Definition.Corridor.Top;
            float bottom = ctx.Definition.Corridor.Bottom;
            return y < top ? top : (y > bottom ? bottom : y);
        }

        private float WorldScale => ctx.ScreenManager.GlobalTransformation.M11;

        private Num2 WorldToScreen(float wx, float wy)
        {
            Viewport vp = ctx.ScreenManager.PresentationViewport;
            float s = WorldScale;
            return new Num2((wx - ctx.Arena.CameraX) * s + vp.X, wy * s + vp.Y);
        }

        private Num2 WorldToScreen(Vector2 w) => WorldToScreen(w.X, w.Y);

        private Vector2 ScreenToWorld(Num2 p)
        {
            Viewport vp = ctx.ScreenManager.PresentationViewport;
            float s = WorldScale;
            if (s <= 0f) s = 1f;
            return new Vector2((p.X - vp.X) / s + ctx.Arena.CameraX, (p.Y - vp.Y) / s);
        }

        private float MidCorridor() => (ctx.Definition.Corridor.Top + ctx.Definition.Corridor.Bottom) / 2f;

        private SpawnDef NewSpawn() => new SpawnDef
        {
            Type = "piaLoco",
            Template = "piaLoco",
            Personality = "Balanced",
            X = (float)Math.Round(ctx.Arena.CameraX + ctx.Arena.ViewWidth / 2f),
            Y = (float)Math.Round(MidCorridor()),
        };

        private void ClampSelection(StageDefinition def)
        {
            if (selSection < 0) selSection = 0;
            if (selSection >= def.Sections.Count) selSection = Math.Max(0, def.Sections.Count - 1);
            int waves = def.Sections.Count > 0 ? def.Sections[selSection].Waves.Count : 0;
            if (selWave < 0) selWave = 0;
            if (selWave >= waves) selWave = Math.Max(0, waves - 1);
        }

        private void Save()
        {
            if (ctx.SavePath != null && StageLoader.TrySaveFile(ctx.SavePath, ctx.Definition))
                status = "Salvo em " + ctx.SavePath;
            else
                status = "Falha ao salvar (caminho não resolvido?)";
        }

        private void Reload()
        {
            if (ctx.SavePath != null && StageLoader.TryLoadFile(ctx.SavePath, out StageDefinition def))
            {
                ctx.Definition = def;
                ctx.Replace?.Invoke(def);
                selWave = 0;
                status = "Recarregado do disco";
            }
            else
            {
                status = "Falha ao recarregar";
            }
        }

        private static float Drag(string label, float value, float speed = 1f, float min = 0f, float max = 0f)
        {
            ImGui.DragFloat(label, ref value, speed, min, max);
            return value;
        }

        private static int DragI(string label, int value, float speed = 1f, int min = 0, int max = 0)
        {
            ImGui.DragInt(label, ref value, speed, min, max);
            return value;
        }

        private static string InputStr(string label, string value, uint max = 256)
        {
            value ??= "";
            ImGui.InputText(label, ref value, max);
            return value;
        }

        private static float Dist(Num2 a, Num2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
#endif
