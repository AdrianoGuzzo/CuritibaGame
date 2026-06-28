using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Curitiba.Core.Effects
{
    public class ParticleManager
    {
        private Random random;

        private Vector2 position;
        /// <summary>
        /// Position where these particles eminate from
        /// </summary>
        public Vector2 Position
        {
            get => position;
            set => position = value;
        }

        private Vector2 textureOrigin;
        private Texture2D texture;
        /// <summary>
        /// Texture to be used for this set of particles
        /// </summary>
        public Texture2D Texture
        {
            get => texture;
            set => texture = value;
        }

        private List<Particle> particles;
        /// <summary>
        /// How many particles still left to be shown
        /// </summary>
        public int ParticleCount => particles != null ? particles.Count : 0;

        private bool hasFinishedEmitting;
        /// <summary>
        /// Indicates whether all particles have finished
        /// </summary>
        public bool Finished => hasFinishedEmitting && ParticleCount == 0;

        /// <summary>
        /// ParticleManager constructor
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="position"></param>
        public ParticleManager(Texture2D texture, Vector2 position)
        {
            this.particles = new List<Particle>();
            this.random = new Random();
            this.texture = texture;
            this.textureOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
            this.position = position;
        }

        /// <summary>
        /// Emit built-in particles based on the effect type
        /// </summary>
        /// <param name="numberOfParticles"></param>
        /// <param name="effectType"></param>
        /// <param name="color"></param>
        public void Emit(int numberOfParticles, ParticleEffectType effectType, Color? color = null)
        {
            hasFinishedEmitting = false;

            switch (effectType)
            {
                case ParticleEffectType.Confetti:
                    EmitConfetti(numberOfParticles, position, color);
                    break;
                case ParticleEffectType.Explosions:
                    EmitExplosions(numberOfParticles, position, color);
                    break;
                case ParticleEffectType.Fireworks:
                    EmitFireworks(numberOfParticles, position, color);
                    break;
                case ParticleEffectType.Sparkles:
                    EmitSparkles(numberOfParticles, position, color);
                    break;
            }

            hasFinishedEmitting = true;
        }

        /// <summary>
        /// Emit particles for Confetti effect
        /// </summary>
        /// <param name="numberOfParticles"></param>
        /// <param name="emitPosition"></param>
        /// <param name="color"></param>
        private void EmitConfetti(int numberOfParticles, Vector2 emitPosition, Color? color = null)
        {
            for (int i = 0; i < numberOfParticles; i++)
            {
                Vector2 randomDirection = new Vector2(
                    (float)(random.NextDouble() * 2 - 1),
                    (float)random.NextDouble()
                );

                randomDirection.Normalize();

                float speed = (float)random.NextDouble() * 200 + 50;

                Vector2 velocity = new Vector2((float)(random.NextDouble() * 2 - 1), (float)random.NextDouble()) * 200;
                float lifetime = (float)random.NextDouble() * 3f + 1f;

                Color actualParticleColor = color ?? new Color(random.Next(256), random.Next(256), random.Next(256));

                float scale = (float)random.NextDouble() * 0.5f + 0.3f;

                var particle = new Particle(emitPosition, randomDirection, speed, lifetime, actualParticleColor, scale);
                particles.Add(particle);
            }
        }

        /// <summary>
        /// Emit particles for Explosions effect
        /// </summary>
        /// <param name="numberOfParticles"></param>
        /// <param name="emitPosition"></param>
        /// <param name="color"></param>
        private void EmitExplosions(int numberOfParticles, Vector2 emitPosition, Color? color = null)
        {
            for (int i = 0; i < numberOfParticles; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2);
                Vector2 direction = new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                );

                float speed = (float)(random.NextDouble() * 300 + 100);

                float lifetime = (float)random.NextDouble() * 1.5f + 0.5f;

                Color actualParticleColor = color ?? new Color(
                    random.Next(200, 256),
                    random.Next(100, 200),
                    random.Next(0, 100)
                );

                float scale = (float)random.NextDouble() * 0.5f + 0.2f;

                var particle = new Particle(emitPosition, direction, speed, lifetime, actualParticleColor, scale, 10);

                particles.Add(particle);
            }
        }

        /// <summary>
        /// Emit particles for Fireworks effect
        /// </summary>
        /// <param name="numberOfParticles"></param>
        /// <param name="emitPosition"></param>
        /// <param name="color"></param>
        private void EmitFireworks(int numberOfParticles, Vector2 emitPosition, Color? color = null)
        {
            for (int i = 0; i < numberOfParticles; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2);

                Vector2 direction = new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                );

                float speed = (float)random.NextDouble() * 300 + 100;

                float lifetime = (float)random.NextDouble() * 2f + 1f;

                Color actualParticleColor = color ?? new Color(
                    random.Next(256),
                    random.Next(256),
                    random.Next(256)
                );

                float scale = (float)random.NextDouble() * 0.5f + 0.5f;

                var particle = new Particle(emitPosition, direction, speed, lifetime, actualParticleColor, scale);

                particle.OnDeath += FireworkParticle_OnDeath;

                particles.Add(particle);
            }
        }

        /// <summary>
        /// Emit particles for Sparkles effect
        /// </summary>
        /// <param name="numberOfParticles"></param>
        /// <param name="emitPosition"></param>
        /// <param name="color"></param>
        private void EmitSparkles(int numberOfParticles, Vector2 emitPosition, Color? color = null)
        {
            for (int i = 0; i < numberOfParticles; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2);
                Vector2 direction = new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                );

                float speed = (float)(random.NextDouble() * 300);

                float lifetime = (float)random.NextDouble() * 1f + 0.5f;

                Color actualParticleColor = color ?? Color.White * ((float)random.NextDouble() * 0.5f + 0.5f);

                float scale = (float)random.NextDouble() * 0.5f + 0.2f;

                var particle = new Particle(emitPosition, direction, speed, lifetime, actualParticleColor, scale);

                particles.Add(particle);
            }
        }

        /// <summary>
        /// Event fireed when the Fireworks particle dies
        /// </summary>
        /// <param name="particlePosition"></param>
        private void FireworkParticle_OnDeath(Vector2 particlePosition)
        {
            EmitExplosions(5, particlePosition);
        }

        /// <summary>
        /// Update each Particle that is still alive
        /// </summary>
        /// <param name="gameTime"></param>
        public void Update(GameTime gameTime)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                particles[i].Update(gameTime);

                if (!particles[i].IsAlive)
                {
                    particles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Controls the "density" of the tail
        /// Dense Tail (t += 1f): A continuous, almost solid-looking trail.Ideal for effects like glowing streaks.
    	/// Sparse Tail (t += 10f): A dotted, fragmented appearance.Useful for effects like spark trails or light debris.
        /// </summary>
        const float tailDensity = 5f;

        /// <summary>
        /// Draws all active particles and their corresponding tails.
        /// </summary>
        /// <param name="spriteBatch">The SpriteBatch used to draw the particles.</param>
        public void Draw(SpriteBatch spriteBatch)
        {
            foreach (Particle particle in particles)
            {
                if (particle.IsAlive)
                {
                    Vector2 tailDirection = particle.Position - particle.PreviousPosition;
                    float tailLength = particle.TailLength * tailDirection.Length();

                    if (tailDirection != Vector2.Zero)
                        tailDirection.Normalize();

                    spriteBatch.Draw(
                        texture,
                        particle.Position,
                        null,
                        particle.Color,
                        0.0f,
                        textureOrigin,
                        particle.Scale,
                        SpriteEffects.None,
                        0f);

                    for (float t = 0; t < tailLength; t += tailDensity)
                    {
                        Vector2 tailPosition = particle.Position - tailDirection * t;

                        float alpha = MathHelper.Clamp(1f - (t / tailLength), 0f, 1f);
                        Color tailColor = particle.Color * alpha;

                        spriteBatch.Draw(
                            texture,
                            tailPosition,
                            null,
                            tailColor,
                            0f,
                            textureOrigin,
                            particle.Scale * 0.8f,
                            SpriteEffects.None,
                            0f);
                    }
                }
            }
        }
    }
}