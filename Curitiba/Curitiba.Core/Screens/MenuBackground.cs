using Curitiba.ScreenManagers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Curitiba.Screens
{
    /// <summary>
    /// Two-layer menu backdrop: an opaque sky that drifts slowly and loops
    /// horizontally (clouds carried by the wind), with the botanical garden
    /// drawn static on top. The garden's transparent regions reveal the
    /// moving sky behind it.
    /// </summary>
    internal class MenuBackground
    {
        private const float ScrollSpeed = 10.0f;

        private Texture2D sky;
        private Texture2D garden;

        private float scrollOffset;

        private float scaledSkyWidth;

        /// <summary>
        /// Loads the sky and garden textures.
        /// </summary>
        public void Load(ContentManager content, ScreenManager screenManager)
        {
            sky = content.Load<Texture2D>("Backgrounds/Menu/Sky");
            garden = content.Load<Texture2D>("Backgrounds/Menu/Garden");

            scaledSkyWidth = sky.Width * (screenManager.BaseScreenSize.Y / sky.Height);
        }

        /// <summary>
        /// Advances the slow horizontal scroll of the sky.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            scrollOffset += ScrollSpeed * elapsed;

            if (scaledSkyWidth > 0.0f && scrollOffset >= scaledSkyWidth)
                scrollOffset -= scaledSkyWidth;
        }

        /// <summary>
        /// Draws the sky (looping) and the garden on top.
        /// </summary>
        /// <param name="spriteBatch">The SpriteBatch used to draw.</param>
        /// <param name="screenManager">Provides the global transformation and virtual screen size.</param>
        /// <param name="cinematic">An extra transform (zoom/pan) applied before the global transformation; pass <see cref="Matrix.Identity"/> for none.</param>
        /// <param name="alpha">Overall opacity (used for screen transition fades).</param>
        public void Draw(SpriteBatch spriteBatch, ScreenManager screenManager, Matrix cinematic, float alpha)
        {
            float screenWidth = screenManager.BaseScreenSize.X;
            float screenHeight = screenManager.BaseScreenSize.Y;
            Color tint = new Color(alpha, alpha, alpha);

            spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null,
                              cinematic * screenManager.GlobalTransformation);

            float skyScale = screenHeight / sky.Height;
            for (float x = -scrollOffset; x < screenWidth; x += scaledSkyWidth)
            {
                spriteBatch.Draw(sky, new Vector2(x, 0.0f), null, tint, 0.0f,
                                 Vector2.Zero, skyScale, SpriteEffects.None, 0.0f);
            }

            spriteBatch.Draw(garden,
                             new Rectangle(0, 0, (int)screenWidth, (int)screenHeight),
                             tint);

            spriteBatch.End();
        }
    }
}
