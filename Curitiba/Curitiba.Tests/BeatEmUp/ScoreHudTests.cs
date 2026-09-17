using System.Globalization;
using System.Threading;
using Curitiba.Core.BeatEmUp;
using Curitiba.Core.Localization;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// What the score HUD says, as a set of plain functions — the readouts and the fade decided
    /// away from <c>Draw</c>, which has no test of its own.
    /// </summary>
    /// <remarks>
    /// Same split as <c>PresentationLayout</c> and <c>StageReloadPolicy</c>: the decision is
    /// testable, and the drawing left in the arena is only a positioning call.
    /// </remarks>
    public class ScoreHudTests
    {
        [Fact]
        public void ScoreText_ShouldPrefixTheLocalisedLabel()
        {
            // The label must come from the resources, never from a literal in the arena.
            string text = ScoreHud.ScoreText(1234);

            Assert.StartsWith(Resources.Score, text);
            Assert.EndsWith("1234", text);
        }

        [Fact]
        public void ScoreText_ShouldWritePlainDigits_WhateverTheCulture()
        {
            // A grouped number would need separator glyphs the sprite font may not carry, and the
            // separator itself changes with the culture.
            using var scope = new CultureScope();
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            string text = ScoreHud.ScoreText(1234567);

            Assert.EndsWith("1234567", text);
            Assert.DoesNotContain(".", text);
            Assert.DoesNotContain(",", text);
        }

        [Fact]
        public void ScoreText_ShouldHandleAScoreOfZero()
        {
            Assert.EndsWith("0", ScoreHud.ScoreText(0));
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(2, true)]
        [InlineData(30, true)]
        public void ShowCombo_ShouldHideASingleBlow(int combo, bool expected)
        {
            // One landed blow is not a combo; flashing a "1" on every jab would be noise.
            Assert.Equal(expected, ScoreHud.ShowCombo(combo));
        }

        [Fact]
        public void ComboText_ShouldShowTheCountAndTheMultiplier()
        {
            string text = ScoreHud.ComboText(12, 3);

            Assert.Contains(Resources.Combo, text);
            Assert.Contains("12", text);
            Assert.Contains("x3", text);
        }

        [Fact]
        public void ComboText_ShouldOmitTheMultiplier_WhileItIsStillOne()
        {
            // "x1" is not news — it is the value every run starts at.
            string text = ScoreHud.ComboText(3, 1);

            Assert.Contains("3", text);
            Assert.DoesNotContain("x", text);
        }

        [Fact]
        public void ComboOpacity_ShouldBeFullWhileTheWindowIsWideOpen()
        {
            Assert.Equal(1f, ScoreHud.ComboOpacity(2.0f));
        }

        [Fact]
        public void ComboOpacity_ShouldFadeAsTheWindowCloses()
        {
            float half = ScoreHud.ComboOpacity(ScoreHud.FadeSeconds / 2f);

            Assert.InRange(half, 0.45f, 0.55f);
        }

        [Theory]
        [InlineData(-5f, 0f)]
        [InlineData(0f, 0f)]
        [InlineData(100f, 1f)]
        public void ComboOpacity_ShouldStayWithinZeroToOne(float remaining, float expected)
        {
            // A negative remainder cannot happen through the ScoreSystem, but a colour built from
            // an out-of-range alpha is the kind of thing that only shows up on screen.
            Assert.Equal(expected, ScoreHud.ComboOpacity(remaining));
        }
    }
}
