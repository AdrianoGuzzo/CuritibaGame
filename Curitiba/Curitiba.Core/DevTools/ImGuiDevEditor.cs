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
        private string tmjFile = "capao-raso.tmj";

        private static readonly string[] PersonalityNames = { "Aggressive", "Defensive", "Balanced", "Runner" };

        public ImGuiDevEditor(Game game) : base(game)
        {
            // Run after the ScreenManager (added earlier) so the full viewport is restored.
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
                // Open the editor on whichever section the game is currently in.
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
                // Steady state: remember the current pan so it survives the next rebuild.
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

        // ----------------------------------------------------------------- Panels

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
            ImGui.Text("Ondas");
            DrawWaves(s);

            ImGui.Separator();
            ImGui.Text("Set pieces");
            DrawSetPieces(s);
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
                    wave.EnemyCount = DragI("EnemyCount (procedural)", wave.EnemyCount, 0.1f, 0, 32);
                    wave.HitsToKnockdown = DragI("HitsToKnockdown", wave.HitsToKnockdown, 0.1f, 1, 20);
                    ImGui.TextDisabled($"Spawns explícitos: {wave.Spawns.Count} (têm prioridade sobre EnemyCount)");
                    if (ImGui.SmallButton("+ spawn")) wave.Spawns.Add(NewSpawn());
                    DrawSpawns(wave);
                    ImGui.TreePop();
                }
                ImGui.PopID();
            }
            if (ImGui.Button("+ onda"))
                s.Waves.Add(new WaveDef { LockCameraX = 0f, EnemyCount = 1, HitsToKnockdown = 3 });
        }

        private static void DrawSpawns(WaveDef wave)
        {
            for (int i = 0; i < wave.Spawns.Count; i++)
            {
                ImGui.PushID(i);
                SpawnDef sp = wave.Spawns[i];
                int idx = Array.IndexOf(PersonalityNames, sp.Personality);
                if (idx < 0) idx = 2;
                if (ImGui.Combo("Personalidade", ref idx, PersonalityNames, PersonalityNames.Length))
                    sp.Personality = PersonalityNames[idx];
                sp.X = Drag("X", sp.X);
                sp.Y = Drag("Y", sp.Y);
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

        // ----------------------------------------------------------------- Gizmos / dragging

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

            if (spawns != null)
            {
                for (int i = 0; i < spawns.Count; i++)
                {
                    Num2 c = WorldToScreen(spawns[i].X, spawns[i].Y);
                    dl.AddCircleFilled(c, 7f, dragSpawn == i ? colSpawnSel : colSpawn);
                    dl.AddText(new Num2(c.X + 9f, c.Y - 8f), colSpawn, spawns[i].Personality);
                }
            }

            for (int k = 0; k < section.SetPieces.Count; k++)
            {
                Num2 c = WorldToScreen(section.SetPieces[k].X, section.SetPieces[k].Y);
                dl.AddRect(new Num2(c.X - 12f, c.Y - 16f), new Num2(c.X + 12f, c.Y), colPiece);
                dl.AddText(new Num2(c.X + 13f, c.Y - 16f), colPiece, "obj");
            }

            HandleDrag(spawns, section.SetPieces);
        }

        private void HandleDrag(List<SpawnDef> spawns, List<SetPieceDef> pieces)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            if (io.WantCaptureMouse)
                return;

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                dragSpawn = -1;
                dragSetPiece = -1;
                float best = 14f;
                if (spawns != null)
                {
                    for (int i = 0; i < spawns.Count; i++)
                    {
                        float d = Dist(io.MousePos, WorldToScreen(spawns[i].X, spawns[i].Y));
                        if (d < best) { best = d; dragSpawn = i; dragSetPiece = -1; }
                    }
                }
                for (int k = 0; k < pieces.Count; k++)
                {
                    float d = Dist(io.MousePos, WorldToScreen(pieces[k].X, pieces[k].Y));
                    if (d < best) { best = d; dragSetPiece = k; dragSpawn = -1; }
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
            }
            else
            {
                dragSpawn = -1;
                dragSetPiece = -1;
            }
        }

        // ----------------------------------------------------------------- Helpers

        private float WorldScale => ctx.ScreenManager.GlobalTransformation.M11;

        private Num2 WorldToScreen(float wx, float wy)
        {
            Viewport vp = ctx.ScreenManager.PresentationViewport;
            float s = WorldScale;
            return new Num2((wx - ctx.Arena.CameraX) * s + vp.X, wy * s + vp.Y);
        }

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
