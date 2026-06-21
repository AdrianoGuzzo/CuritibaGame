using Microsoft.Xna.Framework.Graphics;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>How a section advances: a scrolling corridor or a single fixed screen.</summary>
    internal enum SectionMode { Scroll, Frame }

    /// <summary>
    /// One section of a beat 'em up stage. A stage is a sequence of sections chained
    /// together; the section's behaviour is chosen automatically from the width of its
    /// background relative to the viewport (800):
    /// <list type="bullet">
    /// <item>width &gt; viewport =&gt; <see cref="SectionMode.Scroll"/>: the camera follows the
    /// player horizontally to the end of the image (the classic corridor).</item>
    /// <item>width &lt;= viewport =&gt; <see cref="SectionMode.Frame"/>: no camera scroll; the
    /// player fights within the single screen and walks to the right edge to advance.</item>
    /// </list>
    /// <see cref="Background"/>, <see cref="Width"/> and <see cref="Mode"/> are resolved at
    /// load time (they need the loaded texture); the rest is config.
    /// </summary>
    internal class StageSection
    {
        /// <summary>Single world-space background image (the full scene). Null =&gt; colour/parallax fallback.</summary>
        public string BackgroundAsset { get; }

        /// <summary>Width used when the art is missing, so the mode can still be decided (graceful degradation).</summary>
        public float FallbackWidth { get; }

        /// <summary>Combat waves. For a <see cref="SectionMode.Frame"/> section this is normally a single wave.</summary>
        public SpawnArea[] Waves { get; }

        /// <summary>
        /// Draw the tiled sky/buildings parallax behind this section (decorative). Does NOT affect the
        /// section width — the width always comes from the background image (or <see cref="FallbackWidth"/>).
        /// </summary>
        public bool ParallaxBackdrop { get; }

        /// <summary>
        /// Depth line (world Y) of the curb/step that splits this section's floor into a raised
        /// sidewalk (<c>Y &lt; CurbY</c>) and the asphalt (<c>Y &gt;= CurbY</c>). 0 disables the step.
        /// </summary>
        public float CurbY { get; }

        /// <summary>
        /// World-X span of the gate driveway, where the curb is a ramp and both floors connect
        /// (free passage, no jump). Empty when <c>DrivewayLeft &gt;= DrivewayRight</c>.
        /// </summary>
        public float DrivewayLeft { get; }
        public float DrivewayRight { get; }

        /// <summary>Loaded background texture (null when missing => colour fallback). Set in LoadSection.</summary>
        public Texture2D Background { get; set; }

        /// <summary>Scaled background width (to screen height) or <see cref="FallbackWidth"/>. Set in LoadSection.</summary>
        public float Width { get; set; }

        public SectionMode Mode => Width <= 800f ? SectionMode.Frame : SectionMode.Scroll;

        public StageSection(string backgroundAsset, float fallbackWidth, SpawnArea[] waves, bool parallaxBackdrop = false,
            float curbY = 0f, float drivewayLeft = 0f, float drivewayRight = 0f)
        {
            BackgroundAsset = backgroundAsset;
            FallbackWidth = fallbackWidth;
            Waves = waves;
            ParallaxBackdrop = parallaxBackdrop;
            CurbY = curbY;
            DrivewayLeft = drivewayLeft;
            DrivewayRight = drivewayRight;
        }
    }
}
