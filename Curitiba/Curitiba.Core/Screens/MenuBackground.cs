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
        // Slow horizontal drift of the sky, in virtual pixels per second.
        private const float ScrollSpeed = 10.0f;

        private Texture2D sky;
        private Texture2D garden;

        // Current horizontal offset of the sky, wrapped to its scaled width.
        private float scrollOffset;

        // Sky width once scaled to the virtual screen height (computed on load).
        private float scaledSkyWidth;

        /// <summary>
        /// Loads the sky and garden textures.
        /// </summary>
        public void Load(ContentManager content, ScreenManager screenManager)
        {
            sky = content.Load<Texture2D>("Backgrounds/Menu/Sky");
            garden = content.Load<Texture2D>("Backgrounds/Menu/Garden");

            // Scale the sky to the virtual screen height (480) and remember the
            // resulting width so we can tile/wrap it horizontally.
            scaledSkyWidth = sky.Width * (screenManager.BaseScreenSize.Y / sky.Height);
        }

        /// <summary>
        /// Advances the slow horizontal scroll of the sky.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            scrollOffset += ScrollSpeed * elapsed;

            // Wrap so the offset stays within a single sky tile width.
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

            // Sky: scaled to fill the screen height and tiled horizontally so it
            // loops seamlessly. Draw enough copies to cover the screen width,
            // starting one tile to the left to fill the scrolled-in gap.
            float skyScale = screenHeight / sky.Height;
            for (float x = -scrollOffset; x < screenWidth; x += scaledSkyWidth)
            {
                spriteBatch.Draw(sky, new Vector2(x, 0.0f), null, tint, 0.0f,
                                 Vector2.Zero, skyScale, SpriteEffects.None, 0.0f);
            }

            // Garden: scaled to fill the whole virtual screen, drawn on top.
            spriteBatch.Draw(garden,
                             new Rectangle(0, 0, (int)screenWidth, (int)screenHeight),
                             tint);

            spriteBatch.End();
        }
    }
}
