using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Curitiba.Screens
{
    /// <summary>
    /// The BackgroundScreen renders a static background image behind all other menu screens.
    /// It remains fixed and unaffected by transitions on top of it.
    /// </summary>
    internal class BackgroundScreen : GameScreen
    {
        ContentManager content;
        MenuBackground menuBackground = new MenuBackground();

        /// <summary>
        /// Initializes a new instance of the BackgroundScreen class.
        /// Sets the transition times for screen appearance and disappearance.
        /// </summary>
        public BackgroundScreen()
        {
            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);
        }

        /// <summary>
        /// Loads the background texture using a local ContentManager.
        /// This allows for independent unloading of the background texture.
        /// </summary>
        public override void LoadContent()
        {
            if (content == null)
                content = new ContentManager(ScreenManager.Game.Services, "Content");

            menuBackground.Load(content, ScreenManager);
        }

        /// <summary>
        /// Unloads the background texture by unloading the local ContentManager.
        /// </summary>
        public override void UnloadContent()
        {
            content.Unload();
        }

        /// <summary>
        /// Updates the background screen.
        /// Forces the screen to remain active even when covered by other screens.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        /// <param name="otherScreenHasFocus">Indicates whether another screen has focus.</param>
        /// <param name="coveredByOtherScreen">Indicates whether the screen is covered by another screen.</param>
        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            menuBackground.Update(gameTime);

            base.Update(gameTime, otherScreenHasFocus, false);
        }

        /// <summary>
        /// Draws the background screen.
        /// Clears the screen and renders the background texture with transition alpha.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public override void Draw(GameTime gameTime)
        {
            ScreenManager.GraphicsDevice.Clear(ClearOptions.Target, Color.Black, 0, 0);

            menuBackground.Draw(ScreenManager.SpriteBatch, ScreenManager, Matrix.Identity, TransitionAlpha);
        }
    }
}