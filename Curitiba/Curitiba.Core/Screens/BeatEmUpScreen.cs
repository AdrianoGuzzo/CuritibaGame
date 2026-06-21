using System;
using Curitiba.Core.BeatEmUp;
using Curitiba.Core.Inputs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Curitiba.Screens
{
    /// <summary>
    /// Hosts the beat 'em up demo: the Capão Raso stage. Mirrors the structure of
    /// <see cref="GameplayScreen"/> — it owns the arena, routes input to it while
    /// active, and hands off to the end-of-demo screen on completion (or restarts
    /// the stage if Sofia is defeated).
    /// </summary>
    internal class BeatEmUpScreen : GameScreen
    {
        private ContentManager content;
        private SpriteBatch spriteBatch;
        private CapaoRasoArena arena;
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

            arena = new CapaoRasoArena(ScreenManager, content);

            ScreenManager.Game.ResetElapsedTime();
        }

        public override void UnloadContent()
        {
            content.Unload();
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, false);

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
