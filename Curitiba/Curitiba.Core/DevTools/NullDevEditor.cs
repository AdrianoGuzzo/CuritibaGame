namespace Curitiba.Core.DevTools
{
    /// <summary>No-op editor used on platforms/builds without the ImGui dev tools.</summary>
    internal sealed class NullDevEditor : IDevEditor
    {
        public bool IsOpen => false;

        public bool WantsCaptureInput => false;

        public void SetContext(EditorContext context) { }
    }
}
