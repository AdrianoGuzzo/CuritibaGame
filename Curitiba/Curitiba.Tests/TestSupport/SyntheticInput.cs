using Curitiba.Core.Inputs;
using Microsoft.Xna.Framework.Input;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// Builds an <see cref="InputState"/> with keys held or freshly pressed, without touching a real
    /// keyboard or gamepad.
    /// </summary>
    /// <remarks>
    /// <see cref="InputState"/> exposes its current/last device state arrays as public fields and
    /// derives everything else from them, so a test can write the exact pair of frames it wants.
    /// A "fresh press" is a key down this frame that was up last frame — which is what every
    /// <c>IsNewKeyPress</c> check in the game looks for.
    /// </remarks>
    internal static class SyntheticInput
    {
        /// <summary>Keys held down on this frame and the previous one (no fresh press).</summary>
        public static InputState Held(params Keys[] keys)
        {
            var input = new InputState();
            SetFrame(input, previous: keys, current: keys);
            return input;
        }

        /// <summary>Keys that went down this frame, so they read as a fresh press.</summary>
        public static InputState Pressed(params Keys[] keys)
        {
            var input = new InputState();
            SetFrame(input, previous: new Keys[0], current: keys);
            return input;
        }

        /// <summary>A fresh press of <paramref name="pressed"/> while <paramref name="held"/> stays down.</summary>
        public static InputState PressedWhileHolding(Keys pressed, params Keys[] held)
        {
            var current = new Keys[held.Length + 1];
            held.CopyTo(current, 0);
            current[held.Length] = pressed;

            var input = new InputState();
            SetFrame(input, previous: held, current: current);
            return input;
        }

        /// <summary>Nothing pressed at all.</summary>
        public static InputState None() => Held();

        private static void SetFrame(InputState input, Keys[] previous, Keys[] current)
        {
            for (int i = 0; i < InputState.MaxInputs; i++)
            {
                input.LastKeyboardStates[i] = new KeyboardState(previous);
                input.CurrentKeyboardStates[i] = new KeyboardState(current);
            }
        }
    }
}
