using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>How badly a <see cref="StageIssue"/> breaks the stage.</summary>
    internal enum StageSeverity
    {
        /// <summary>Legal, but worth knowing about (e.g. a section with no waves is walked straight through).</summary>
        Info,

        /// <summary>The stage loads but a value is silently coerced or ignored, so it will not play as authored.</summary>
        Warning,

        /// <summary>The stage cannot be played: building or running the arena throws, hangs or traps the player.</summary>
        Error,
    }

    /// <summary>One problem found in a <see cref="StageDefinition"/>, located by a JSON-ish path.</summary>
    internal readonly struct StageIssue
    {
        /// <summary>Where the problem is, e.g. <c>sections[1].waves[0].spawns[2].spawnPoint</c>.</summary>
        public readonly string Path;

        public readonly string Message;
        public readonly StageSeverity Severity;

        public StageIssue(string path, string message, StageSeverity severity)
        {
            Path = path;
            Message = message;
            Severity = severity;
        }

        public override string ToString() => $"[{Severity}] {Path}: {Message}";
    }

    /// <summary>
    /// Checks a <see cref="StageDefinition"/> for data the arena cannot cope with.
    /// </summary>
    /// <remarks>
    /// Every rule here exists because of a concrete failure in the current code, not because a value
    /// looked odd: <see cref="CapaoRasoArena"/> dereferences <c>Corridor</c>/<c>Backdrop</c> without a
    /// guard, indexes <c>sections[0]</c> unconditionally, resolves personalities with a case-sensitive
    /// parse that falls back silently, and a wave that can never spawn an enemy locks the camera for
    /// good. The validator is deliberately <em>advisory</em> — nothing in the game calls it, so loading
    /// behaviour is unchanged; it exists so tests (and later the editor) can catch a broken stage up front.
    /// </remarks>
    internal static class StageValidator
    {
        /// <summary>
        /// The virtual viewport width the camera shows at once. Matches the 800 that
        /// <see cref="StageSection.Mode"/> compares against, so the two agree on what "fits on
        /// screen" means.
        /// </summary>
        private const float ViewportWidth = 800f;

        private static readonly string[] EntryModes = { "Fixed", "Carry", "Fall", "Door" };
        private static readonly string[] FacingNames = { "Left", "Right" };
        private static readonly string[] SpawnPointTypes = { "Left", "Right", "Custom" };

        /// <summary>Validates the whole stage. An empty result means nothing was found.</summary>
        public static IReadOnlyList<StageIssue> Validate(StageDefinition def)
        {
            var issues = new List<StageIssue>();

            if (def == null)
            {
                issues.Add(new StageIssue("stage", "Stage definition is null.", StageSeverity.Error));
                return issues;
            }

            ValidateCorridor(def, issues);
            ValidateBackdrop(def, issues);
            ValidatePersonalities(def, issues);
            ValidateSections(def, issues);

            return issues;
        }

        /// <summary>True when nothing at <see cref="StageSeverity.Error"/> was found.</summary>
        public static bool IsPlayable(StageDefinition def)
        {
            IReadOnlyList<StageIssue> issues = Validate(def);
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == StageSeverity.Error)
                    return false;
            }
            return true;
        }

        /// <summary>Renders the issues as one multi-line string, for an assertion message or a log.</summary>
        public static string Describe(IReadOnlyList<StageIssue> issues)
        {
            if (issues == null || issues.Count == 0)
                return "(no issues)";

            var sb = new StringBuilder();
            for (int i = 0; i < issues.Count; i++)
                sb.AppendLine(issues[i].ToString());
            return sb.ToString();
        }

        private static void ValidateCorridor(StageDefinition def, List<StageIssue> issues)
        {
            // CapaoRasoArena reads def.Corridor.* straight away: a null here is a NullReferenceException.
            if (def.Corridor == null)
            {
                issues.Add(new StageIssue("corridor",
                    "Corridor is missing; the arena dereferences it while building.", StageSeverity.Error));
                return;
            }

            // The corridor is used as a [Top, Bottom] clamp range; inverted, every lane collapses.
            if (def.Corridor.Top >= def.Corridor.Bottom)
            {
                issues.Add(new StageIssue("corridor",
                    Format("Top ({0}) must be above Bottom ({1}); the walkable band is inverted or empty.",
                        def.Corridor.Top, def.Corridor.Bottom),
                    StageSeverity.Error));
            }

            // A negative step would raise the asphalt above the sidewalk.
            if (def.Corridor.CurbHeight < 0f)
            {
                issues.Add(new StageIssue("corridor.curbHeight",
                    Format("Curb height must not be negative (was {0}).", def.Corridor.CurbHeight),
                    StageSeverity.Error));
            }
        }

        private static void ValidateBackdrop(StageDefinition def, List<StageIssue> issues)
        {
            // Same as the corridor: read unguarded in the arena constructor.
            if (def.Backdrop == null)
            {
                issues.Add(new StageIssue("backdrop",
                    "Backdrop is missing; the arena dereferences it while building.", StageSeverity.Error));
                return;
            }

            if (def.Backdrop.BuildingsHeight < 0)
            {
                issues.Add(new StageIssue("backdrop.buildingsHeight",
                    Format("Buildings height must not be negative (was {0}).", def.Backdrop.BuildingsHeight),
                    StageSeverity.Warning));
            }
        }

        private static void ValidatePersonalities(StageDefinition def, List<StageIssue> issues)
        {
            if (def.Personalities == null)
                return;

            foreach (KeyValuePair<string, PersonalityDef> pair in def.Personalities)
            {
                // ResolveProfileByName parses the key with a case-SENSITIVE Enum.TryParse, so a key
                // that does not match an EnemyPersonality member is never looked up at all.
                if (!IsKnownPersonality(pair.Key))
                {
                    issues.Add(new StageIssue(PersonalityPath(pair.Key),
                        "Unknown personality key; it will never be applied (expected "
                        + string.Join(", ", Enum.GetNames(typeof(EnemyPersonality))) + ", matched case-sensitively).",
                        StageSeverity.Warning));
                }

                if (pair.Value == null)
                {
                    issues.Add(new StageIssue(PersonalityPath(pair.Key),
                        "Personality entry is null; the built-in defaults are used instead.",
                        StageSeverity.Warning));
                    continue;
                }

                if (pair.Value.AttackCooldown < 0f)
                {
                    issues.Add(new StageIssue(PersonalityPath(pair.Key) + ".attackCooldown",
                        Format("Attack cooldown must not be negative (was {0}).", pair.Value.AttackCooldown),
                        StageSeverity.Warning));
                }
            }
        }

        private static void ValidateSections(StageDefinition def, List<StageIssue> issues)
        {
            // The arena calls LoadSection(0) unconditionally: no sections means IndexOutOfRangeException.
            if (def.Sections == null || def.Sections.Count == 0)
            {
                issues.Add(new StageIssue("sections",
                    "Stage has no sections; building the arena indexes sections[0].", StageSeverity.Error));
                return;
            }

            for (int i = 0; i < def.Sections.Count; i++)
            {
                SectionDef section = def.Sections[i];
                string path = "sections[" + i + "]";

                if (section == null)
                {
                    issues.Add(new StageIssue(path, "Section is null.", StageSeverity.Error));
                    continue;
                }

                ValidateSection(section, path, issues);
            }
        }

        private static void ValidateSection(SectionDef section, string path, List<StageIssue> issues)
        {
            // The section width drives the camera bounds and the exit trigger.
            if (section.FallbackWidth <= 0f)
            {
                issues.Add(new StageIssue(path + ".fallbackWidth",
                    Format("Fallback width must be positive (was {0}); a section with no width cannot be traversed.",
                        section.FallbackWidth),
                    StageSeverity.Error));
            }

            // StageSection coerces RepeatX to 1, so this is authoring noise rather than a crash.
            if (section.RepeatX < 1)
            {
                issues.Add(new StageIssue(path + ".repeatX",
                    Format("RepeatX must be at least 1 (was {0}); it is coerced to 1.", section.RepeatX),
                    StageSeverity.Warning));
            }

            // An inverted driveway span silently disables the ramp.
            if (section.DrivewayLeft != 0f && section.DrivewayRight != 0f
                && section.DrivewayLeft >= section.DrivewayRight)
            {
                issues.Add(new StageIssue(path + ".driveway",
                    Format("DrivewayLeft ({0}) must be less than DrivewayRight ({1}); the ramp span is empty.",
                        section.DrivewayLeft, section.DrivewayRight),
                    StageSeverity.Warning));
            }

            ValidateEntry(section.Entry, path + ".entry", issues);
            ValidateSpawnPoints(section, path, issues);
            ValidateWaves(section, path, issues);
        }

        private static void ValidateEntry(EntryDef entry, string path, List<StageIssue> issues)
        {
            if (entry == null)
                return;

            if (Array.IndexOf(EntryModes, entry.Mode) < 0)
            {
                issues.Add(new StageIssue(path + ".mode",
                    "Unknown entry mode " + Quote(entry.Mode) + "; it falls back to Fixed (expected "
                    + string.Join(", ", EntryModes) + ").",
                    StageSeverity.Warning));
            }

            if (Array.IndexOf(FacingNames, entry.Facing) < 0)
            {
                issues.Add(new StageIssue(path + ".facing",
                    "Unknown facing " + Quote(entry.Facing) + " (expected " + string.Join(", ", FacingNames) + ").",
                    StageSeverity.Warning));
            }
        }

        private static void ValidateSpawnPoints(SectionDef section, string path, List<StageIssue> issues)
        {
            if (section.SpawnPoints == null)
                return;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < section.SpawnPoints.Count; i++)
            {
                SpawnPointDef point = section.SpawnPoints[i];
                string pointPath = path + ".spawnPoints[" + i + "]";

                if (point == null)
                {
                    issues.Add(new StageIssue(pointPath, "Spawn point is null.", StageSeverity.Error));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(point.Id) && string.IsNullOrWhiteSpace(point.Name))
                {
                    issues.Add(new StageIssue(pointPath,
                        "Spawn point has neither an id nor a name, so no spawn can reference it.",
                        StageSeverity.Warning));
                }

                // ParseSpawnType is case-insensitive but still falls back to Custom, which changes
                // where the enemy is born (an authored world point instead of a screen edge).
                if (!IsKnownSpawnPointType(point.Type))
                {
                    issues.Add(new StageIssue(pointPath + ".type",
                        "Unknown spawn point type " + Quote(point.Type) + "; it falls back to Custom (expected "
                        + string.Join(", ", SpawnPointTypes) + ").",
                        StageSeverity.Warning));
                }

                if (!string.IsNullOrWhiteSpace(point.Id) && !seen.Add(point.Id))
                {
                    issues.Add(new StageIssue(pointPath + ".id",
                        "Duplicate spawn point id " + Quote(point.Id) + "; references resolve to the first match only.",
                        StageSeverity.Warning));
                }
            }
        }

        private static void ValidateWaves(SectionDef section, string path, List<StageIssue> issues)
        {
            if (section.Waves == null || section.Waves.Count == 0)
            {
                // Legal: the arena goes straight to ExitReady and Sofia walks through.
                issues.Add(new StageIssue(path + ".waves",
                    "Section has no waves; the player walks straight through it.", StageSeverity.Info));
                return;
            }

            for (int i = 0; i < section.Waves.Count; i++)
            {
                WaveDef wave = section.Waves[i];
                string wavePath = path + ".waves[" + i + "]";

                if (wave == null)
                {
                    issues.Add(new StageIssue(wavePath, "Wave is null.", StageSeverity.Error));
                    continue;
                }

                ValidateWave(section, wave, wavePath, issues);
            }
        }

        private static void ValidateWave(SectionDef section, WaveDef wave, string wavePath, List<StageIssue> issues)
        {
            bool hasAuthoredSpawns = wave.Spawns != null && wave.Spawns.Count > 0;

            // A wave clears only once every enemy is defeated. A wave that spawns nobody never clears,
            // so the camera lock is never released and the stage is stuck for good.
            if (!hasAuthoredSpawns && wave.EnemyCount <= 0)
            {
                issues.Add(new StageIssue(wavePath,
                    Format("Wave spawns nothing (enemyCount {0} and no spawns[]); it never clears, so the camera lock is never released.",
                        wave.EnemyCount),
                    StageSeverity.Error));
            }

            if (wave.EnemyCount < 0)
            {
                issues.Add(new StageIssue(wavePath + ".enemyCount",
                    Format("Enemy count must not be negative (was {0}).", wave.EnemyCount),
                    StageSeverity.Warning));
            }

            if (wave.Delay < 0f)
            {
                issues.Add(new StageIssue(wavePath + ".delay",
                    Format("Delay must not be negative (was {0}).", wave.Delay),
                    StageSeverity.Warning));
            }

            if (wave.HitsToKnockdown < 0)
            {
                issues.Add(new StageIssue(wavePath + ".hitsToKnockdown",
                    Format("HitsToKnockdown must not be negative (was {0}).", wave.HitsToKnockdown),
                    StageSeverity.Warning));
            }

            if (wave.LockCameraX < 0f)
            {
                issues.Add(new StageIssue(wavePath + ".lockCameraX",
                    Format("Lock position must not be negative (was {0}).", wave.LockCameraX),
                    StageSeverity.Warning));
            }
            else
            {
                ValidateLockIsReachable(section, wave, wavePath, issues);
            }

            if (hasAuthoredSpawns)
                ValidateSpawns(section, wave, wavePath, issues);
        }

        /// <summary>
        /// A wave arms only once the camera actually reaches its lock, and the camera can never
        /// scroll past <c>width - viewport</c>. So a lock beyond that point leaves the wave unarmed
        /// for ever — no enemies spawn, the area never clears, and the stage is stuck.
        /// </summary>
        /// <remarks>
        /// The width a section really gets comes from its background texture when it has one, and
        /// from <see cref="SectionDef.FallbackWidth"/> when the art is missing or fails to load
        /// (note the fallback path does not apply <see cref="SectionDef.RepeatX"/>). So a section
        /// with art whose fallback width cannot support its own locks is playable today but would
        /// deadlock the moment that texture went missing — a warning rather than an error.
        /// </remarks>
        private static void ValidateLockIsReachable(SectionDef section, WaveDef wave, string wavePath,
                                                    List<StageIssue> issues)
        {
            float reachableOnFallback = Math.Max(0f, section.FallbackWidth - ViewportWidth);
            if (wave.LockCameraX <= reachableOnFallback)
                return;

            bool hasArt = section.BackgroundAsset != null;
            issues.Add(new StageIssue(wavePath + ".lockCameraX",
                Format("Lock position ({0}) is past the furthest the camera can scroll on the fallback width ({1} - {2} viewport = {3}), so the wave would never arm{4}.",
                    wave.LockCameraX, section.FallbackWidth, ViewportWidth, reachableOnFallback,
                    hasArt ? " if the background art were missing" : string.Empty),
                hasArt ? StageSeverity.Warning : StageSeverity.Error));
        }

        private static void ValidateSpawns(SectionDef section, WaveDef wave, string wavePath, List<StageIssue> issues)
        {
            for (int i = 0; i < wave.Spawns.Count; i++)
            {
                SpawnDef spawn = wave.Spawns[i];
                string spawnPath = wavePath + ".spawns[" + i + "]";

                if (spawn == null)
                {
                    issues.Add(new StageIssue(spawnPath, "Spawn is null.", StageSeverity.Error));
                    continue;
                }

                // ParsePersonality uses a case-SENSITIVE Enum.TryParse and silently falls back to
                // Balanced, so a typo here quietly changes the enemy's behaviour.
                if (!IsKnownPersonality(spawn.Personality))
                {
                    issues.Add(new StageIssue(spawnPath + ".personality",
                        "Unknown personality " + Quote(spawn.Personality)
                        + "; it falls back to Balanced (matched case-sensitively).",
                        StageSeverity.Warning));
                }

                if (string.IsNullOrWhiteSpace(spawn.SpawnPoint))
                    continue;

                if (spawn.SpawnPoint.StartsWith("random", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateRandomReference(section, spawn.SpawnPoint, spawnPath, issues);
                    continue;
                }

                // An unresolved reference makes the enemy fall back to the nearest screen edge
                // instead of the authored door/alley.
                if (!ReferencesKnownPoint(section, spawn.SpawnPoint))
                {
                    issues.Add(new StageIssue(spawnPath + ".spawnPoint",
                        "Spawn point " + Quote(spawn.SpawnPoint)
                        + " is not declared in this section; the enemy enters from the nearest screen edge instead.",
                        StageSeverity.Error));
                }
            }
        }

        private static void ValidateRandomReference(SectionDef section, string reference, string spawnPath,
                                                    List<StageIssue> issues)
        {
            int colon = reference.IndexOf(':');
            if (colon < 0)
            {
                if (section.SpawnPoints == null || section.SpawnPoints.Count == 0)
                {
                    issues.Add(new StageIssue(spawnPath + ".spawnPoint",
                        Quote("random") + " was used but the section declares no spawn points.",
                        StageSeverity.Error));
                }
                return;
            }

            string filter = reference.Substring(colon + 1);
            if (!IsKnownSpawnPointType(filter))
            {
                issues.Add(new StageIssue(spawnPath + ".spawnPoint",
                    "Unknown random filter " + Quote(filter) + " (expected " + string.Join(", ", SpawnPointTypes) + ").",
                    StageSeverity.Warning));
                return;
            }

            if (!HasPointOfType(section, filter))
            {
                issues.Add(new StageIssue(spawnPath + ".spawnPoint",
                    "No spawn point of type " + Quote(filter)
                    + " in this section; the enemy enters from the nearest screen edge instead.",
                    StageSeverity.Error));
            }
        }

        private static bool HasPointOfType(SectionDef section, string type)
        {
            if (section.SpawnPoints == null)
                return false;

            for (int i = 0; i < section.SpawnPoints.Count; i++)
            {
                SpawnPointDef point = section.SpawnPoints[i];
                if (point != null && string.Equals(point.Type, type, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool ReferencesKnownPoint(SectionDef section, string reference)
        {
            if (section.SpawnPoints == null)
                return false;

            for (int i = 0; i < section.SpawnPoints.Count; i++)
            {
                SpawnPointDef point = section.SpawnPoints[i];
                if (point == null)
                    continue;

                if (string.Equals(point.Id, reference, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(point.Name, reference, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsKnownSpawnPointType(string type) =>
            Array.FindIndex(SpawnPointTypes, t => string.Equals(t, type, StringComparison.OrdinalIgnoreCase)) >= 0;

        /// <summary>Matches the arena's case-sensitive <c>Enum.TryParse</c> over <see cref="EnemyPersonality"/>.</summary>
        private static bool IsKnownPersonality(string name) =>
            !string.IsNullOrEmpty(name) && Enum.TryParse(name, out EnemyPersonality _);

        private static string PersonalityPath(string key) => "personalities[" + Quote(key) + "]";

        private static string Quote(string value) => "\"" + value + "\"";

        private static string Format(string format, params object[] args) =>
            string.Format(CultureInfo.InvariantCulture, format, args);
    }
}
