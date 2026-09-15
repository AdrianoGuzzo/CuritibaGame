using System.Collections.Generic;
using Curitiba.Core.BeatEmUp;

namespace Curitiba.Tests.TestSupport
{
    /// <summary>
    /// An <see cref="IEnemyFactory"/> that records what it was asked to build instead of building it.
    /// </summary>
    /// <remarks>
    /// This is what makes the spawn rules testable with no content: <c>SpawnManager</c> resolves the
    /// birth point, the walk-in target, the profile and the tuning, hands the request to the factory,
    /// and never looks at what comes back — so returning <c>null</c> is safe and the recorded requests
    /// are the whole observable output of a wave.
    /// </remarks>
    internal sealed class RecordingEnemyFactory : IEnemyFactory
    {
        private readonly List<string> types = new List<string>();
        private readonly List<EnemySpawnRequest> requests = new List<EnemySpawnRequest>();

        /// <summary>The requests, in spawn order.</summary>
        public IReadOnlyList<EnemySpawnRequest> Requests => requests;

        /// <summary>The enemy type asked for, in spawn order.</summary>
        public IReadOnlyList<string> Types => types;

        public int Count => requests.Count;

        public PiaLocoEnemy Create(string type, EnemySpawnRequest request)
        {
            types.Add(type);
            requests.Add(request);
            return null;
        }
    }
}
