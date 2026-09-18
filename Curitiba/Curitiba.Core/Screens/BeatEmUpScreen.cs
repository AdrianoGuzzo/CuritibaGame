using System;
using System.IO;
using Curitiba.Core;
using Curitiba.Core.Audio;
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
        private readonly StageReloadPolicy reloadPolicy = new StageReloadPolicy();
        private IDevEditor devEditor;
        private EditorContext editorContext;
        private string savePath;
        private TouchControls touchControls;
        private float pauseAlpha;
        private bool transitioningOut;

        private const float TransitionOnSeconds = 1.0f;
        private const float TransitionOffSeconds = 0.5f;

        private IMusicPlayer musicPlayer;
        private ISoundPlayer soundPlayer;

        // The fades share the screen's own transitions, so there is no second pair of numbers to
        // keep in sync: the track is up once the stage is, and silent once the screen is gone.
        private readonly ArenaMusicPolicy arenaMusic =
            new ArenaMusicPolicy(TransitionOnSeconds, TransitionOffSeconds);

        public BeatEmUpScreen()
        {
            TransitionOnTime = TimeSpan.FromSeconds(TransitionOnSeconds);
            TransitionOffTime = TimeSpan.FromSeconds(TransitionOffSeconds);
        }

        public override void LoadContent()
        {
            base.LoadContent();

            content ??= new ContentManager(ScreenManager.Game.Services, "Content");
            spriteBatch = ScreenManager.SpriteBatch;

            // Resolved before the arena, which takes it: the stage owns its impact sounds, and a
            // rebuild must not be the frame the fight goes quiet.
            soundPlayer ??= ScreenManager.Game.Services.GetService<ISoundPlayer>();

            stageDefinition = StageLoader.LoadOrDefault(StageLoader.CapaoRasoTitlePath, StageDefinition.CapaoRasoDefault);
            arena = BuildArena();

            if (CuritibaGame.IsDesktop)
                hotReloader = StageHotReloader.TryCreate(StageLoader.ResolveWritableStagesDir());

            // Touch devices have no keyboard or pad, so the stage is unplayable without these.
            if (CuritibaGame.IsMobile)
                touchControls = new TouchControls(ScreenManager.GraphicsDevice, ScreenManager.BaseScreenSize, ScreenManager.Font);

            devEditor = ScreenManager.Game.Services.GetService(typeof(IDevEditor)) as IDevEditor;
            musicPlayer ??= ScreenManager.Game.Services.GetService<IMusicPlayer>();
            string dir = hotReloader?.WatchedDirectory ?? StageLoader.ResolveWritableStagesDir();
            savePath = dir != null ? Path.Combine(dir, StageLoader.CapaoRasoFileName) : null;
            RefreshEditorContext();

            ScreenManager.Game.ResetElapsedTime();
        }

        public override void UnloadContent()
        {
            arenaMusic.Stop(musicPlayer);
            devEditor?.SetContext(null);
            hotReloader?.Dispose();
            hotReloader = null;
            touchControls?.Dispose();
            touchControls = null;
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
        private void RecreateArena() => arena = BuildArena();

        /// <summary>The single place a stage is built, so every rebuild keeps its sound channel.</summary>
        private CapaoRasoArena BuildArena() =>
            new CapaoRasoArena(ScreenManager, content, stageDefinition, soundPlayer);

        /// <summary>
        /// On the game thread, rebuilds the arena when the stage JSON changes. The retry budget
        /// covers the brief window where an editor has the file locked mid-save; invalid JSON
        /// simply leaves the current arena untouched until the next save.
        /// </summary>
        private void PollHotReload()
        {
            if (hotReloader == null)
                return;

            bool changed = hotReloader.TryConsume(out _);
            string file = Path.Combine(hotReloader.WatchedDirectory, StageLoader.CapaoRasoFileName);

            StageDefinition reloaded = null;
            if (reloadPolicy.ShouldRebuild(changed, () => StageLoader.TryLoadFile(file, out reloaded)))
            {
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

            // Ahead of the exit block on purpose: BeginExit lands on the same frame that hands the
            // screen over, so the fade rides the transition off instead of cutting. coveredByOtherScreen
            // is the pause menu being up, the same signal pauseAlpha runs on.
            arenaMusic.Update((float)gameTime.ElapsedGameTime.TotalSeconds, musicPlayer, coveredByOtherScreen);

            if (IsActive && !transitioningOut)
            {
                if (arena.Completed)
                {
                    transitioningOut = true;
                    arenaMusic.BeginExit();
                    LoadingScreen.Load(ScreenManager, false, ControllingPlayer, new BackgroundScreen(), new EndOfDemoScreen());
                }
                else if (arena.PlayerDefeated)
                {
                    transitioningOut = true;
                    arenaMusic.BeginExit();
                    LoadingScreen.Load(ScreenManager, true, ControllingPlayer, new BeatEmUpScreen());
                }
            }
        }

        public override void HandleInput(GameTime gameTime, InputState inputState)
        {
            ArgumentNullException.ThrowIfNull(inputState);

            base.HandleInput(gameTime, inputState);

            if (devEditor != null && devEditor.IsOpen)
                return;

            // Folded into player one's pad before anything reads it, so the arena and SofiaPlayer
            // see touch exactly as they see a physical controller.
            if (touchControls != null)
            {
                touchControls.Update(inputState.CurrentTouchState, inputState);
                inputState.CurrentGamePadStates[0] = touchControls.Merge(inputState.CurrentGamePadStates[0]);
            }

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

            if (touchControls != null && IsActive)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, ScreenManager.GlobalTransformation);
                touchControls.Draw(spriteBatch);
                spriteBatch.End();
            }

            base.Draw(gameTime);

            if (TransitionPosition > 0 || pauseAlpha > 0)
            {
                float alpha = MathHelper.Lerp(1f - TransitionAlpha, 1f, pauseAlpha / 2);
                ScreenManager.FadeBackBufferToBlack(alpha);
            }
        }
    }
}
