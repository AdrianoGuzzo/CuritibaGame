using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using Curitiba.Core.Effects;
using Curitiba.Core.Settings;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.Settings
{
    /// <summary>
    /// Saving and loading the player's preferences. The contract that matters is that a broken or
    /// missing settings file must never stop the game starting.
    /// </summary>
    public class SettingsManagerTests
    {
        [Fact]
        public void Manager_ShouldRejectAMissingStorage()
        {
            Assert.Throws<ArgumentNullException>(() => new SettingsManager<CuritibaSettings>(null));
        }

        [Fact]
        public void Manager_ShouldLoadOnConstruction()
        {
            var storage = new InMemorySettingsStorage();

            var manager = new SettingsManager<CuritibaSettings>(storage);

            Assert.Equal(1, storage.LoadCount);
            Assert.NotNull(manager.Settings);
        }

        [Fact]
        public void Manager_ShouldExposeItsStorage()
        {
            var storage = new InMemorySettingsStorage();

            var manager = new SettingsManager<CuritibaSettings>(storage);

            Assert.Same(storage, manager.Storage);
        }

        [Fact]
        public void AnEmptyStorage_ShouldYieldTheDefaults()
        {
            var manager = new SettingsManager<CuritibaSettings>(new InMemorySettingsStorage());

            Assert.False(manager.Settings.FullScreen);
            Assert.Equal(2, manager.Settings.Language);
        }

        [Fact]
        public void SavedSettings_ShouldComeBackOnReload()
        {
            var storage = new InMemorySettingsStorage();
            var manager = new SettingsManager<CuritibaSettings>(storage);
            manager.Settings.FullScreen = true;
            manager.Settings.Language = 1;

            manager.Save();
            var reloaded = new SettingsManager<CuritibaSettings>(storage);

            Assert.True(reloaded.Settings.FullScreen);
            Assert.Equal(1, reloaded.Settings.Language);
        }

        [Fact]
        public void Load_ShouldReplaceTheCurrentSettings()
        {
            var storage = new InMemorySettingsStorage();
            var manager = new SettingsManager<CuritibaSettings>(storage);
            manager.Settings.Language = 0;
            storage.Seed(new CuritibaSettings { Language = 1 });

            manager.Load();

            Assert.Equal(1, manager.Settings.Language);
        }

        [Fact]
        public void Load_ShouldRaiseItsEvent()
        {
            var storage = new InMemorySettingsStorage();
            var manager = new SettingsManager<CuritibaSettings>(storage);
            CuritibaSettings loaded = null;
            manager.SettingsLoaded += s => loaded = s;

            manager.Load();

            Assert.Same(manager.Settings, loaded);
        }

        [Fact]
        public void Save_ShouldRaiseItsEvent()
        {
            var storage = new InMemorySettingsStorage();
            var manager = new SettingsManager<CuritibaSettings>(storage);
            CuritibaSettings saved = null;
            manager.SettingsSaved += s => saved = s;

            manager.Save();

            Assert.Same(manager.Settings, saved);
        }

        // ---------------------------------------------------------------- failure paths

        [Fact]
        public void ANullFromStorage_ShouldStillYieldUsableSettings()
        {
            var storage = new InMemorySettingsStorage { ReturnsNull = true };

            var manager = new SettingsManager<CuritibaSettings>(storage);

            Assert.NotNull(manager.Settings);
            Assert.Equal(2, manager.Settings.Language);
        }

        [Fact]
        public void AStorageThatThrowsOnLoad_ShouldFallBackToDefaults()
        {
            var storage = new InMemorySettingsStorage { ThrowOnLoad = new IOException("disk is on fire") };

            var manager = new SettingsManager<CuritibaSettings>(storage);

            Assert.NotNull(manager.Settings);
            Assert.Equal(2, manager.Settings.Language);
        }

        [Fact]
        public void AStorageThatThrowsOnLoad_ShouldStillRaiseTheLoadedEvent()
        {
            var storage = new InMemorySettingsStorage();
            var manager = new SettingsManager<CuritibaSettings>(storage);
            storage.ThrowOnLoad = new IOException("nope");
            bool raised = false;
            manager.SettingsLoaded += _ => raised = true;

            manager.Load();

            Assert.True(raised);
        }

        [Fact]
        public void AStorageThatThrowsOnSave_ShouldNotBringTheGameDown()
        {
            var storage = new InMemorySettingsStorage { ThrowOnSave = new UnauthorizedAccessException() };
            var manager = new SettingsManager<CuritibaSettings>(storage);

            manager.Save();   // must not throw
        }

        [Fact]
        public void AFailedSave_ShouldNotRaiseTheSavedEvent()
        {
            // So the event is a genuine success signal rather than just "we tried".
            var storage = new InMemorySettingsStorage { ThrowOnSave = new IOException() };
            var manager = new SettingsManager<CuritibaSettings>(storage);
            bool raised = false;
            manager.SettingsSaved += _ => raised = true;

            manager.Save();

            Assert.False(raised);
        }

        [Fact]
        public void CorruptStoredData_ShouldFallBackToDefaults()
        {
            var storage = new InMemorySettingsStorage();
            storage.Corrupt();

            var manager = new SettingsManager<CuritibaSettings>(storage);

            Assert.NotNull(manager.Settings);
            Assert.Equal(2, manager.Settings.Language);
        }
    }

    /// <summary>The JSON-on-disk storage the desktop and mobile builds share.</summary>
    /// <remarks>
    /// These touch the real filesystem and the shared <c>SpecialFolderPath</c> static, so they run
    /// serialised — see <see cref="GlobalStateCollection"/>.
    /// </remarks>
    [Collection(GlobalStateCollection.Name)]
    public class BaseSettingsStorageTests
    {
        /// <summary>A storage rooted in a temp directory instead of the user's profile.</summary>
        private sealed class ScratchStorage : BaseSettingsStorage
        {
            private readonly string directory;

            public ScratchStorage(string directory) => this.directory = directory;

            protected override string SettingsFilePath => Path.Combine(directory, SettingsFileName);
        }

        [Fact]
        public void AMissingFile_ShouldYieldDefaults_WithoutWriting()
        {
            using var dir = new TempDir();
            var storage = new ScratchStorage(dir.Path);

            CuritibaSettings settings = storage.LoadSettings<CuritibaSettings>();

            Assert.NotNull(settings);
            Assert.Equal(2, settings.Language);
            Assert.False(storage.SettingsExist());
            Assert.Empty(Directory.GetFiles(dir.Path));
        }

        [Fact]
        public void Saving_ShouldRoundTrip()
        {
            using var dir = new TempDir();
            var storage = new ScratchStorage(dir.Path);

            storage.SaveSettings(new CuritibaSettings { FullScreen = true, Language = 1 });
            CuritibaSettings reloaded = storage.LoadSettings<CuritibaSettings>();

            Assert.True(storage.SettingsExist());
            Assert.True(reloaded.FullScreen);
            Assert.Equal(1, reloaded.Language);
        }

        [Fact]
        public void Saving_ShouldCreateTheDirectory()
        {
            using var dir = new TempDir();
            var storage = new ScratchStorage(Path.Combine(dir.Path, "nested"));

            storage.SaveSettings(new CuritibaSettings());

            Assert.True(storage.SettingsExist());
        }

        [Fact]
        public void Saving_ShouldWriteReadableJson()
        {
            using var dir = new TempDir();
            var storage = new ScratchStorage(dir.Path);

            storage.SaveSettings(new CuritibaSettings { Language = 1 });

            string json = File.ReadAllText(Path.Combine(dir.Path, "settings.json"));
            Assert.Contains("\"Language\"", json);
            Assert.Contains("\n", json);
        }

        [Fact]
        public void CorruptJson_ShouldSurfaceAsAnException_ForTheManagerToHandle()
        {
            // BaseSettingsStorage does not swallow anything; SettingsManager is where the
            // fallback to defaults lives.
            using var dir = new TempDir();
            var storage = new ScratchStorage(dir.Path);
            dir.Write("settings.json", "{ not json");

            Assert.Throws<JsonException>(() => storage.LoadSettings<CuritibaSettings>());
        }

        [Fact]
        public void CorruptJson_ShouldBeSurvivedByTheManager()
        {
            using var dir = new TempDir();
            var storage = new ScratchStorage(dir.Path);
            dir.Write("settings.json", "{ not json");

            var manager = new SettingsManager<CuritibaSettings>(storage);

            Assert.Equal(2, manager.Settings.Language);
        }

        [Fact]
        public void CorruptJson_ShouldBeLeftOnDisk_UntilSomethingOverwritesIt()
        {
            using var dir = new TempDir();
            var storage = new ScratchStorage(dir.Path);
            string path = dir.Write("settings.json", "{ not json");
            _ = new SettingsManager<CuritibaSettings>(storage);

            Assert.Equal("{ not json", File.ReadAllText(path));
        }

        [Fact]
        public void ALiteralNullFile_ShouldYieldDefaults()
        {
            using var dir = new TempDir();
            var storage = new ScratchStorage(dir.Path);
            dir.Write("settings.json", "null");

            CuritibaSettings settings = storage.LoadSettings<CuritibaSettings>();

            Assert.NotNull(settings);
            Assert.Equal(2, settings.Language);
        }

        [Fact]
        public void TheFileName_ShouldBeSwappable()
        {
            // The leaderboard design swaps this per stage, so it has to take effect immediately.
            using var dir = new TempDir();
            var storage = new ScratchStorage(dir.Path) { SettingsFileName = "01.json" };

            storage.SaveSettings(new CuritibaSettings { Language = 1 });

            Assert.True(File.Exists(Path.Combine(dir.Path, "01.json")));
        }

        [Fact]
        public void DefaultFileName_ShouldBeSettingsJson()
        {
            using var dir = new TempDir();

            Assert.Equal("settings.json", new ScratchStorage(dir.Path).SettingsFileName);
        }
    }

    /// <summary>The settings object itself, which the UI data-binds to.</summary>
    public class CuritibaSettingsTests
    {
        [Fact]
        public void Settings_ShouldDefaultToWindowedPortugueseAndNoEffect()
        {
            var settings = new CuritibaSettings();

            Assert.False(settings.FullScreen);
            Assert.Equal(2, settings.Language);
            Assert.Equal(default(ParticleEffectType), settings.ParticleEffect);
        }

        [Theory]
        [InlineData(nameof(CuritibaSettings.FullScreen))]
        [InlineData(nameof(CuritibaSettings.Language))]
        [InlineData(nameof(CuritibaSettings.ParticleEffect))]
        public void ChangingAProperty_ShouldRaiseItsNotification(string property)
        {
            var settings = new CuritibaSettings();
            string raised = null;
            settings.PropertyChanged += (_, e) => raised = e.PropertyName;

            Change(settings, property);

            Assert.Equal(property, raised);
        }

        [Theory]
        [InlineData(nameof(CuritibaSettings.FullScreen))]
        [InlineData(nameof(CuritibaSettings.Language))]
        [InlineData(nameof(CuritibaSettings.ParticleEffect))]
        public void SettingAPropertyToItsCurrentValue_ShouldRaiseNothing(string property)
        {
            var settings = new CuritibaSettings();
            Change(settings, property);
            bool raised = false;
            settings.PropertyChanged += (_, _) => raised = true;

            Set(settings, property, Get(settings, property));

            Assert.False(raised);
        }

        [Fact]
        public void Settings_ShouldSerialiseWithPascalCaseKeys()
        {
            // The storage uses the default serializer options, so the on-disk keys are PascalCase.
            string json = JsonSerializer.Serialize(new CuritibaSettings { Language = 1 });

            Assert.Contains("\"Language\":1", json);
        }

        private static void Change(CuritibaSettings settings, string property)
        {
            switch (property)
            {
                case nameof(CuritibaSettings.FullScreen): settings.FullScreen = true; break;
                case nameof(CuritibaSettings.Language): settings.Language = 42; break;
                default: settings.ParticleEffect = (ParticleEffectType)1; break;
            }
        }

        private static object Get(CuritibaSettings settings, string property) => property switch
        {
            nameof(CuritibaSettings.FullScreen) => settings.FullScreen,
            nameof(CuritibaSettings.Language) => settings.Language,
            _ => settings.ParticleEffect,
        };

        private static void Set(CuritibaSettings settings, string property, object value)
        {
            switch (property)
            {
                case nameof(CuritibaSettings.FullScreen): settings.FullScreen = (bool)value; break;
                case nameof(CuritibaSettings.Language): settings.Language = (int)value; break;
                default: settings.ParticleEffect = (ParticleEffectType)value; break;
            }
        }
    }
}
