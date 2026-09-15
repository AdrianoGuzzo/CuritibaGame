using System.IO;
using System.Text.Json;
using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.Data
{
    /// <summary>
    /// Reading and writing stage JSON. The loader's contract is that it never throws — a missing or
    /// broken file has to degrade, because it is read at runtime and on every hot-reload.
    /// </summary>
    public class StageLoaderTests
    {
        [Fact]
        public void TryLoadFile_ShouldReadAValidStage()
        {
            bool ok = StageLoader.TryLoadFile(Fixtures.Path_("valid-stage.json"), out StageDefinition def);

            Assert.True(ok);
            Assert.NotNull(def);
            Assert.Equal("valid-stage", def.Id);
            Assert.Equal(2, def.Sections.Count);
        }

        [Fact]
        public void TryLoadFile_ShouldFailQuietly_OnMalformedJson()
        {
            bool ok = StageLoader.TryLoadFile(Fixtures.Path_("invalid-json.json"), out StageDefinition def);

            Assert.False(ok);
            Assert.Null(def);
        }

        [Fact]
        public void TryLoadFile_ShouldFailQuietly_WhenTheFileIsMissing()
        {
            bool ok = StageLoader.TryLoadFile(Fixtures.Path_("no-such-stage.json"), out StageDefinition def);

            Assert.False(ok);
            Assert.Null(def);
        }

        [Fact]
        public void TryLoadFile_ShouldFailQuietly_OnALiteralNullDocument()
        {
            using var dir = new TempDir();
            string path = dir.Write("null.json", "null");

            bool ok = StageLoader.TryLoadFile(path, out StageDefinition def);

            Assert.False(ok);
            Assert.Null(def);
        }

        [Fact]
        public void TryLoadFile_ShouldFailQuietly_OnADirectoryPath()
        {
            using var dir = new TempDir();

            bool ok = StageLoader.TryLoadFile(dir.Path, out StageDefinition def);

            Assert.False(ok);
            Assert.Null(def);
        }

        [Fact]
        public void TrySaveFile_ShouldRoundTripAStage()
        {
            using var dir = new TempDir();
            StageDefinition original = StageDefinition.CapaoRasoDefault();
            string path = dir.File("stage.json");

            Assert.True(StageLoader.TrySaveFile(path, original));
            Assert.True(StageLoader.TryLoadFile(path, out StageDefinition reloaded));

            Assert.Equal(original.Id, reloaded.Id);
            Assert.Equal(original.Sections.Count, reloaded.Sections.Count);
            Assert.Equal(original.Corridor.Top, reloaded.Corridor.Top);
            Assert.Equal(original.Corridor.Bottom, reloaded.Corridor.Bottom);
            Assert.Equal(original.Personalities.Count, reloaded.Personalities.Count);
            Assert.Equal(original.Tuning.Sofia.MaxHealth, reloaded.Tuning.Sofia.MaxHealth);
            Assert.Equal(original.Tuning.Sofia.ComboChain.Count, reloaded.Tuning.Sofia.ComboChain.Count);
        }

        [Fact]
        public void TrySaveFile_ShouldCreateMissingDirectories()
        {
            using var dir = new TempDir();
            string path = Path.Combine(dir.Path, "nested", "deeper", "stage.json");

            Assert.True(StageLoader.TrySaveFile(path, StageDefinition.CapaoRasoDefault()));
            Assert.True(File.Exists(path));
        }

        [Fact]
        public void TrySaveFile_ShouldFailQuietly_OnAnUnusablePath()
        {
            bool ok = StageLoader.TrySaveFile("   ", StageDefinition.CapaoRasoDefault());

            Assert.False(ok);
        }

        [Fact]
        public void LoadOrDefault_ShouldReadTheRealStage_FromTitleContent()
        {
            // The stage JSON is copied next to the test assembly exactly as the game lays it out,
            // so this exercises the real TitleContainer path rather than a bare file read.
            StageDefinition def = StageLoader.LoadOrDefault(StageLoader.CapaoRasoTitlePath,
                                                            StageDefinition.CapaoRasoDefault);

            Assert.Equal("capao-raso", def.Id);
            Assert.NotEmpty(def.Sections);
        }

        [Fact]
        public void LoadOrDefault_ShouldFallBack_WhenTheStageIsMissing()
        {
            StageDefinition fallback = StageDefinition.CapaoRasoDefault();

            StageDefinition def = StageLoader.LoadOrDefault("Content/Data/Stages/not-here.json", () => fallback);

            Assert.Same(fallback, def);
        }

        [Fact]
        public void JsonOptions_ShouldAcceptCamelCaseCommentsAndTrailingCommas()
        {
            const string json = @"{
                // a comment the editor may leave behind
                ""schemaVersion"": 2,
                ""corridor"": { ""top"": 10, ""bottom"": 20, },
            }";

            var def = JsonSerializer.Deserialize<StageDefinition>(json, StageLoader.JsonOptions);

            Assert.Equal(2, def.SchemaVersion);
            Assert.Equal(10f, def.Corridor.Top);
        }

        [Fact]
        public void JsonOptions_ShouldBeCaseInsensitive()
        {
            const string json = @"{ ""SCHEMAVERSION"": 7 }";

            var def = JsonSerializer.Deserialize<StageDefinition>(json, StageLoader.JsonOptions);

            Assert.Equal(7, def.SchemaVersion);
        }

        [Fact]
        public void SavedJson_ShouldUseCamelCaseKeys()
        {
            using var dir = new TempDir();
            string path = dir.File("stage.json");
            StageLoader.TrySaveFile(path, StageDefinition.CapaoRasoDefault());

            string json = File.ReadAllText(path);

            Assert.Contains("\"schemaVersion\"", json);
            Assert.Contains("\"lockCameraX\"", json);
            Assert.DoesNotContain("\"SchemaVersion\"", json);
        }

        [Fact]
        public void PersonalityKeys_ShouldSurviveARoundTripUnchanged()
        {
            // Dictionary keys are data, not property names, so the camelCase policy must not touch
            // them — the arena looks them up with a case-sensitive enum parse.
            using var dir = new TempDir();
            string path = dir.File("stage.json");
            StageLoader.TrySaveFile(path, StageDefinition.CapaoRasoDefault());

            string json = File.ReadAllText(path);
            StageLoader.TryLoadFile(path, out StageDefinition reloaded);

            Assert.Contains("\"Aggressive\"", json);
            Assert.True(reloaded.Personalities.ContainsKey("Aggressive"));
            Assert.True(reloaded.Personalities.ContainsKey("Balanced"));
        }
    }
}
