using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Content;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// A <see cref="ContentManager"/> that records every asset it was asked for and then fails,
    /// exactly as <see cref="HeadlessContent"/> does.
    /// </summary>
    /// <remarks>
    /// <see cref="HeadlessContent"/> answers "does a missing asset stay quiet"; this one answers
    /// "how many times was it asked for". That distinction is the whole point for sound effects: a
    /// player that retried a failed load would hit the disk several times a second in mid-combat,
    /// and nothing about the audible result would give it away.
    /// </remarks>
    internal sealed class CountingContentManager : ContentManager
    {
        private readonly List<string> loads = new List<string>();

        public CountingContentManager()
            : base(new EmptyServiceProvider(), "__no_content_in_tests__")
        {
        }

        /// <summary>The asset names asked for, in order, including repeats.</summary>
        public IReadOnlyList<string> Loads => loads;

        public override T Load<T>(string assetName)
        {
            loads.Add(assetName);
            throw new ContentLoadException("There is no compiled content in the test suite.");
        }

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public object GetService(Type serviceType) => null;
        }
    }
}
