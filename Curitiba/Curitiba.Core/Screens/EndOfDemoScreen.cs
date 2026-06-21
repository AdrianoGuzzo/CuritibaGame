using System;
using Curitiba.Core.Inputs;
using Curitiba.Core.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Curitiba.Screens
{
    /// <summary>
    /// The "Fim da Demo" screen shown after the Capão Raso stage is completed.
    /// Any confirmation/cancel input returns to the main menu.
    /// </summary>
    internal class EndOfDemoScreen : GameScreen
    {
        public EndOfDemoScreen()
        {
            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);
        }

        public override void HandleInput(GameTime gameTime, InputState inputState)
        {
            base.HandleInput(gameTime, inputState);

            if (inputState.IsMenuSelect(ControllingPlayer, out _) || inputState.IsMenuCancel(ControllingPlayer, out _))
            {
                LoadingScreen.Load(ScreenManager, false, ControllingPlayer, new BackgroundScreen(), new MainMenuScreen());
            }
        }

        public override void Draw(GameTime gameTime)
        {
            SpriteBatch spriteBatch = ScreenManager.SpriteBatch;
            SpriteFont font = ScreenManager.Font;

            string title = Resources.EndOfDemo;
            string hint = Resources.PressEnterToContinue;

            Vector2 titleSize = font.MeasureString(title);
            Vector2 hintSize = font.MeasureString(hint);
            Vector2 center = ScreenManager.BaseScreenSize / 2f;

            Vector2 titlePosition = new Vector2(center.X - titleSize.X / 2f, center.Y - titleSize.Y);
            Vector2 hintPosition = new Vector2(center.X - hintSize.X / 2f, center.Y + 24f);

            float alpha = TransitionAlpha;

            spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, ScreenManager.GlobalTransformation);
            spriteBatch.DrawString(font, title, titlePosition + new Vector2(2f, 2f), Color.Black * alpha);
            spriteBatch.DrawString(font, title, titlePosition, Color.White * alpha);
            spriteBatch.DrawString(font, hint, hintPosition, Color.LightGray * alpha);
            spriteBatch.End();
        }
    }
}
