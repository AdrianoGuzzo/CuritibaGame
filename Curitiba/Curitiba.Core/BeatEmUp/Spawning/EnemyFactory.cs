using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>A fully-resolved request to create one enemy: where it is born (off-screen), where it
    /// walks in to, and its combat profile/tuning/difficulty.</summary>
    internal struct EnemySpawnRequest
    {
        public Vector2 SpawnPosition;   // off-screen birth point
        public Vector2 EntryTarget;     // walk-in destination inside the playable area
        public EnemyProfile Profile;
        public FighterTuning Tuning;
        public int HitsToKnockdown;
    }

    /// <summary>
    /// Creates enemies by a string <c>type</c>. Kept behind an interface so the spawn system depends
    /// on the abstraction, not the concrete enemy classes (DIP), and Open/Closed via a registry:
    /// new types (a boss, a different mook) register a builder without touching the call sites.
    /// Unknown types fall back to the default mook so authoring mistakes never crash a stage.
    /// </summary>
    internal interface IEnemyFactory
    {
        PiaLocoEnemy Create(string type, EnemySpawnRequest request);
    }

    /// <summary>
    /// Default factory. Today every type maps to a <see cref="PiaLocoEnemy"/> variation (tuning and
    /// personality vary, the class does not), because the live-enemy list is typed to that class;
    /// a future common enemy base/interface would let distinct classes (e.g. a boss) be registered
    /// here without changing the spawn pipeline.
    /// </summary>
    internal sealed class EnemyFactory : IEnemyFactory
    {
        public const string DefaultType = "piaLoco";

        private readonly ContentManager content;
        private readonly Texture2D blank;
        private readonly SofiaPlayer target;
        private readonly IReadOnlyList<PiaLocoEnemy> neighbors;
        private readonly AttackSlotManager slots;
        private readonly Dictionary<string, Func<EnemySpawnRequest, PiaLocoEnemy>> registry;

        public EnemyFactory(ContentManager content, Texture2D blank, SofiaPlayer target,
                            IReadOnlyList<PiaLocoEnemy> neighbors, AttackSlotManager slots)
        {
            this.content = content;
            this.blank = blank;
            this.target = target;
            this.neighbors = neighbors;
            this.slots = slots;
            registry = new Dictionary<string, Func<EnemySpawnRequest, PiaLocoEnemy>>(StringComparer.OrdinalIgnoreCase)
            {
                [DefaultType] = BuildPiaLoco,
            };
        }

        /// <summary>Registers (or replaces) the builder for a type. Extension seam for new enemies.</summary>
        public void Register(string type, Func<EnemySpawnRequest, PiaLocoEnemy> builder)
        {
            if (!string.IsNullOrEmpty(type) && builder != null)
                registry[type] = builder;
        }

        public PiaLocoEnemy Create(string type, EnemySpawnRequest request)
        {
            if (type == null || !registry.TryGetValue(type, out Func<EnemySpawnRequest, PiaLocoEnemy> build))
                build = registry[DefaultType];
            return build(request);
        }

        private PiaLocoEnemy BuildPiaLoco(EnemySpawnRequest r)
        {
            var enemy = new PiaLocoEnemy(content, blank, r.SpawnPosition, target,
                                         r.HitsToKnockdown, slots, neighbors, r.Profile, r.Tuning);
            enemy.BeginEntry(r.EntryTarget); // born off-screen, walks in before engaging
            return enemy;
        }
    }
}
