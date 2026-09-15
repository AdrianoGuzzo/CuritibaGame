using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Curitiba.Core.Localization;
using Curitiba.Tests.TestSupport;
using Xunit;

namespace Curitiba.Tests.Localization
{
    /// <summary>
    /// Language discovery and switching.
    /// </summary>
    /// <remarks>
    /// These run serialised and inside a <see cref="CultureScope"/>, because
    /// <c>LocalizationManager.SetCulture</c> writes the calling thread's culture and never puts it
    /// back — on a pooled test thread that would otherwise leak into whatever runs next.
    /// </remarks>
    [Collection(GlobalStateCollection.Name)]
    public class LocalizationTests
    {
        [Fact]
        public void SupportedCultures_ShouldNotBeEmpty()
        {
            List<CultureInfo> cultures = LocalizationManager.GetSupportedCultures();

            Assert.NotEmpty(cultures);
        }

        [Fact]
        public void SupportedCultures_ShouldEndWithTheNeutralCulture()
        {
            // English is the neutral resource, appended last — the menu shows it as the final option.
            List<CultureInfo> cultures = LocalizationManager.GetSupportedCultures();

            Assert.Equal(CultureInfo.InvariantCulture, cultures[cultures.Count - 1]);
        }

        [Theory]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("pt-BR")]
        public void EveryTranslatedCulture_ShouldBeDiscovered(string name)
        {
            List<CultureInfo> cultures = LocalizationManager.GetSupportedCultures();

            Assert.Contains(cultures, c => c.Name == name);
        }

        [Fact]
        public void SupportedCultures_ShouldBeListedInNameOrder()
        {
            // The order is load-bearing: the saved language is an index into this list.
            List<CultureInfo> cultures = LocalizationManager.GetSupportedCultures();
            List<string> named = cultures.Where(c => !Equals(c, CultureInfo.InvariantCulture))
                                         .Select(c => c.Name).ToList();

            Assert.Equal(named.OrderBy(n => n, System.StringComparer.Ordinal).ToList(), named);
        }

        [Fact]
        public void TheDefaultLanguageIndex_ShouldStillResolve()
        {
            // CuritibaSettings.Language defaults to 2, and CuritibaGame indexes this list with it
            // unguarded — so losing a satellite assembly would crash the game on startup.
            List<CultureInfo> cultures = LocalizationManager.GetSupportedCultures();

            Assert.True(cultures.Count > 2, "index 2 must exist for the default settings to load");
            Assert.Equal("pt-BR", cultures[2].Name);
        }

        [Fact]
        public void SupportedCultures_ShouldNotRepeatItself()
        {
            List<CultureInfo> cultures = LocalizationManager.GetSupportedCultures();

            Assert.Equal(cultures.Count, cultures.Select(c => c.Name).Distinct().Count());
        }

        // ---------------------------------------------------------------- switching

        [Theory]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("pt-BR")]
        [InlineData("en-US")]
        public void SetCulture_ShouldSwitchTheThreadCulture(string name)
        {
            using var scope = new CultureScope();

            LocalizationManager.SetCulture(name);

            Assert.Equal(name, Thread.CurrentThread.CurrentCulture.Name);
            Assert.Equal(name, Thread.CurrentThread.CurrentUICulture.Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void SetCulture_ShouldFallBackToTheDefault_WhenGivenNothing(string name)
        {
            using var scope = new CultureScope();

            LocalizationManager.SetCulture(name);

            Assert.Equal(LocalizationManager.DEFAULT_CULTURE_CODE,
                Thread.CurrentThread.CurrentUICulture.Name);
        }

        [Fact]
        public void TheDefaultCultureCode_ShouldBeEnEn()
        {
            // Recorded, not fixed: "en-EN" is not a real culture. ICU manufactures one rather than
            // throwing, so it resolves to no satellite and falls through to the neutral resources —
            // which is the intended English, by accident rather than by design.
            Assert.Equal("en-EN", LocalizationManager.DEFAULT_CULTURE_CODE);
        }

        // ---------------------------------------------------------------- resources

        [Fact]
        public void AUiString_ShouldResolveInTheNeutralCulture()
        {
            using var scope = new CultureScope();
            LocalizationManager.SetCulture("en-US");

            Assert.False(string.IsNullOrWhiteSpace(Resources.Play));
        }

        [Theory]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("pt-BR")]
        public void AUiString_ShouldResolveInEveryTranslatedCulture(string name)
        {
            using var scope = new CultureScope();
            LocalizationManager.SetCulture(name);

            Assert.False(string.IsNullOrWhiteSpace(Resources.Play));
        }

        [Fact]
        public void ATranslatedString_ShouldActuallyDiffer_BetweenCultures()
        {
            using var scope = new CultureScope();

            LocalizationManager.SetCulture("en-US");
            string english = Resources.Play;
            LocalizationManager.SetCulture("pt-BR");
            string portuguese = Resources.Play;

            Assert.NotEqual(english, portuguese);
        }

        [Fact]
        public void AnUntranslatedCulture_ShouldFallBackToTheNeutralText()
        {
            using var scope = new CultureScope();
            LocalizationManager.SetCulture("en-US");
            string neutral = Resources.Play;

            // German has no satellite assembly, so the neutral resource is what shows.
            LocalizationManager.SetCulture("de-DE");

            Assert.Equal(neutral, Resources.Play);
        }

        [Fact]
        public void TheStageNameTheHudShows_ShouldResolve()
        {
            using var scope = new CultureScope();
            LocalizationManager.SetCulture("pt-BR");

            Assert.False(string.IsNullOrWhiteSpace(Resources.StageCapaoRaso));
        }

        [Fact]
        public void SwitchingCulture_ShouldBeReversible()
        {
            using var scope = new CultureScope();
            LocalizationManager.SetCulture("fr-FR");
            string french = Resources.Play;

            LocalizationManager.SetCulture("es-ES");
            LocalizationManager.SetCulture("fr-FR");

            Assert.Equal(french, Resources.Play);
        }
    }
}
