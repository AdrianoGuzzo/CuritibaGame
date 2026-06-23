namespace Curitiba.Core.DevTools
{
    /// <summary>
    /// In-game developer editor overlay (ImGui-based on desktop). Kept behind an interface so the
    /// rest of the game can talk to it without compiling ImGui on mobile/Release builds, where
    /// <see cref="NullDevEditor"/> stands in as a no-op. The desktop implementation is itself a
    /// game component, so it drives its own update/draw; the game only feeds it the current
    /// <see cref="EditorContext"/> and reads back its state.
    /// </summary>
    internal interface IDevEditor
    {
        /// <summary>True while the editor overlay is open (gameplay should freeze).</summary>
        bool IsOpen { get; }

        /// <summary>True when ImGui is consuming input (so gameplay should ignore it this frame).</summary>
        bool WantsCaptureInput { get; }

        /// <summary>Supplies the active stage/arena context, or null when no stage is being edited.</summary>
        void SetContext(EditorContext context);
    }
}
