using Microsoft.Xna.Framework.Graphics;

namespace Curitiba.Core
{
    /// <summary>
    /// Represents an animated texture.
    /// </summary>
    /// <remarks>
    /// By default each frame is assumed to be as wide as the strip is tall
    /// (square frames), and the frame count is inferred from that. A non-square
    /// frame width can be supplied to the constructor for strips whose frames are
    /// wider than tall (e.g. Sofia's dash, which stretches horizontally).
    /// </remarks>
    internal class Animation
    {
        /// <summary>
        /// All frames in the animation arranged horizontally.
        /// </summary>
        public Texture2D Texture
        {
            get { return texture; }
        }
        Texture2D texture;

        /// <summary>
        /// Duration of time to show each frame.
        /// </summary>
        public float FrameTime
        {
            get { return frameTime; }
        }
        float frameTime;

        /// <summary>
        /// When the end of the animation is reached, should it
        /// continue playing from the beginning?
        /// </summary>
        public bool IsLooping
        {
            get { return isLooping; }
        }
        bool isLooping;

        /// <summary>
        /// Width of a single frame in the strip. Defaults to the strip height (square frames).
        /// </summary>
        int frameWidth;

        /// <summary>
        /// Gets the number of frames in the animation.
        /// </summary>
        public int FrameCount
        {
            get { return Texture.Width / FrameWidth; }
        }

        /// <summary>
        /// Gets the width of a frame in the animation.
        /// </summary>
        public int FrameWidth
        {
            get { return frameWidth; }
        }

        /// <summary>
        /// Gets the height of a frame in the animation.
        /// </summary>
        public int FrameHeight
        {
            get { return Texture.Height; }
        }

        /// <summary>
        /// Constructs a new animation with the specified texture, frame duration, and looping behavior.
        /// </summary>
        /// <param name="texture">The texture containing the animation frames.</param>
        /// <param name="frameTime">The duration (in seconds) each frame should be displayed.</param>
        /// <param name="isLooping">Indicates whether the animation should loop continuously.</param>
        /// <param name="frameWidth">Width of a single frame, in pixels. Pass 0 (the default) for square frames (width = strip height).</param>
        public Animation(Texture2D texture, float frameTime, bool isLooping, int frameWidth = 0)
        {
            this.texture = texture;
            this.frameTime = frameTime;
            this.isLooping = isLooping;
            this.frameWidth = frameWidth > 0 ? frameWidth : texture.Height;
        }
    }
}