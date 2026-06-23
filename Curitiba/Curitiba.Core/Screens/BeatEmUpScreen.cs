using System;
using System.IO;
using Curitiba.Core;
using Curitiba.Core.BeatEmUp;
using Curitiba.Core.DevTools;
using Curitiba.Core.Inputs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Curitiba.Screens
{
    /// <summary>
    /// Hosts the beat 'em up demo: the Capão Raso stage. It owns the arena, routes
    /// input to it while active, and hands off to the end-of-demo screen on completion
    /// (or restarts the stage if Sofia is defeated).
    /// </summary>
    internal class BeatEmUpScreen : GameScreen
    {
        private ContentManager content;
        private SpriteBatch spriteBatch;
        private CapaoRasoArena arena;
        private StageDefinition stageDefinition;
        private StageHotReloader hotReloader;
        private int reloadRetries;
        private IDevEditor devEditor;
        private EditorContext editorContext;
        private string savePath;
        private float pauseAlpha;
        private bool transitioningOut;

        public BeatEmUpScreen()
        {
            TransitionOnTime = TimeSpan.FromSeconds(1.0);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);
        }

        public override void LoadContent()
        {
            base.LoadContent();

            content ??= new ContentManager(ScreenManager.Game.Services, "Content");
            spriteBatch = ScreenManager.SpriteBatch;

            stageDefinition = StageLoader.LoadOrDefault(StageLoader.CapaoRasoTitlePath, StageDefinition.CapaoRasoDefault);
            arena = new CapaoRasoArena(ScreenManager, content, stageDefinition);

            // Hot-reload of the stage JSON, desktop dev only (no-op when the folder isn't found).
            if (CuritibaGame.IsDesktop)
                hotReloader = StageHotReloader.TryCreate(StageLoader.ResolveWritableStagesDir());

            // Wire the in-game editor (a no-op everywhere but desktop Debug).
            devEditor = ScreenManager.Game.Services.GetService(typeof(IDevEditor)) as IDevEditor;
            string dir = hotReloader?.WatchedDirectory ?? StageLoader.ResolveWritableStagesDir();
            savePath = dir != null ? Path.Combine(dir, StageLoader.CapaoRasoFileName) : null;
            RefreshEditorContext();

            ScreenManager.Game.ResetElapsedTime();
        }

        public override void UnloadContent()
        {
            devEditor?.SetContext(null);
            hotReloader?.Dispose();
            hotReloader = null;
            content.Unload();
        }

        private void RefreshEditorContext()
        {
            if (devEditor == null)
                return;

            editorContext ??= new EditorContext();
            editorContext.Definition = stageDefinition;
            editorContext.Arena = arena;
            editorContext.ScreenManager = ScreenManager;
            editorContext.Rebuild = RebuildArena;
            editorContext.Replace = ReplaceDefinition;
            editorContext.SavePath = savePath;
            devEditor.SetContext(editorContext);
        }

        private void RebuildArena() => RecreateArena();

        private void ReplaceDefinition(StageDefinition def)
        {
            stageDefinition = def;
            RecreateArena();
        }

        /// <summary>
        /// Rebuilds the arena from the current definition. The new arena always starts at section 0;
        /// while the editor is open it pins the view back to the edited section/camera every frame
        /// (see <c>ImGuiDevEditor.EnforceSection</c>), so applying/saving doesn't snap to section 0.
        /// </summary>
        private void RecreateArena() => arena = new CapaoRasoArena(ScreenManager, content, stageDefinition);

        /// <summary>
        /// On the game thread, rebuilds the arena when the stage JSON changes. The retry budget
        /// covers the brief window where an editor has the file locked mid-save; invalid JSON
        /// simply leaves the current arena untouched until the next save.
        /// </summary>
        private void PollHotReload()
        {
            if (hotReloader == null)
                return;

            if (hotReloader.TryConsume(out _))
                reloadRetries = 20;

            if (reloadRetries <= 0)
                return;

            reloadRetries--;
            string file = Path.Combine(hotReloader.WatchedDirectory, StageLoader.CapaoRasoFileName);
            if (StageLoader.TryLoadFile(file, out StageDefinition reloaded))
            {
                reloadRetries = 0;
                stageDefinition = reloaded;
                RecreateArena();
            }
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, false);

            PollHotReload();
            RefreshEditorContext();

            if (coveredByOtherScreen)
                pauseAlpha = Math.Min(pauseAlpha + 1f / 32, 1);
            else
                pauseAlpha = Math.Max(pauseAlpha - 1f / 32, 0);

            if (IsActive && !transitioningOut)
            {
                if (arena.Completed)
                {
                    transitioningOut = true;
                    LoadingScreen.Load(ScreenManager, false, ControllingPlayer, new BackgroundScreen(), new EndOfDemoScreen());
                }
                else if (arena.PlayerDefeated)
                {
                    transitioningOut = true;
                    LoadingScreen.Load(ScreenManager, true, ControllingPlayer, new BeatEmUpScreen());
                }
            }
        }

        public override void HandleInput(GameTime gameTime, InputState inputState)
        {
            ArgumentNullException.ThrowIfNull(inputState);

            base.HandleInput(gameTime, inputState);

            // While the dev editor is open the scene is frozen and the editor owns all input.
            if (devEditor != null && devEditor.IsOpen)
                return;

            if (inputState.IsPauseGame(ControllingPlayer))
            {
                ScreenManager.AddScreen(new PauseScreen(), ControllingPlayer);
            }
            else
            {
                arena.Update(gameTime, inputState, ControllingPlayer);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            ScreenManager.GraphicsDevice.Clear(ClearOptions.Target, Color.Black, 0, 0);

            arena.Draw(gameTime, spriteBatch);

            base.Draw(gameTime);

            if (TransitionPosition > 0 || pauseAlpha > 0)
            {
                float alpha = MathHelper.Lerp(1f - TransitionAlpha, 1f, pauseAlpha / 2);
                ScreenManager.FadeBackBufferToBlack(alpha);
            }
        }
    }
}
