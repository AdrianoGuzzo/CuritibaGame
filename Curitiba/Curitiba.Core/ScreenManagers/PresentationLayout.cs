using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Curitiba.ScreenManagers
{
    /// <summary>
    /// The result of fitting the fixed virtual resolution (<see cref="ScreenManager.BaseScreenSize"/>)
    /// into an arbitrary backbuffer: the uniform scale, the letterboxed/pillarboxed area the game is
    /// drawn into, and the two matrices derived from them.
    /// </summary>
    /// <remarks>
    /// This is a pure value computed by <see cref="ScreenManager.ComputePresentation"/>; it holds no
    /// device state, so the resolution-independence maths can be reasoned about (and tested) without
    /// a <see cref="GraphicsDevice"/> or a window.
    /// </remarks>
    public readonly struct PresentationLayout
    {
        /// <summary>Uniform virtual-to-backbuffer scale factor.</summary>
        public readonly float ScalingFactor;

        /// <summary>Scale-only matrix passed to every <c>SpriteBatch.Begin</c>.</summary>
        public readonly Matrix GlobalTransformation;

        /// <summary>The centred, scaled virtual area inside the backbuffer.</summary>
        public readonly Viewport PresentationViewport;

        /// <summary>
        /// Inverse of scale+offset: maps a backbuffer (mouse/touch) point back into virtual
        /// coordinates. Handed to <see cref="Curitiba.Core.Inputs.InputState"/>.
        /// </summary>
        public readonly Matrix InputTransformation;

        public PresentationLayout(float scalingFactor, Matrix globalTransformation,
                                  Viewport presentationViewport, Matrix inputTransformation)
        {
            ScalingFactor = scalingFactor;
            GlobalTransformation = globalTransformation;
            PresentationViewport = presentationViewport;
            InputTransformation = inputTransformation;
        }
    }
}
