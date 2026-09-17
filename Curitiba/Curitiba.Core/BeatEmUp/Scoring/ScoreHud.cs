using System.Globalization;
using Curitiba.Core.Localization;
using Microsoft.Xna.Framework;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// What the score HUD says: the readouts and the fade, as plain functions.
    /// </summary>
    /// <remarks>
    /// Split out of the arena's <c>DrawHud</c> for the same reason as
    /// <c>ScreenManagers.PresentationLayout</c> and <c>DevTools.StageReloadPolicy</c>: drawing has
    /// no automated test here, so every decision that can be taken away from it is. What is left in
    /// the arena is measuring the strings and placing them.
    /// </remarks>
    internal static class ScoreHud
    {
        /// <summary>
        /// The shortest combo worth putting on screen. A single landed blow is not a combo, and
        /// flashing a "1" on every jab would be noise rather than feedback.
        /// </summary>
        public const int MinimumCombo = 2;

        /// <summary>
        /// How long before the combo window closes the readout starts fading out, in seconds. Short
        /// on purpose: it reads as "you are about to lose it", not as a slow dissolve.
        /// </summary>
        public const float FadeSeconds = 0.4f;

        /// <summary>The banked score, behind its localised label.</summary>
        public static string ScoreText(long total) =>
            Resources.Score + total.ToString(CultureInfo.InvariantCulture);

        /// <summary>Whether the combo readout belongs on screen at all.</summary>
        public static bool ShowCombo(int combo) => combo >= MinimumCombo;

        /// <summary>
        /// The combo readout: the count, and the multiplier once it is worth mentioning. At x1
        /// the multiplier is left off — it is the value every run starts at, so showing it says
        /// nothing.
        /// </summary>
        public static string ComboText(int combo, int multiplier)
        {
            string text = Resources.Combo + " " + combo.ToString(CultureInfo.InvariantCulture);
            return multiplier > 1
                ? text + "  x" + multiplier.ToString(CultureInfo.InvariantCulture)
                : text;
        }

        /// <summary>
        /// How opaque the combo readout should be, from the seconds left on the combo window. Full
        /// until the last <see cref="FadeSeconds"/>, then down to nothing — and clamped, because an
        /// out-of-range alpha is the kind of mistake that only ever shows up on screen.
        /// </summary>
        public static float ComboOpacity(float comboTimeRemaining) =>
            MathHelper.Clamp(comboTimeRemaining / FadeSeconds, 0f, 1f);
    }
}
