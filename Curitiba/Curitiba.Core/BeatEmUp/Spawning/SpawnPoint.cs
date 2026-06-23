using System;
using Microsoft.Xna.Framework;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>Where an enemy enters the arena from. Left/Right are camera-relative edges (the
    /// enemy is born just off that side of the screen and walks in); Custom is an explicit world
    /// point the designer places (e.g. an alley or doorway), which may itself sit off-screen.</summary>
    internal enum SpawnPointType
    {
        Left,
        Right,
        Custom,
    }

    /// <summary>
    /// A named entry point for a section, resolved at runtime from a <c>SpawnPointDef</c>. The
    /// spawn system asks it for the off-screen birth position; the enemy then walks from there to a
    /// target inside the playable area (see <see cref="SpawnManager"/>).
    /// </summary>
    internal sealed class SpawnPoint
    {
        public string Id { get; }
        public string Name { get; }

        /// <summary>For Custom this is the world birth point; for Left/Right only the Y (lane) is used.</summary>
        public Vector2 Position { get; }

        public SpawnPointType Type { get; }

        public SpawnPoint(string id, string name, Vector2 position, SpawnPointType type)
        {
            Id = id;
            Name = name;
            Position = position;
            Type = type;
        }

        /// <summary>
        /// The off-screen world position an enemy is born at. Left/Right place it
        /// <paramref name="margin"/> px beyond the current view edge on the point's lane (Y);
        /// Custom returns the authored position verbatim.
        /// </summary>
        public Vector2 ResolveSpawnPosition(Camera2D camera, float sectionWidth, float margin)
        {
            switch (Type)
            {
                case SpawnPointType.Left:
                    return new Vector2(camera.Left - margin, Position.Y);
                case SpawnPointType.Right:
                    return new Vector2(Math.Min(camera.Right, sectionWidth) + margin, Position.Y);
                default:
                    return Position;
            }
        }
    }
}
