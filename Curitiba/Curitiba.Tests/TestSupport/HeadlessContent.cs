using System;
using Microsoft.Xna.Framework.Content;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// A <see cref="ContentManager"/> that can never load anything, for tests that need a fighter or
    /// an arena but no graphics.
    /// </summary>
    /// <remarks>
    /// The game already treats missing art as a normal condition — <c>FighterAnimator</c>,
    /// <c>Fighter.LoadPortrait</c> and <c>CapaoRasoArena.TryLoadTexture</c> all catch
    /// <see cref="ContentLoadException"/> and fall back to placeholders — so pointing a content
    /// manager at a directory that does not exist puts every fighter on that same fallback path.
    /// <see cref="ContentManager"/> opens the asset stream before it ever asks for a graphics device,
    /// which is what keeps this working with no GPU, no window and no compiled content.
    /// </remarks>
    internal static class HeadlessContent
    {
        /// <summary>Root that is guaranteed not to exist, so every asset lookup misses.</summary>
        private const string MissingRoot = "__no_content_in_tests__";

        /// <summary>Creates a content manager whose every <c>Load</c> throws <see cref="ContentLoadException"/>.</summary>
        public static ContentManager Create() => new ContentManager(new EmptyServiceProvider(), MissingRoot);

        /// <summary>
        /// A service provider with no services at all. The content manager only resolves
        /// <c>IGraphicsDeviceService</c> once an asset stream has been opened successfully, which
        /// never happens here.
        /// </summary>
        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public object GetService(Type serviceType) => null;
        }
    }
}
