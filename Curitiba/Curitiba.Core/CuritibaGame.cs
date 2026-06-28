using Curitiba.Core.DevTools;
using Curitiba.Core.Effects;
using Curitiba.Core.Localization;
using Curitiba.Core.Settings;
using Curitiba.ScreenManagers;
using Curitiba.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Curitiba.Core
{
    /// <summary>
    /// The main class for the game, responsible for managing game components, settings, 
    /// and platform-specific configurations.
    /// </summary>
    /// <remarks>
    /// This class is the entry point for the game and handles initialization, content loading,
    /// and screen management.
    /// </remarks>}
    public class CuritibaGame : Game
    {
        private GraphicsDeviceManager graphicsDeviceManager;

        private ScreenManager screenManager;

        private SettingsManager<CuritibaSettings> settingsManager;

        private Texture2D particleTexture;

        private ParticleManager particleManager;

        /// <summary>
        /// Indicates if the game is running on a mobile platform.
        /// </summary>
        public readonly static bool IsMobile = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

        /// <summary>
        /// Indicates if the game is running on a desktop platform.
        /// </summary>
        public readonly static bool IsDesktop = OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() || OperatingSystem.IsWindows();

        /// <summary>
        /// Initializes a new instance of the game. Configures platform-specific settings,
        /// initializes services like the settings manager, and sets up the
        /// screen manager for screen transitions.
        /// </summary>
        public CuritibaGame()
        {
            graphicsDeviceManager = new GraphicsDeviceManager(this);

            Services.AddService(typeof(GraphicsDeviceManager), graphicsDeviceManager);

            ISettingsStorage storage;
            if (IsMobile)
            {
                storage = new MobileSettingsStorage();
                graphicsDeviceManager.IsFullScreen = true;
                IsMouseVisible = false;
            }
            else if (IsDesktop)
            {
                storage = new DesktopSettingsStorage();
                IsMouseVisible = true;
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            graphicsDeviceManager.HardwareModeSwitch = false;

            settingsManager = new SettingsManager<CuritibaSettings>(storage);
            Services.AddService(typeof(SettingsManager<CuritibaSettings>), settingsManager);

            if (IsDesktop && settingsManager.Settings.FullScreen)
            {
                graphicsDeviceManager.PreferredBackBufferWidth = 1920;
                graphicsDeviceManager.PreferredBackBufferHeight = 1080;
                graphicsDeviceManager.IsFullScreen = true;
            }

            Content.RootDirectory = "Content";

            graphicsDeviceManager.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;

            screenManager = new ScreenManager(this);
            Components.Add(screenManager);

#if CURITIBA_DEVTOOLS
            if (IsDesktop)
            {
                var devEditor = new ImGuiDevEditor(this);
                Components.Add(devEditor);
                Services.AddService(typeof(IDevEditor), (IDevEditor)devEditor);
            }
            else
            {
                Services.AddService(typeof(IDevEditor), new NullDevEditor());
            }
#else
            Services.AddService(typeof(IDevEditor), new NullDevEditor());
#endif
        }

        /// <summary>
        /// Initializes the game, including setting up localization and adding the 
        /// initial screens to the ScreenManager.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();

            List<CultureInfo> cultures = LocalizationManager.GetSupportedCultures();
            var languages = new List<CultureInfo>();
            for (int i = 0; i < cultures.Count; i++)
            {
                languages.Add(cultures[i]);
            }
            var selectedLanguage = languages[settingsManager.Settings.Language].Name;
            LocalizationManager.SetCulture(selectedLanguage);

            screenManager.AddScreen(new BackgroundScreen(), null);
            screenManager.AddScreen(new MainMenuScreen(), null);
        }

        /// <summary>
        /// Loads game content, such as textures and particle systems.
        /// </summary>
        protected override void LoadContent()
        {
            base.LoadContent();

            particleTexture = Content.Load<Texture2D>("Sprites/blank");
            particleManager = new ParticleManager(particleTexture, new Vector2(400, 200));

            Services.AddService(typeof(ParticleManager), particleManager);
        }
    }
}