using System;
using Microsoft.Xna.Framework;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Horizontal scrolling camera that follows the player. The classic beat 'em up
    /// "lock" is implemented through <see cref="MaxAdvanceX"/>: while enemies remain
    /// in the current area the camera may not scroll past the lock point, which keeps
    /// the player penned in the arena until it is cleared.
    /// </summary>
    internal class Camera2D
    {
        /// <summary>Left edge of the view in world space.</summary>
        public float X { get; private set; }

        /// <summary>Furthest the camera's left edge may scroll right (the advance lock).</summary>
        public float MaxAdvanceX { get; set; }

        public float ViewWidth { get; }
        public float WorldWidth { get; }

        public Camera2D(float viewWidth, float worldWidth)
        {
            ViewWidth = viewWidth;
            WorldWidth = worldWidth;
            MaxAdvanceX = worldWidth - viewWidth;
        }

        public float Left => X;
        public float Right => X + ViewWidth;

        public void Follow(Vector2 focus)
        {
            float target = focus.X - ViewWidth / 2f;
            float upperBound = Math.Max(0f, Math.Min(MaxAdvanceX, WorldWidth - ViewWidth));
            X = MathHelper.Clamp(target, 0f, upperBound);
        }

        /// <summary>Translation matrix for drawing world-space content (rounded to avoid jitter).</summary>
        public Matrix GetTransform() => Matrix.CreateTranslation(-(float)Math.Round(X), 0f, 0f);
    }
}
