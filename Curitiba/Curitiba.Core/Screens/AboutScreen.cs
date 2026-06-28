using Curitiba.Core.Localization;

namespace Curitiba.Screens
{
    /// <summary>
    /// Represents the "About" screen, providing information about the game and its technology.
    /// This screen displays credits and links to the MonoGame website.
    /// </summary>
    /// <remarks>
    /// This class extends <see cref="MenuScreen"/>, inheriting its menu management capabilities.
    /// </remarks>
    internal class AboutScreen : MenuScreen
    {
        private MenuEntry builtWithMonoGameMenuEntry;
        private MenuEntry monoGameWebsiteMenuEntry;

        /// <summary>
        /// Initializes a new instance of the <see cref="AboutScreen"/> class.
        /// </summary>
        /// <remarks>
        /// This constructor sets the screen's title and creates the menu entries.
        /// It also hooks up event handlers for menu entry selections.
        /// </remarks>
        public AboutScreen()
            : base(Resources.About)
        {
            builtWithMonoGameMenuEntry = new MenuEntry("#BuiltWithMonoGame", false);

            monoGameWebsiteMenuEntry = new MenuEntry(Resources.MonoGameSite);

            MenuEntry back = new MenuEntry(Resources.Back);

            monoGameWebsiteMenuEntry.Selected += MonoGameWebsiteMenuSelected;
            back.Selected += OnCancel;

            MenuEntries.Add(builtWithMonoGameMenuEntry);
            MenuEntries.Add(monoGameWebsiteMenuEntry);
            MenuEntries.Add(back);
        }

        /// <summary>
        /// Handles the selection event for the MonoGame website menu entry.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="PlayerIndexEventArgs"/> instance containing the event data.</param>
        private void MonoGameWebsiteMenuSelected(object sender, PlayerIndexEventArgs e)
        {
            LaunchDefaultBrowser("https://www.monogame.net/");
        }

        /// <summary>
        /// Launches the default web browser with the specified URL.
        /// </summary>
        /// <param name="url">The URL to open in the browser.</param>
        /// <remarks>
        /// This method uses <see cref="System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo)"/> to launch the browser.
        /// Note: Platform-specific adjustments might be necessary for cross-platform compatibility.
        /// </remarks>
        private static void LaunchDefaultBrowser(string url)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}