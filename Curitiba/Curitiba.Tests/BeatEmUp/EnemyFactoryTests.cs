using System.Collections.Generic;
using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// The enemy factory: the single place a mook is born, and so the single place the arena can
    /// hook one up without having to remember to do it at every spawn site.
    /// </summary>
    public class EnemyFactoryTests
    {
        private static EnemyFactory NewFactory(ContentManager content,
                                               System.Action<PiaLocoEnemy> onSpawned = null)
        {
            var sofia = new SofiaPlayer(content, null);
            return new EnemyFactory(content, null, sofia, new List<PiaLocoEnemy>(),
                                    new AttackSlotManager(), onSpawned);
        }

        private static EnemySpawnRequest Request() => new EnemySpawnRequest
        {
            SpawnPosition = new Vector2(500f, 400f),
            EntryTarget = new Vector2(450f, 400f),
            Profile = EnemyProfile.From(EnemyPersonality.Balanced),
            HitsToKnockdown = 3,
        };

        [Fact]
        public void EveryEnemyBuilt_ShouldBeAnnouncedToTheArena()
        {
            // Arrange
            ContentManager content = HeadlessContent.Create();
            var spawned = new List<PiaLocoEnemy>();
            EnemyFactory factory = NewFactory(content, spawned.Add);

            // Act
            PiaLocoEnemy enemy = factory.Create(EnemyFactory.DefaultType, Request());

            // Assert
            Assert.Same(enemy, Assert.Single(spawned));
        }

        [Fact]
        public void AnEnemyFromARegisteredBuilder_ShouldBeAnnouncedLikeAnyOther()
        {
            // Arrange - a type registered after the fact, which is the extension seam the
            // registry exists for. Hooking the built-in builder alone would let it be born mute.
            ContentManager content = HeadlessContent.Create();
            var spawned = new List<PiaLocoEnemy>();
            EnemyFactory factory = NewFactory(content, spawned.Add);
            var sofia = new SofiaPlayer(content, null);
            factory.Register("boss", r => Enemies.Create(content, sofia, r.SpawnPosition));

            // Act
            PiaLocoEnemy boss = factory.Create("boss", Request());

            // Assert
            Assert.Same(boss, Assert.Single(spawned));
        }

        [Fact]
        public void AFactoryWithNothingToAnnounceTo_ShouldStillBuildEnemies()
        {
            // Arrange - the hook is optional, the way the sound channel is.
            ContentManager content = HeadlessContent.Create();
            EnemyFactory factory = NewFactory(content);

            // Act
            PiaLocoEnemy enemy = factory.Create(EnemyFactory.DefaultType, Request());

            // Assert
            Assert.NotNull(enemy);
        }
    }
}
