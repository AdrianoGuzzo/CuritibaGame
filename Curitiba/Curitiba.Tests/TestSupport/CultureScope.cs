using System;
using System.Globalization;
using System.Threading;
using Xunit;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// Restores the thread's culture when the test ends.
    /// </summary>
    /// <remarks>
    /// <c>LocalizationManager.SetCulture</c> writes <see cref="Thread.CurrentThread"/>'s culture and
    /// nothing ever puts it back. Test threads come from a pool, so without this a localization test
    /// would leave its culture behind for whatever test runs next on the same thread — exactly the
    /// kind of order dependence the suite must not have.
    /// </remarks>
    internal sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo culture;
        private readonly CultureInfo uiCulture;

        public CultureScope()
        {
            culture = Thread.CurrentThread.CurrentCulture;
            uiCulture = Thread.CurrentThread.CurrentUICulture;
        }

        public void Dispose()
        {
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = uiCulture;
        }
    }

    /// <summary>
    /// Marks the tests that mutate process- or thread-wide state so xunit runs them one at a time.
    /// </summary>
    /// <remarks>
    /// Two pieces of global state force this: the thread culture above, and
    /// <c>BaseSettingsStorage.SpecialFolderPath</c>, which is a <c>protected static</c> shared by
    /// every storage instance — constructing a second storage type retargets the first one.
    /// </remarks>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class GlobalStateCollection
    {
        public const string Name = "global-state";
    }
}
