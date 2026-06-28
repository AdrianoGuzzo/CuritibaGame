using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// One-way import of a Tiled <c>.tmj</c> map into a <see cref="StageDefinition"/> section.
    /// The game JSON stays canonical; Tiled is only an authoring shortcut for layout. Importing
    /// regenerates the target section's background, walkable zone, waves/spawns and set pieces,
    /// while preserving everything else (tuning, personalities, other sections, ids).
    ///
    /// Conventions (object/image layer names; coordinates are map pixels = virtual world units):
    /// <list type="bullet">
    /// <item>image layer "background" → section background; "sky"/"buildings" → backdrop.</item>
    /// <item>object layer "spawns": point per enemy; props <c>wave</c>(int), <c>personality</c>,
    /// <c>template</c>, optional <c>lockCameraX</c>/<c>hitsToKnockdown</c> (per wave).</item>
    /// <item>object layer "setpieces": point per prop; props <c>asset</c>, <c>depthSortByY</c>, <c>solid</c>.</item>
    /// <item>object layer "walkzone": rect "corridor" (top/bottom), "curb" (y), "driveway" (x..x+w).</item>
    /// <item>map props: <c>repeatX</c>, <c>parallaxBackdrop</c>, <c>fallbackWidth</c>, <c>curbHeight</c>.</item>
    /// </list>
    /// </summary>
    internal static class TiledImporter
    {
        public static bool TryImportFile(string tmjPath, StageDefinition target, int sectionIndex, out string error)
        {
            try
            {
                if (!File.Exists(tmjPath))
                {
                    error = "arquivo não encontrado: " + tmjPath;
                    return false;
                }

                string json = File.ReadAllText(tmjPath);
                TmjMap map = JsonSerializer.Deserialize<TmjMap>(json, StageLoader.JsonOptions);
                if (map == null)
                {
                    error = "JSON do Tiled inválido";
                    return false;
                }

                Import(map, target, sectionIndex);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void Import(TmjMap map, StageDefinition def, int sectionIndex)
        {
            if (sectionIndex < 0)
                sectionIndex = 0;
            while (def.Sections.Count <= sectionIndex)
                def.Sections.Add(new SectionDef());

            SectionDef section = def.Sections[sectionIndex];

            section.RepeatX = GetInt(map.Properties, "repeatX", section.RepeatX);
            section.ParallaxBackdrop = GetBool(map.Properties, "parallaxBackdrop", section.ParallaxBackdrop);
            section.FallbackWidth = GetFloat(map.Properties, "fallbackWidth", section.FallbackWidth);
            def.Corridor.CurbHeight = GetFloat(map.Properties, "curbHeight", def.Corridor.CurbHeight);

            ImportImageLayers(map, def, section);
            ImportWalkzone(map, def, section);
            ImportSpawns(map, section);
            ImportSetPieces(map, section);
        }

        private static void ImportImageLayers(TmjMap map, StageDefinition def, SectionDef section)
        {
            string firstOther = null;
            foreach (TmjLayer layer in map.Layers)
            {
                if (!string.Equals(layer.Type, "imagelayer", StringComparison.OrdinalIgnoreCase) || layer.Image == null)
                    continue;

                string asset = StripAsset(layer.Image);
                string name = layer.Name ?? "";
                if (name.Equals("sky", StringComparison.OrdinalIgnoreCase))
                    def.Backdrop.SkyAsset = asset;
                else if (name.Equals("buildings", StringComparison.OrdinalIgnoreCase))
                    def.Backdrop.BuildingsAsset = asset;
                else if (name.Equals("background", StringComparison.OrdinalIgnoreCase))
                    section.BackgroundAsset = asset;
                else
                    firstOther ??= asset;
            }

            if (firstOther != null && (section.BackgroundAsset == null ||
                section.BackgroundAsset.Length == 0))
            {
                section.BackgroundAsset = firstOther;
            }
        }

        private static void ImportWalkzone(TmjMap map, StageDefinition def, SectionDef section)
        {
            TmjLayer wz = FindLayer(map, "walkzone");
            if (wz == null)
                return;

            TmjObject corridor = FindObject(wz, "corridor");
            if (corridor != null)
            {
                def.Corridor.Top = corridor.Y;
                def.Corridor.Bottom = corridor.Y + corridor.Height;
            }

            TmjObject curb = FindObject(wz, "curb");
            if (curb != null)
                section.CurbY = curb.Y;

            TmjObject driveway = FindObject(wz, "driveway");
            if (driveway != null)
            {
                section.DrivewayLeft = driveway.X;
                section.DrivewayRight = driveway.X + driveway.Width;
            }
        }

        private static void ImportSpawns(TmjMap map, SectionDef section)
        {
            TmjLayer layer = FindLayer(map, "spawns");
            if (layer == null || layer.Objects == null || layer.Objects.Count == 0)
                return;

            var byWave = new SortedDictionary<int, WaveDef>();
            foreach (TmjObject obj in layer.Objects)
            {
                int waveIdx = GetInt(obj.Properties, "wave", 0);
                if (!byWave.TryGetValue(waveIdx, out WaveDef wave))
                {
                    wave = new WaveDef { LockCameraX = 0f, EnemyCount = 0, HitsToKnockdown = 3 };
                    byWave[waveIdx] = wave;
                }

                TmjProperty lockProp = Find(obj.Properties, "lockCameraX");
                if (lockProp != null) wave.LockCameraX = lockProp.AsFloat();
                TmjProperty hitsProp = Find(obj.Properties, "hitsToKnockdown");
                if (hitsProp != null) wave.HitsToKnockdown = hitsProp.AsInt();

                wave.Spawns.Add(new SpawnDef
                {
                    Template = GetString(obj.Properties, "template", "piaLoco"),
                    Personality = GetString(obj.Properties, "personality", "Balanced"),
                    X = obj.X,
                    Y = obj.Y,
                });
            }

            section.Waves = byWave.Values.ToList();
        }

        private static void ImportSetPieces(TmjMap map, SectionDef section)
        {
            TmjLayer layer = FindLayer(map, "setpieces");
            if (layer == null || layer.Objects == null)
                return;

            section.SetPieces = layer.Objects.Select(o => new SetPieceDef
            {
                Asset = GetString(o.Properties, "asset", ""),
                X = o.X,
                Y = o.Y,
                DepthSortByY = GetBool(o.Properties, "depthSortByY", true),
                Solid = GetBool(o.Properties, "solid", false),
            }).ToList();
        }

        private static TmjLayer FindLayer(TmjMap map, string name) =>
            map.Layers?.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));

        private static TmjObject FindObject(TmjLayer layer, string name) =>
            layer.Objects?.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));

        private static TmjProperty Find(List<TmjProperty> props, string name) =>
            props?.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        private static string GetString(List<TmjProperty> props, string name, string fallback)
        {
            TmjProperty p = Find(props, name);
            return p != null ? (p.AsString() ?? fallback) : fallback;
        }

        private static int GetInt(List<TmjProperty> props, string name, int fallback)
        {
            TmjProperty p = Find(props, name);
            return p != null ? p.AsInt() : fallback;
        }

        private static float GetFloat(List<TmjProperty> props, string name, float fallback)
        {
            TmjProperty p = Find(props, name);
            return p != null ? p.AsFloat() : fallback;
        }

        private static bool GetBool(List<TmjProperty> props, string name, bool fallback)
        {
            TmjProperty p = Find(props, name);
            return p != null ? p.AsBool() : fallback;
        }

        /// <summary>
        /// Converts a Tiled image path into a MonoGame content asset name: forward slashes, no
        /// extension, no leading <c>../</c> or <c>./</c>, and trimmed to after a <c>Content/</c>
        /// segment if present (e.g. "../Content/Backgrounds/Stage1/Gate.png" → "Backgrounds/Stage1/Gate").
        /// </summary>
        private static string StripAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            string s = path.Replace('\\', '/');

            int ci = s.IndexOf("Content/", StringComparison.OrdinalIgnoreCase);
            if (ci >= 0)
                s = s.Substring(ci + "Content/".Length);

            while (s.StartsWith("../", StringComparison.Ordinal)) s = s.Substring(3);
            while (s.StartsWith("./", StringComparison.Ordinal)) s = s.Substring(2);

            int dot = s.LastIndexOf('.');
            if (dot > 0)
                s = s.Substring(0, dot);

            return s;
        }
    }
}
