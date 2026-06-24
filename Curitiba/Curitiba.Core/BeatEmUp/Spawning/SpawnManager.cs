using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Turns a wave's authored spawns (or its procedural fallback) into live enemies. Single
    /// responsibility: resolving <em>where</em> each enemy is born and <em>where</em> it walks in to,
    /// then delegating creation to the <see cref="IEnemyFactory"/>. It owns no game loop and no
    /// camera/phase state — the arena drives it and the <see cref="WaveManager"/> decides timing.
    ///
    /// Every enemy is born off-screen and given a target inside the locked play area:
    /// <list type="bullet">
    /// <item>Authored spawn with a <c>SpawnPoint</c> reference: born at that point (Left/Right edge or
    /// a Custom world point), walks to the authored X/Y target (or an auto spread slot if 0,0).</item>
    /// <item>Authored spawn without a reference: born off the screen edge nearest its X/Y, which is
    /// treated as the walk-in destination.</item>
    /// <item>No authored spawns: the legacy procedural spread, with each enemy now entering from the
    /// right edge and walking to its spread position.</item>
    /// </list>
    /// </summary>
    internal sealed class SpawnManager
    {
        private const float OffscreenMargin = 60f;

        // A mix of archetypes per wave so the crowd reads as coordinated-but-varied: an eager
        // attacker, a steady one, a cautious one that hangs back, and a runner that rushes in.
        private static readonly EnemyPersonality[] PersonalityMix =
        {
            EnemyPersonality.Aggressive,
            EnemyPersonality.Balanced,
            EnemyPersonality.Defensive,
            EnemyPersonality.Runner,
        };

        private readonly IEnemyFactory factory;
        private readonly List<PiaLocoEnemy> enemies;
        private readonly AttackSlotManager slots;
        private readonly Func<string, EnemyProfile> resolveProfile;   // personality name -> profile
        private readonly Func<string, FighterTuning> resolveTuning;   // enemy type/template -> tuning
        private readonly Random rng = new Random();

        // Per-section context (refreshed by Configure when a section loads).
        private Camera2D camera;
        private float sectionWidth;
        private float corridorTop;
        private float corridorBottom;
        private SpawnPoint[] points = Array.Empty<SpawnPoint>();

        public SpawnManager(IEnemyFactory factory, List<PiaLocoEnemy> enemies, AttackSlotManager slots,
                            Func<string, EnemyProfile> resolveProfile, Func<string, FighterTuning> resolveTuning)
        {
            this.factory = factory;
            this.enemies = enemies;
            this.slots = slots;
            this.resolveProfile = resolveProfile;
            this.resolveTuning = resolveTuning;
        }

        /// <summary>Binds the live per-section context the resolver needs (camera bounds, corridor, points).</summary>
        public void Configure(Camera2D camera, float sectionWidth, float corridorTop, float corridorBottom, SpawnPoint[] spawnPoints)
        {
            this.camera = camera;
            this.sectionWidth = sectionWidth;
            this.corridorTop = corridorTop;
            this.corridorBottom = corridorBottom;
            this.points = spawnPoints ?? Array.Empty<SpawnPoint>();
        }

        /// <summary>Materializes a wave into <c>enemies</c>. Each enemy starts off-screen and walks in.</summary>
        public void SpawnWave(SpawnArea area)
        {
            slots.Reset(); // fresh ring + attack tokens for the new crowd

            SpawnDef[] defs = area.SpawnDefs;
            if (defs != null && defs.Length > 0)
            {
                Vector2[] targets = ComputePlayTargets(defs.Length); // auto fallbacks for 0,0 targets
                for (int i = 0; i < defs.Length; i++)
                {
                    SpawnDef d = defs[i];
                    string type = string.IsNullOrEmpty(d.Type) ? d.Template : d.Type;
                    Vector2 target = (d.X != 0f || d.Y != 0f) ? new Vector2(d.X, ClampLane(d.Y)) : targets[i];

                    SpawnPoint point = ResolveSpawnPoint(d.SpawnPoint);
                    Vector2 birth = point != null
                        ? point.ResolveSpawnPosition(camera, sectionWidth, OffscreenMargin)
                        : OffscreenBirthNear(target);

                    enemies.Add(factory.Create(type, new EnemySpawnRequest
                    {
                        SpawnPosition = birth,
                        EntryTarget = target,
                        Profile = resolveProfile(d.Personality),
                        Tuning = resolveTuning(type),
                        HitsToKnockdown = area.HitsToKnockdown,
                    }));
                }
                return;
            }

            // Legacy procedural spread: enemies enter from the right edge (ahead of the player).
            int count = area.EnemyCount;
            Vector2[] spread = ComputePlayTargets(count);
            for (int i = 0; i < count; i++)
            {
                EnemyPersonality personality = PersonalityMix[i % PersonalityMix.Length];
                enemies.Add(factory.Create(EnemyFactory.DefaultType, new EnemySpawnRequest
                {
                    SpawnPosition = OffscreenRight(spread[i].Y),
                    EntryTarget = spread[i],
                    Profile = resolveProfile(personality.ToString()),
                    Tuning = resolveTuning(EnemyFactory.DefaultType),
                    HitsToKnockdown = area.HitsToKnockdown,
                }));
            }
        }

        // The classic spread inside the locked play area, alternating two depth lanes so the crowd
        // reads with depth. Mirrors the original CapaoRasoArena.SpawnWave placement.
        private Vector2[] ComputePlayTargets(int count)
        {
            var arr = new Vector2[Math.Max(0, count)];
            if (count <= 0)
                return arr;

            float right = Math.Min(camera.Right, sectionWidth);
            float spanLeft = MathHelper.Lerp(camera.Left, right, 0.55f);
            float spanRight = right - 40f;
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float x = MathHelper.Lerp(spanLeft, spanRight, t);
                float y = MathHelper.Lerp(corridorTop + 12f, corridorBottom - 6f, (i % 2 == 0) ? 0.3f : 0.78f);
                arr[i] = new Vector2(x, y);
            }
            return arr;
        }

        private Vector2 OffscreenRight(float y) =>
            new Vector2(Math.Min(camera.Right, sectionWidth) + OffscreenMargin, ClampLane(y));

        private Vector2 OffscreenBirthNear(Vector2 target)
        {
            float center = (camera.Left + Math.Min(camera.Right, sectionWidth)) / 2f;
            return target.X >= center
                ? OffscreenRight(target.Y)
                : new Vector2(camera.Left - OffscreenMargin, ClampLane(target.Y));
        }

        private float ClampLane(float y) =>
            y <= 0f ? (corridorTop + corridorBottom) / 2f : MathHelper.Clamp(y, corridorTop, corridorBottom);

        /// <summary>Resolves a spawn-point reference: a point id/name, or "random"/"random:Left"/"random:Right".</summary>
        private SpawnPoint ResolveSpawnPoint(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference) || points.Length == 0)
                return null;

            if (reference.StartsWith("random", StringComparison.OrdinalIgnoreCase))
            {
                SpawnPointType? filter = null;
                int colon = reference.IndexOf(':');
                if (colon >= 0 && Enum.TryParse(reference.Substring(colon + 1), true, out SpawnPointType t))
                    filter = t;
                return PickRandom(filter);
            }

            for (int i = 0; i < points.Length; i++)
            {
                if (string.Equals(points[i].Id, reference, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(points[i].Name, reference, StringComparison.OrdinalIgnoreCase))
                    return points[i];
            }
            return null;
        }

        private SpawnPoint PickRandom(SpawnPointType? filter)
        {
            int n = 0;
            for (int i = 0; i < points.Length; i++)
                if (filter == null || points[i].Type == filter.Value)
                    n++;
            if (n == 0)
                return null;

            int pick = rng.Next(n);
            for (int i = 0; i < points.Length; i++)
            {
                if (filter != null && points[i].Type != filter.Value)
                    continue;
                if (pick-- == 0)
                    return points[i];
            }
            return null;
        }
    }
}
