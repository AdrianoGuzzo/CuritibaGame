using System.Collections.Generic;
using System.Text.Json;
using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.Data
{
    /// <summary>
    /// Importing a Tiled map into a stage section.
    /// </summary>
    /// <remarks>
    /// The import is one-way and destructive by design — it regenerates the section's background,
    /// walk zone, waves and set pieces — so what needs protecting is which parts it overwrites and
    /// which it leaves alone. <c>Import</c> takes an already-parsed map, so these tests build the
    /// map in code and never touch a file.
    /// </remarks>
    public class TiledImporterTests
    {
        private static TmjProperty Prop(string name, object value) => new TmjProperty
        {
            Name = name,
            Value = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value)),
        };

        private static TmjMap Map(params TmjLayer[] layers) => new TmjMap
        {
            Width = 50,
            Height = 30,
            TileWidth = 16,
            TileHeight = 16,
            Layers = new List<TmjLayer>(layers),
        };

        private static TmjLayer ImageLayer(string name, string image) =>
            new TmjLayer { Type = "imagelayer", Name = name, Image = image };

        private static TmjLayer ObjectLayer(string name, params TmjObject[] objects) =>
            new TmjLayer { Type = "objectgroup", Name = name, Objects = new List<TmjObject>(objects) };

        // ---------------------------------------------------------------- section targeting

        [Fact]
        public void Import_ShouldWriteIntoTheChosenSection()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            TiledImporter.Import(Map(ImageLayer("background", "../Content/Backgrounds/New.png")), def, 1);

            Assert.Equal("Backgrounds/New", def.Sections[1].BackgroundAsset);
            Assert.Equal("Backgrounds/Stage1/Gate", def.Sections[0].BackgroundAsset);
        }

        [Fact]
        public void ANegativeSectionIndex_ShouldBeTreatedAsTheFirst()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            TiledImporter.Import(Map(ImageLayer("background", "New.png")), def, -5);

            Assert.Equal("New", def.Sections[0].BackgroundAsset);
        }

        [Fact]
        public void AnIndexPastTheEnd_ShouldGrowTheStage()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            TiledImporter.Import(Map(ImageLayer("background", "New.png")), def, 4);

            Assert.Equal(5, def.Sections.Count);
            Assert.Equal("New", def.Sections[4].BackgroundAsset);
        }

        // ---------------------------------------------------------------- map properties

        [Fact]
        public void MapProperties_ShouldConfigureTheSection()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            TmjMap map = Map();
            map.Properties = new List<TmjProperty>
            {
                Prop("repeatX", 4),
                Prop("parallaxBackdrop", false),
                Prop("fallbackWidth", 2200.0),
            };

            TiledImporter.Import(map, def, 0);

            Assert.Equal(4, def.Sections[0].RepeatX);
            Assert.False(def.Sections[0].ParallaxBackdrop);
            Assert.Equal(2200f, def.Sections[0].FallbackWidth);
        }

        [Fact]
        public void CurbHeight_ShouldBeStageWide_NotPerSection()
        {
            // The curb height lives on the corridor, so importing one section changes it for all.
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            TmjMap map = Map();
            map.Properties = new List<TmjProperty> { Prop("curbHeight", 22.0) };

            TiledImporter.Import(map, def, 1);

            Assert.Equal(22f, def.Corridor.CurbHeight);
        }

        [Fact]
        public void MissingMapProperties_ShouldLeaveTheSectionAsItWas()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            int repeatX = def.Sections[1].RepeatX;

            TiledImporter.Import(Map(), def, 1);

            Assert.Equal(repeatX, def.Sections[1].RepeatX);
        }

        // ---------------------------------------------------------------- image layers

        [Fact]
        public void NamedImageLayers_ShouldFeedTheBackdropAndBackground()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            TmjMap map = Map(
                ImageLayer("sky", "../Content/Backgrounds/Stage2/Sky.png"),
                ImageLayer("buildings", "../Content/Backgrounds/Stage2/Buildings.png"),
                ImageLayer("background", "../Content/Backgrounds/Stage2/Street.png"));

            TiledImporter.Import(map, def, 0);

            Assert.Equal("Backgrounds/Stage2/Sky", def.Backdrop.SkyAsset);
            Assert.Equal("Backgrounds/Stage2/Buildings", def.Backdrop.BuildingsAsset);
            Assert.Equal("Backgrounds/Stage2/Street", def.Sections[0].BackgroundAsset);
        }

        [Fact]
        public void AnUnnamedImageLayer_ShouldBecomeTheBackground_WhenThereIsNoneYet()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            def.Sections[0].BackgroundAsset = null;

            TiledImporter.Import(Map(ImageLayer("whatever", "Art/Alley.png")), def, 0);

            Assert.Equal("Art/Alley", def.Sections[0].BackgroundAsset);
        }

        [Theory]
        [InlineData("../Content/Backgrounds/Stage1/Gate.png", "Backgrounds/Stage1/Gate")]
        [InlineData("Content/Backgrounds/Stage1/Gate.png", "Backgrounds/Stage1/Gate")]
        [InlineData("..\\Content\\Backgrounds\\Stage1\\Gate.png", "Backgrounds/Stage1/Gate")]
        [InlineData("./Sprites/Thing.png", "Sprites/Thing")]
        [InlineData("Plain.png", "Plain")]
        public void AnImagePath_ShouldBecomeAContentAssetName(string image, string expected)
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            TiledImporter.Import(Map(ImageLayer("background", image)), def, 0);

            Assert.Equal(expected, def.Sections[0].BackgroundAsset);
        }

        // ---------------------------------------------------------------- walk zone

        [Fact]
        public void TheWalkzone_ShouldDefineTheCorridorAndTheCurb()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            TmjMap map = Map(ObjectLayer("walkzone",
                new TmjObject { Name = "corridor", X = 0f, Y = 280f, Width = 1600f, Height = 180f },
                new TmjObject { Name = "curb", X = 0f, Y = 330f, Width = 1600f, Height = 4f },
                new TmjObject { Name = "driveway", X = 300f, Y = 280f, Width = 250f, Height = 180f }));

            TiledImporter.Import(map, def, 0);

            Assert.Equal(280f, def.Corridor.Top);
            Assert.Equal(460f, def.Corridor.Bottom);
            Assert.Equal(330f, def.Sections[0].CurbY);
            Assert.Equal(300f, def.Sections[0].DrivewayLeft);
            Assert.Equal(550f, def.Sections[0].DrivewayRight);
        }

        [Fact]
        public void AMissingWalkzone_ShouldLeaveTheCorridorAlone()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            TiledImporter.Import(Map(), def, 0);

            Assert.Equal(300f, def.Corridor.Top);
            Assert.Equal(448f, def.Corridor.Bottom);
        }

        // ---------------------------------------------------------------- spawns

        [Fact]
        public void SpawnObjects_ShouldReplaceTheSectionWaves()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            Assert.Equal(2, def.Sections[0].Waves.Count);

            TmjMap map = Map(ObjectLayer("spawns",
                new TmjObject { X = 500f, Y = 400f, Properties = new List<TmjProperty> { Prop("wave", 0) } }));
            TiledImporter.Import(map, def, 0);

            Assert.Single(def.Sections[0].Waves);
            Assert.Single(def.Sections[0].Waves[0].Spawns);
        }

        [Fact]
        public void SpawnObjects_ShouldBeGroupedByTheirWaveNumber()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            TmjMap map = Map(ObjectLayer("spawns",
                new TmjObject { X = 100f, Y = 400f, Properties = new List<TmjProperty> { Prop("wave", 1) } },
                new TmjObject { X = 200f, Y = 400f, Properties = new List<TmjProperty> { Prop("wave", 0) } },
                new TmjObject { X = 300f, Y = 400f, Properties = new List<TmjProperty> { Prop("wave", 1) } }));

            TiledImporter.Import(map, def, 0);

            Assert.Equal(2, def.Sections[0].Waves.Count);
            Assert.Single(def.Sections[0].Waves[0].Spawns);
            Assert.Equal(2, def.Sections[0].Waves[1].Spawns.Count);
        }

        [Fact]
        public void ImportedWaves_ShouldBeOrderedByWaveNumber()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            TmjMap map = Map(ObjectLayer("spawns",
                new TmjObject { X = 1f, Y = 400f, Properties = new List<TmjProperty> { Prop("wave", 2) } },
                new TmjObject { X = 2f, Y = 400f, Properties = new List<TmjProperty> { Prop("wave", 0) } },
                new TmjObject { X = 3f, Y = 400f, Properties = new List<TmjProperty> { Prop("wave", 1) } }));

            TiledImporter.Import(map, def, 0);

            Assert.Equal(2f, def.Sections[0].Waves[0].Spawns[0].X);
            Assert.Equal(3f, def.Sections[0].Waves[1].Spawns[0].X);
            Assert.Equal(1f, def.Sections[0].Waves[2].Spawns[0].X);
        }

        [Fact]
        public void ImportedWaves_ShouldUseAuthoredSpawnsRatherThanACount()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            TmjMap map = Map(ObjectLayer("spawns",
                new TmjObject { X = 500f, Y = 400f, Properties = new List<TmjProperty> { Prop("wave", 0) } }));

            TiledImporter.Import(map, def, 0);

            Assert.Equal(0, def.Sections[0].Waves[0].EnemyCount);
            Assert.Equal(3, def.Sections[0].Waves[0].HitsToKnockdown);
        }

        [Fact]
        public void ASpawnObject_ShouldCarryItsTemplateAndPersonality()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            TmjMap map = Map(ObjectLayer("spawns", new TmjObject
            {
                X = 500f,
                Y = 400f,
                Properties = new List<TmjProperty>
                {
                    Prop("wave", 0), Prop("template", "boss"), Prop("personality", "Aggressive"),
                },
            }));

            TiledImporter.Import(map, def, 0);

            SpawnDef spawn = def.Sections[0].Waves[0].Spawns[0];
            Assert.Equal("boss", spawn.Template);
            Assert.Equal("Aggressive", spawn.Personality);
            Assert.Equal(500f, spawn.X);
            Assert.Equal(400f, spawn.Y);
        }

        [Fact]
        public void WaveProperties_ShouldBeReadableOffAnySpawnInTheWave()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            TmjMap map = Map(ObjectLayer("spawns", new TmjObject
            {
                X = 500f,
                Y = 400f,
                Properties = new List<TmjProperty>
                {
                    Prop("wave", 0), Prop("lockCameraX", 900.0), Prop("hitsToKnockdown", 6),
                },
            }));

            TiledImporter.Import(map, def, 0);

            Assert.Equal(900f, def.Sections[0].Waves[0].LockCameraX);
            Assert.Equal(6, def.Sections[0].Waves[0].HitsToKnockdown);
        }

        [Fact]
        public void AnEmptySpawnLayer_ShouldLeaveTheWavesAlone()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            TiledImporter.Import(Map(ObjectLayer("spawns")), def, 0);

            Assert.Equal(2, def.Sections[0].Waves.Count);
        }

        // ---------------------------------------------------------------- set pieces

        [Fact]
        public void SetPieceObjects_ShouldReplaceTheSectionProps()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            TmjMap map = Map(ObjectLayer("setpieces", new TmjObject
            {
                X = 640f,
                Y = 420f,
                Properties = new List<TmjProperty> { Prop("asset", "Props/Car"), Prop("solid", true) },
            }));

            TiledImporter.Import(map, def, 0);

            Assert.Single(def.Sections[0].SetPieces);
            SetPieceDef piece = def.Sections[0].SetPieces[0];
            Assert.Equal("Props/Car", piece.Asset);
            Assert.Equal(640f, piece.X);
            Assert.Equal(420f, piece.Y);
            Assert.True(piece.Solid);
        }

        [Fact]
        public void AnImportedSetPiece_ShouldDepthSortByDefault()
        {
            // Note this is the opposite of SetPieceDef's own default: a prop placed in Tiled is
            // assumed to be something the fighters walk in front of and behind.
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            TiledImporter.Import(Map(ObjectLayer("setpieces", new TmjObject { X = 1f, Y = 2f })), def, 0);

            Assert.True(def.Sections[0].SetPieces[0].DepthSortByY);
            Assert.False(new SetPieceDef().DepthSortByY);
        }

        [Fact]
        public void AnEmptySetPieceLayer_ShouldClearTheProps()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            def.Sections[0].SetPieces.Add(new SetPieceDef { Asset = "Props/Old" });

            TiledImporter.Import(Map(ObjectLayer("setpieces")), def, 0);

            Assert.Empty(def.Sections[0].SetPieces);
        }

        // ---------------------------------------------------------------- what it leaves alone

        [Fact]
        public void Import_ShouldPreserveTuningAndPersonalities()
        {
            // The import regenerates layout, never balance — that stays hand-authored.
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            def.Tuning.Sofia.MaxHealth = 250;

            TmjMap map = Map(ImageLayer("background", "New.png"), ObjectLayer("spawns"));
            TiledImporter.Import(map, def, 0);

            Assert.Equal(250, def.Tuning.Sofia.MaxHealth);
            Assert.Equal(4, def.Personalities.Count);
        }

        [Fact]
        public void Import_ShouldPreserveSpawnPoints()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            TiledImporter.Import(Map(ImageLayer("background", "New.png")), def, 0);

            Assert.Equal(2, def.Sections[0].SpawnPoints.Count);
        }

        [Fact]
        public void ImportedStages_ShouldStillLoadAndValidate()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();
            TmjMap map = Map(
                ImageLayer("background", "../Content/Backgrounds/Stage1/Gate.png"),
                ObjectLayer("walkzone",
                    new TmjObject { Name = "corridor", X = 0f, Y = 300f, Width = 1600f, Height = 150f }),
                ObjectLayer("spawns", new TmjObject
                {
                    X = 500f, Y = 400f,
                    Properties = new List<TmjProperty> { Prop("wave", 0), Prop("personality", "Balanced") },
                }));

            TiledImporter.Import(map, def, 0);

            Assert.True(StageValidator.IsPlayable(def),
                StageValidator.Describe(StageValidator.Validate(def)));
        }

        // ---------------------------------------------------------------- file import

        [Fact]
        public void TryImportFile_ShouldReportAMissingFile()
        {
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            bool ok = TiledImporter.TryImportFile("no-such-map.tmj", def, 0, out string error);

            Assert.False(ok);
            Assert.Contains("no-such-map.tmj", error);
        }

        [Fact]
        public void TryImportFile_ShouldReportBrokenJson()
        {
            using var dir = new TempDir();
            string path = dir.Write("broken.tmj", "{ not a map");
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            bool ok = TiledImporter.TryImportFile(path, def, 0, out string error);

            Assert.False(ok);
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void TryImportFile_ShouldReadARealMap()
        {
            using var dir = new TempDir();
            string path = dir.Write("map.tmj", JsonSerializer.Serialize(
                Map(ImageLayer("background", "../Content/Backgrounds/Imported.png")), StageLoader.JsonOptions));
            StageDefinition def = StageDefinition.CapaoRasoDefault();

            bool ok = TiledImporter.TryImportFile(path, def, 0, out string error);

            Assert.True(ok, error);
            Assert.Null(error);
            Assert.Equal("Backgrounds/Imported", def.Sections[0].BackgroundAsset);
        }
    }

    /// <summary>Reading typed values out of a Tiled custom property.</summary>
    public class TmjPropertyTests
    {
        private static TmjProperty Of(string json) => new TmjProperty
        {
            Name = "p",
            Value = JsonSerializer.Deserialize<JsonElement>(json),
        };

        [Fact]
        public void ANumber_ShouldReadAsFloatAndInt()
        {
            Assert.Equal(12.5f, Of("12.5").AsFloat());
            Assert.Equal(12, Of("12").AsInt());
        }

        [Fact]
        public void ANumericString_ShouldStillParse()
        {
            // Tiled sometimes writes numbers as strings depending on the property type.
            Assert.Equal(12.5f, Of("\"12.5\"").AsFloat());
            Assert.Equal(12, Of("\"12\"").AsInt());
        }

        [Fact]
        public void AFloat_ShouldParseWithTheInvariantCulture()
        {
            // A dot is always the decimal point, whatever the machine's locale.
            Assert.Equal(0.5f, Of("\"0.5\"").AsFloat());
        }

        [Fact]
        public void ABoolean_ShouldRead()
        {
            Assert.True(Of("true").AsBool());
            Assert.False(Of("false").AsBool());
        }

        [Fact]
        public void ABooleanString_ShouldStillParse()
        {
            Assert.True(Of("\"true\"").AsBool());
        }

        [Fact]
        public void AString_ShouldRead()
        {
            Assert.Equal("Props/Car", Of("\"Props/Car\"").AsString());
        }

        [Fact]
        public void AnUnparseableValue_ShouldFallBackToZero()
        {
            Assert.Equal(0f, Of("\"not a number\"").AsFloat());
            Assert.Equal(0, Of("\"not a number\"").AsInt());
        }
    }
}
