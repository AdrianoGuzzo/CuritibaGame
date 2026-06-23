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
        // Resources for drawing.
        private GraphicsDeviceManager graphicsDeviceManager;

        // Manages the game's screen transitions and screens.
        private ScreenManager screenManager;

        // Manages game settings, such as preferences and configurations.
        private SettingsManager<CuritibaSettings> settingsManager;

        // Texture for rendering particles.
        private Texture2D particleTexture;

        // Manages particle effects in the game.
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

            // Share GraphicsDeviceManager as a service.
            Services.AddService(typeof(GraphicsDeviceManager), graphicsDeviceManager);

            // Determine the appropriate settings storage based on the platform.
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

            // Use borderless fullscreen (no monitor mode switch) instead of an
            // exclusive hardware mode change.
            graphicsDeviceManager.HardwareModeSwitch = false;

            // Initialize settings and leaderboard managers.
            settingsManager = new SettingsManager<CuritibaSettings>(storage);
            Services.AddService(typeof(SettingsManager<CuritibaSettings>), settingsManager);

            // On desktop, restore the saved fullscreen preference at boot, running
            // fullscreen in Full HD (1920x1080). MonoGame applies these properties
            // when the graphics device is created, so no ApplyChanges() is needed here.
            if (IsDesktop && settingsManager.Settings.FullScreen)
            {
                graphicsDeviceManager.PreferredBackBufferWidth = 1920;
                graphicsDeviceManager.PreferredBackBufferHeight = 1080;
                graphicsDeviceManager.IsFullScreen = true;
            }

            Content.RootDirectory = "Content";

            // Configure screen orientations.
            graphicsDeviceManager.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;

            // Initialize the screen manager.
            screenManager = new ScreenManager(this);
            Components.Add(screenManager);
        }

        /// <summary>
        /// Initializes the game, including setting up localization and adding the 
        /// initial screens to the ScreenManager.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();

            // Load supported languages and set the default language.
            List<CultureInfo> cultures = LocalizationManager.GetSupportedCultures();
            var languages = new List<CultureInfo>();
            for (int i = 0; i < cultures.Count; i++)
            {
                languages.Add(cultures[i]);
            }
            var selectedLanguage = languages[settingsManager.Settings.Language].Name;
            LocalizationManager.SetCulture(selectedLanguage);

            // Add background and main menu screens.
            screenManager.AddScreen(new BackgroundScreen(), null);
            screenManager.AddScreen(new MainMenuScreen(), null);
        }

        /// <summary>
        /// Loads game content, such as textures and particle systems.
        /// </summary>
        protected override void LoadContent()
        {
            base.LoadContent();

            // Load a texture for particles and initialize the particle manager.
            particleTexture = Content.Load<Texture2D>("Sprites/blank");
            particleManager = new ParticleManager(particleTexture, new Vector2(400, 200));

            // Share the particle manager as a service.
            Services.AddService(typeof(ParticleManager), particleManager);
        }
    }
}