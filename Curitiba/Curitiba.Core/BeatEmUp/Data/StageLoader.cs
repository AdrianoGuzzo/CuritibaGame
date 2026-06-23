using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Loads and saves <see cref="StageDefinition"/> JSON. At runtime stages are read through
    /// <see cref="Microsoft.Xna.Framework.TitleContainer"/> (works on every platform); in a
    /// desktop dev build the source folder can also be located so the editor/hot-reload can
    /// write back to the versioned file. Invalid or missing JSON never throws to the caller —
    /// the supplied fallback (normally <see cref="StageDefinition.CapaoRasoDefault"/>) is used.
    /// </summary>
    internal static class StageLoader
    {
        /// <summary>Path under the content root (relative to the title) of the Capão Raso stage.</summary>
        public const string CapaoRasoTitlePath = "Content/Data/Stages/capao-raso.json";

        /// <summary>Folder (relative to the content root) holding the stage data files.</summary>
        public const string StagesFolder = "Data/Stages";

        public const string CapaoRasoFileName = "capao-raso.json";

        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>Reads a stage from the title (packaged) content, falling back on any error.</summary>
        public static StageDefinition LoadOrDefault(string titlePath, Func<StageDefinition> fallback)
        {
            try
            {
                using Stream stream = TitleContainer.OpenStream(titlePath);
                StageDefinition def = JsonSerializer.Deserialize<StageDefinition>(stream, JsonOptions);
                return def ?? fallback();
            }
            catch (Exception)
            {
                return fallback();
            }
        }

        /// <summary>Reads a stage from an absolute file path (used by hot-reload). Returns false on any error.</summary>
        public static bool TryLoadFile(string fullPath, out StageDefinition def)
        {
            try
            {
                string json = File.ReadAllText(fullPath);
                def = JsonSerializer.Deserialize<StageDefinition>(json, JsonOptions);
                return def != null;
            }
            catch (Exception)
            {
                def = null;
                return false;
            }
        }

        /// <summary>Serializes a stage to an absolute file path. Returns false on any error.</summary>
        public static bool TrySaveFile(string fullPath, StageDefinition def)
        {
            try
            {
                string dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(fullPath, JsonSerializer.Serialize(def, JsonOptions));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Best-effort location of the stages folder to watch/write in a desktop dev session.
        /// Prefers the project source tree (so edits are versioned and survive a rebuild); falls
        /// back to the copied output folder under the running directory. Returns null if neither
        /// exists (e.g. on mobile, where hot-reload is disabled anyway).
        /// </summary>
        public static string ResolveWritableStagesDir()
        {
            // 1) Walk up from the running directory looking for the source tree.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "Curitiba.Core", "Content", StagesFolder);
                if (Directory.Exists(candidate))
                    return candidate;
            }

            // 2) Fall back to the copied output (bin/.../Content/Data/Stages).
            string output = Path.Combine(AppContext.BaseDirectory, "Content", StagesFolder);
            return Directory.Exists(output) ? output : null;
        }
    }
}
