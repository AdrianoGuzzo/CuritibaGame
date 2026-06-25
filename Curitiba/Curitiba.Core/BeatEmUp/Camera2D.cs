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

        /// <summary>Furthest the camera's left edge may scroll right (the advance lock).
        /// Set instantly when an area unlocks; the smooth pan comes from <see cref="Follow"/>
        /// easing the camera position toward this bound, not from animating the bound itself.</summary>
        public float MaxAdvanceX { get; set; }

        // Exponential smoothing rate (per second) for the camera position easing to its target.
        // Higher = tighter/less lag while walking; lower = softer pan. High enough that normal
        // walking (175 px/s) keeps up with only a few px of lag, yet an unlock jump eases over ~0.4s.
        private const float FollowLerpRate = 12f;

        public float ViewWidth { get; }
        public float WorldWidth { get; }

        // Hit-impact shake: a brief jitter added to the draw transform when a blow lands. The RNG is
        // static and NextDouble() does not allocate, so the per-frame TickShake stays hot-path safe.
        private static readonly Random ShakeRng = new Random();
        private float shakeMagnitude;
        private float shakeDuration;
        private float shakeTimer;
        private Vector2 shakeOffset;

        public Camera2D(float viewWidth, float worldWidth)
        {
            ViewWidth = viewWidth;
            WorldWidth = worldWidth;
            MaxAdvanceX = worldWidth - viewWidth;
        }

        public float Left => X;
        public float Right => X + ViewWidth;

        private float DesiredX(Vector2 focus)
        {
            float target = focus.X - ViewWidth / 2f;
            float upperBound = Math.Max(0f, Math.Min(MaxAdvanceX, WorldWidth - ViewWidth));
            return MathHelper.Clamp(target, 0f, upperBound);
        }

        /// <summary>
        /// Eases the camera toward the focus (clamped by the advance lock). The easing is what makes
        /// an unlock pan smoothly: the lock can jump instantly, but the camera glides to catch up
        /// instead of snapping — while still tracking a walking player at full speed.
        /// </summary>
        public void Follow(Vector2 focus, float dt)
        {
            float desired = DesiredX(focus);
            if (Math.Abs(desired - X) < 0.5f)
            {
                X = desired; // snap the last fraction so X reaches the lock exactly (arm check)
                return;
            }
            X = MathHelper.Lerp(X, desired, 1f - (float)Math.Exp(-FollowLerpRate * dt));
        }

        /// <summary>Instantly positions the camera on the focus (section open: avoids a one-frame jump).</summary>
        public void Snap(Vector2 focus) => X = DesiredX(focus);

        /// <summary>Editor-only free pan: positions the camera anywhere in the world, ignoring the advance lock.</summary>
        internal void SetX(float x)
        {
            X = MathHelper.Clamp(x, 0f, Math.Max(0f, WorldWidth - ViewWidth));
        }

        /// <summary>Kicks off an impact shake of <paramref name="magnitude"/> px for
        /// <paramref name="duration"/> seconds (re-arming overrides any shake in progress).</summary>
        public void Shake(float magnitude, float duration)
        {
            shakeMagnitude = magnitude;
            shakeDuration = duration;
            shakeTimer = duration;
        }

        /// <summary>Advances the shake, recomputing the per-frame offset (which fades out over the
        /// shake's life). Safe to call every frame, including while the world is frozen for hit stop
        /// so the impact still reads.</summary>
        public void TickShake(float dt)
        {
            if (shakeTimer <= 0f)
            {
                shakeOffset = Vector2.Zero;
                return;
            }

            shakeTimer -= dt;
            float falloff = MathHelper.Clamp(shakeTimer / shakeDuration, 0f, 1f);
            float m = shakeMagnitude * falloff;
            shakeOffset = new Vector2(
                (float)(ShakeRng.NextDouble() * 2.0 - 1.0) * m,
                (float)(ShakeRng.NextDouble() * 2.0 - 1.0) * m);
        }

        /// <summary>Translation matrix for drawing world-space content (rounded to avoid jitter),
        /// plus the current impact-shake offset.</summary>
        public Matrix GetTransform() => Matrix.CreateTranslation(
            -(float)Math.Round(X) + (float)Math.Round(shakeOffset.X),
            (float)Math.Round(shakeOffset.Y), 0f);
    }
}
