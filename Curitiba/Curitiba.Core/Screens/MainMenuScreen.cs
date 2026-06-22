using Curitiba.Core.Inputs;
using Curitiba.Core.Localization;
using Curitiba.Core.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Curitiba.Screens
{
    /// <summary>
    /// The main menu screen is the first thing displayed when the game starts up.
    /// </summary>
    internal class MainMenuScreen : MenuScreen
    {
        private ContentManager content;
        private MenuBackground menuBackground = new MenuBackground();
        private SettingsManager<CuritibaSettings> settingsManager;
        private MenuEntry playMenuEntry;
        private MenuEntry settingsMenuEntry;
        private MenuEntry aboutMenuEntry;
        private MenuEntry exitMenuEntry;

        // Cinematic intro played when "Play" is selected: a slow zoom-in, the
        // camera descending and the screen darkening, then the game begins.
        private const float CinematicDuration = 2.5f;
        private bool cinematicActive;
        private float cinematicTime;
        private PlayerIndex playerIndex;

        // Use the decorative "Chlorinar" font for the game title only.
        protected override SpriteFont TitleFont => ScreenManager.TitleFont;

        /// <summary>
        /// Constructor fills in the menu contents.
        /// </summary>
        public MainMenuScreen()
            : base(Resources.MainMenu)
        {
            // Upper-left menu with light colors (reads against the sky/garden).
            TopLeftAligned = true;
            MenuEntryColor = Color.White;
            MenuEntrySelectedColor = Color.Yellow;

            // Create our menu entries.
            playMenuEntry = new MenuEntry(Resources.Play);
            settingsMenuEntry = new MenuEntry(Resources.Settings);
            aboutMenuEntry = new MenuEntry(Resources.About);
            exitMenuEntry = new MenuEntry(Resources.Exit);

            // Hook up menu event handlers.
            playMenuEntry.Selected += PlayMenuEntrySelected;
            settingsMenuEntry.Selected += SettingsMenuEntrySelected;
            aboutMenuEntry.Selected += AboutMenuEntrySelected;
            exitMenuEntry.Selected += OnCancel;

            // Add entries to the menu.
            MenuEntries.Add(playMenuEntry);
            MenuEntries.Add(settingsMenuEntry);
            MenuEntries.Add(aboutMenuEntry);
            MenuEntries.Add(exitMenuEntry);
        }

        private void SetLanguageText()
        {
            aboutMenuEntry.Text = Resources.About;
            playMenuEntry.Text = Resources.Play;
            settingsMenuEntry.Text = Resources.Settings;
            exitMenuEntry.Text = Resources.Exit;

            Title = "Curitiba"; // TODO uncomment this if you want it to use Resources.MainMenu; instead
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content for the game.
        /// </summary>
        public override void LoadContent()
        {
            base.LoadContent();

            if (content == null)
                content = new ContentManager(ScreenManager.Game.Services, "Content");

            settingsManager ??= ScreenManager.Game.Services.GetService<SettingsManager<CuritibaSettings>>();
            settingsManager.Settings.PropertyChanged += (s, e) =>
            {
                SetLanguageText();
            };

            SetLanguageText();

            menuBackground.Load(content, ScreenManager);
        }

        /// <summary>
        /// Unload graphics content used by the game.
        /// </summary>
        public override void UnloadContent()
        {
            content.Unload();
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        /// <param name="otherScreenHasFocus">If another screen has focus</param>
        /// <param name="coveredByOtherScreen">If currently covered by another screen</param>
        public override void Update(GameTime gameTime,
            bool otherScreenHasFocus,
            bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            menuBackground.Update(gameTime);

            if (cinematicActive)
            {
                cinematicTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (cinematicTime >= CinematicDuration)
                {
                    cinematicActive = false;
                    LoadingScreen.Load(ScreenManager, true, playerIndex, new BeatEmUpScreen());
                }
            }
        }

        /// <summary>
        /// Responds to user input.
        /// </summary>
        public override void HandleInput(GameTime gameTime, InputState inputState)
        {
            // Lock out menu navigation while the cinematic intro plays.
            if (cinematicActive)
                return;

            base.HandleInput(gameTime, inputState);
        }

        /// <summary>
        /// Draws the main menu screen.
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            SpriteBatch spriteBatch = ScreenManager.SpriteBatch;

            if (cinematicActive)
            {
                // Eased progress for a smooth, gentle ramp.
                float progress = MathHelper.Clamp(cinematicTime / CinematicDuration, 0f, 1f);
                float eased = progress * progress;

                // Slow zoom-in centred on the screen, with the camera descending
                // (content slides up). Build the transform around the centre.
                const float cx = 400f; // BaseScreenSize.X / 2
                const float cy = 240f; // BaseScreenSize.Y / 2
                float zoom = MathHelper.Lerp(1.0f, 1.3f, eased);
                float panY = MathHelper.Lerp(0.0f, -60.0f, eased);

                Matrix cinematic = Matrix.CreateTranslation(-cx, -cy, 0f)
                                 * Matrix.CreateScale(zoom)
                                 * Matrix.CreateTranslation(cx, cy + panY, 0f);

                menuBackground.Draw(spriteBatch, ScreenManager, cinematic, 1f);

                // Darken the whole screen very slowly down to black.
                ScreenManager.FadeBackBufferToBlack(eased);

                // Menu entries / title are intentionally hidden during the intro.
                return;
            }

            menuBackground.Draw(spriteBatch, ScreenManager, Matrix.Identity, 1f);

            base.Draw(gameTime);
        }

        /// <summary>
        /// Event handler for when the Play menu entry is selected.
        /// Starts the cinematic intro that transitions into the beat 'em up.
        /// </summary>
        void PlayMenuEntrySelected(object sender, PlayerIndexEventArgs e)
        {
            playerIndex = e.PlayerIndex;
            cinematicActive = true;
            cinematicTime = 0f;
        }

        /// <summary>
        /// Event handler for when the Options menu entry is selected.
        /// </summary>
        void SettingsMenuEntrySelected(object sender, PlayerIndexEventArgs e)
        {
            ScreenManager.AddScreen(new SettingsScreen(), e.PlayerIndex);
        }

        /// <summary>
        /// Event handler for when the Options menu entry is selected.
        /// </summary>
        void AboutMenuEntrySelected(object sender, PlayerIndexEventArgs e)
        {
            ScreenManager.AddScreen(new AboutScreen(), e.PlayerIndex);
        }

        /// <summary>
        /// When the user cancels the main menu, ask if they want to exit the sample.
        /// </summary>
        protected override void OnCancel(PlayerIndex playerIndex)
        {
            string message = Resources.ExitQuestion;

            MessageBoxScreen confirmExitMessageBox = new MessageBoxScreen(message);

            confirmExitMessageBox.Accepted += ConfirmExitMessageBoxAccepted;

            ScreenManager.AddScreen(confirmExitMessageBox, playerIndex);
        }


        /// <summary>
        /// Event handler for when the user selects ok on the "are you sure
        /// you want to exit" message box.
        /// </summary>
        void ConfirmExitMessageBoxAccepted(object sender, PlayerIndexEventArgs e)
        {
            ScreenManager.Game.Exit();
        }
    }
}
