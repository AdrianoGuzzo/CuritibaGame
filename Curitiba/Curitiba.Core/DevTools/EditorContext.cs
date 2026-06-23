using System;
using Curitiba.Core.BeatEmUp;
using Curitiba.ScreenManagers;

namespace Curitiba.Core.DevTools
{
    /// <summary>
    /// Everything the in-game editor needs from the active beat 'em up screen: the stage data it
    /// edits, the live arena (for camera/world transform and section navigation), the screen
    /// manager (for the world→screen transform), and callbacks to apply (rebuild the arena from
    /// the edited data) and the path to persist to.
    /// </summary>
    internal sealed class EditorContext
    {
        public StageDefinition Definition;
        public CapaoRasoArena Arena;
        public ScreenManager ScreenManager;

        /// <summary>Recreates the arena from <see cref="Definition"/> (owned by the screen).</summary>
        public Action Rebuild;

        /// <summary>Replaces the screen's stage definition (e.g. on reload from disk) and rebuilds.</summary>
        public Action<StageDefinition> Replace;

        /// <summary>Absolute path the editor saves the stage JSON to (null if not resolvable).</summary>
        public string SavePath;
    }
}
