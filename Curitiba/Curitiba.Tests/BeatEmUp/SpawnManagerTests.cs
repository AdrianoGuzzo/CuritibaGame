using System;
using System.Collections.Generic;
using System.Linq;
using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Microsoft.Xna.Framework;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// Turning a wave into enemies: where each one is born, where it walks in to, and which of the
    /// two authoring styles (explicit spawns, or a procedural count) wins.
    /// </summary>
    public class SpawnManagerTests
    {
        private const float ViewWidth = 800f;
        private const float SectionWidth = 2400f;
        private const float CorridorTop = 300f;
        private const float CorridorBottom = 448f;
        private const float OffscreenMargin = 60f;

        private sealed class Harness
        {
            public RecordingEnemyFactory Factory { get; } = new RecordingEnemyFactory();
            public List<PiaLocoEnemy> Enemies { get; } = new List<PiaLocoEnemy>();
            public AttackSlotManager Slots { get; } = new AttackSlotManager();
            public Camera2D Camera { get; }
            public SpawnManager Manager { get; }

            public Harness(SpawnPoint[] points = null, float sectionWidth = SectionWidth)
            {
                Camera = new Camera2D(ViewWidth, sectionWidth);
                Manager = new SpawnManager(Factory, Enemies, Slots,
                    name => EnemyProfile.From(ParsePersonality(name)),
                    type => FighterTuning.PiaLocoDefaults());
                Manager.Configure(Camera, sectionWidth, CorridorTop, CorridorBottom,
                    points ?? Array.Empty<SpawnPoint>());
            }

            private static EnemyPersonality ParsePersonality(string name) =>
                Enum.TryParse(name, out EnemyPersonality p) ? p : EnemyPersonality.Balanced;
        }

        private static SpawnPoint[] BothEdges() => new[]
        {
            new SpawnPoint("left", "LeftEntrance", new Vector2(0f, 400f), SpawnPointType.Left),
            new SpawnPoint("right", "RightEntrance", new Vector2(0f, 360f), SpawnPointType.Right),
        };

        // ---------------------------------------------------------------- procedural waves

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(5)]
        public void AProceduralWave_ShouldSpawnItsEnemyCount(int count)
        {
            var harness = new Harness();

            harness.Manager.SpawnWave(new SpawnArea(0f, count, 3, 0f));

            Assert.Equal(count, harness.Factory.Count);
            Assert.Equal(count, harness.Enemies.Count);
        }

        [Fact]
        public void AProceduralWave_ShouldSpawnNobody_WhenTheCountIsZero()
        {
            var harness = new Harness();

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f));

            Assert.Equal(0, harness.Factory.Count);
        }

        [Fact]
        public void AProceduralWave_ShouldUseTheDefaultEnemyType()
        {
            var harness = new Harness();

            harness.Manager.SpawnWave(new SpawnArea(0f, 3, 3, 0f));

            Assert.All(harness.Factory.Types, t => Assert.Equal(EnemyFactory.DefaultType, t));
        }

        [Fact]
        public void AProceduralWave_ShouldCycleThroughThePersonalityMix()
        {
            var harness = new Harness();

            harness.Manager.SpawnWave(new SpawnArea(0f, 5, 3, 0f));

            string[] personalities = harness.Factory.Requests.Select(r => r.Profile.Profile.ToString()).ToArray();
            Assert.Equal(new[] { "Aggressive", "Balanced", "Defensive", "Runner", "Aggressive" }, personalities);
        }

        [Fact]
        public void AProceduralWave_ShouldBornEveryEnemyOffTheRightEdge()
        {
            var harness = new Harness();

            harness.Manager.SpawnWave(new SpawnArea(0f, 3, 3, 0f));

            foreach (EnemySpawnRequest request in harness.Factory.Requests)
            {
                Assert.True(request.SpawnPosition.X > harness.Camera.Right,
                    "enemies must be born off-screen and walk in");
            }
        }

        [Fact]
        public void AProceduralWave_ShouldSpreadItsWalkInTargetsAcrossTheView()
        {
            var harness = new Harness();

            harness.Manager.SpawnWave(new SpawnArea(0f, 3, 3, 0f));

            float[] xs = harness.Factory.Requests.Select(r => r.EntryTarget.X).ToArray();
            Assert.True(xs[0] < xs[1] && xs[1] < xs[2], "the spread should fan out left to right");
        }

        [Fact]
        public void AProceduralWave_ShouldAlternateLanes()
        {
            var harness = new Harness();

            harness.Manager.SpawnWave(new SpawnArea(0f, 4, 3, 0f));

            float[] ys = harness.Factory.Requests.Select(r => r.EntryTarget.Y).ToArray();
            Assert.Equal(ys[0], ys[2]);
            Assert.Equal(ys[1], ys[3]);
            Assert.NotEqual(ys[0], ys[1]);
        }

        [Fact]
        public void EveryWalkInTarget_ShouldSitInsideTheCorridor()
        {
            var harness = new Harness();

            harness.Manager.SpawnWave(new SpawnArea(0f, 6, 3, 0f));

            foreach (EnemySpawnRequest request in harness.Factory.Requests)
                Assert.InRange(request.EntryTarget.Y, CorridorTop, CorridorBottom);
        }

        [Fact]
        public void ASingleEnemy_ShouldWalkInHalfwayAcrossTheSpread()
        {
            var harness = new Harness();

            harness.Manager.SpawnWave(new SpawnArea(0f, 1, 3, 0f));

            float right = Math.Min(harness.Camera.Right, SectionWidth);
            float spanLeft = MathHelper.Lerp(harness.Camera.Left, right, 0.55f);
            float expected = MathHelper.Lerp(spanLeft, right - 40f, 0.5f);
            Assert.Equal(expected, harness.Factory.Requests[0].EntryTarget.X, 2);
        }

        // ---------------------------------------------------------------- authored spawns

        [Fact]
        public void AuthoredSpawns_ShouldTakePriorityOverTheEnemyCount()
        {
            // The real shape in capao-raso.json: enemyCount says 3, but two spawns are authored.
            var harness = new Harness(BothEdges());
            var area = new SpawnArea(0f, 3, 3, 0f, new[]
            {
                new SpawnDef { Personality = "Aggressive", SpawnPoint = "left" },
                new SpawnDef { Personality = "Balanced", SpawnPoint = "right" },
            });

            harness.Manager.SpawnWave(area);

            Assert.Equal(2, harness.Factory.Count);
        }

        [Fact]
        public void AuthoredSpawns_ShouldBeUsed_EvenWhenTheEnemyCountIsZero()
        {
            var harness = new Harness(BothEdges());
            var area = new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { SpawnPoint = "left" },
                new SpawnDef { SpawnPoint = "left" },
                new SpawnDef { SpawnPoint = "right" },
            });

            harness.Manager.SpawnWave(area);

            Assert.Equal(3, harness.Factory.Count);
        }

        [Fact]
        public void AnEmptySpawnList_ShouldFallBackToTheProceduralSpread()
        {
            var harness = new Harness();

            harness.Manager.SpawnWave(new SpawnArea(0f, 4, 3, 0f, Array.Empty<SpawnDef>()));

            Assert.Equal(4, harness.Factory.Count);
        }

        [Fact]
        public void AnAuthoredSpawn_ShouldKeepItsPersonality()
        {
            var harness = new Harness(BothEdges());

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { Personality = "Runner", SpawnPoint = "left" },
            }));

            Assert.Equal("Runner", harness.Factory.Requests[0].Profile.Profile.ToString());
        }

        [Fact]
        public void AnAuthoredSpawn_ShouldFallBackToItsTemplate_WhenNoTypeIsGiven()
        {
            var harness = new Harness(BothEdges());

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { Type = null, Template = "piaLoco", SpawnPoint = "left" },
            }));

            Assert.Equal("piaLoco", harness.Factory.Types[0]);
        }

        [Fact]
        public void AnAuthoredSpawn_ShouldPreferItsExplicitType()
        {
            var harness = new Harness(BothEdges());

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { Type = "boss", Template = "piaLoco", SpawnPoint = "left" },
            }));

            Assert.Equal("boss", harness.Factory.Types[0]);
        }

        [Fact]
        public void AnAuthoredSpawn_ShouldWalkToItsExplicitTarget()
        {
            var harness = new Harness(BothEdges());

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { X = 500f, Y = 420f, SpawnPoint = "left" },
            }));

            Assert.Equal(new Vector2(500f, 420f), harness.Factory.Requests[0].EntryTarget);
        }

        [Fact]
        public void AnAuthoredSpawnAtTheOrigin_ShouldUseTheAutoSpreadSlot()
        {
            // 0,0 means "auto" — this is what the real stage file uses.
            var harness = new Harness(BothEdges());

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { X = 0f, Y = 0f, SpawnPoint = "left" },
            }));

            Vector2 target = harness.Factory.Requests[0].EntryTarget;
            Assert.NotEqual(Vector2.Zero, target);
            Assert.InRange(target.Y, CorridorTop, CorridorBottom);
        }

        [Fact]
        public void AnExplicitTargetOnLaneZero_ShouldBeMovedToMidCorridor()
        {
            var harness = new Harness(BothEdges());

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { X = 500f, Y = 0f, SpawnPoint = "left" },
            }));

            Assert.Equal((CorridorTop + CorridorBottom) / 2f, harness.Factory.Requests[0].EntryTarget.Y);
        }

        [Fact]
        public void AnExplicitTargetOutsideTheCorridor_ShouldBeClampedIntoIt()
        {
            var harness = new Harness(BothEdges());

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { X = 500f, Y = 9999f, SpawnPoint = "left" },
            }));

            Assert.Equal(CorridorBottom, harness.Factory.Requests[0].EntryTarget.Y);
        }

        // ---------------------------------------------------------------- spawn points

        [Fact]
        public void ALeftSpawnPoint_ShouldBirthTheEnemyOffTheLeftEdge()
        {
            var harness = new Harness(BothEdges());

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { X = 500f, Y = 400f, SpawnPoint = "left" },
            }));

            Assert.Equal(harness.Camera.Left - OffscreenMargin, harness.Factory.Requests[0].SpawnPosition.X);
        }

        [Fact]
        public void ARightSpawnPoint_ShouldBirthTheEnemyOffTheRightEdge()
        {
            var harness = new Harness(BothEdges());

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { X = 500f, Y = 400f, SpawnPoint = "right" },
            }));

            float expected = Math.Min(harness.Camera.Right, SectionWidth) + OffscreenMargin;
            Assert.Equal(expected, harness.Factory.Requests[0].SpawnPosition.X);
        }

        [Fact]
        public void ASpawnPoint_ShouldBeResolvableByNameAsWellAsId()
        {
            var harness = new Harness(BothEdges());

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { X = 500f, Y = 400f, SpawnPoint = "LeftEntrance" },
            }));

            Assert.Equal(harness.Camera.Left - OffscreenMargin, harness.Factory.Requests[0].SpawnPosition.X);
        }

        [Fact]
        public void ACustomSpawnPoint_ShouldBirthTheEnemyExactlyWhereItIsPlaced()
        {
            var points = new[]
            {
                new SpawnPoint("alley", "Alley", new Vector2(1234f, 333f), SpawnPointType.Custom),
            };
            var harness = new Harness(points);

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { X = 500f, Y = 400f, SpawnPoint = "alley" },
            }));

            Assert.Equal(new Vector2(1234f, 333f), harness.Factory.Requests[0].SpawnPosition);
        }

        [Fact]
        public void ASpawnWithNoPointReference_ShouldEnterFromTheNearestEdge()
        {
            var harness = new Harness();

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { X = 700f, Y = 400f },   // right of centre
                new SpawnDef { X = 50f, Y = 400f },    // left of centre
            }));

            Assert.True(harness.Factory.Requests[0].SpawnPosition.X > harness.Camera.Right);
            Assert.True(harness.Factory.Requests[1].SpawnPosition.X < harness.Camera.Left);
        }

        [Fact]
        public void AnUnknownSpawnPoint_ShouldFallBackToTheNearestEdge()
        {
            var harness = new Harness(BothEdges());

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { X = 700f, Y = 400f, SpawnPoint = "back-alley" },
            }));

            Assert.True(harness.Factory.Requests[0].SpawnPosition.X > harness.Camera.Right);
        }

        [Theory]
        [InlineData("random:Left")]
        [InlineData("random:left")]
        public void ARandomLeftReference_ShouldAlwaysResolveToALeftPoint(string reference)
        {
            // The pick is random, but the set it picks from is not — so this stays deterministic.
            var harness = new Harness(BothEdges());

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { X = 700f, Y = 400f, SpawnPoint = reference },
            }));

            Assert.Equal(harness.Camera.Left - OffscreenMargin, harness.Factory.Requests[0].SpawnPosition.X);
        }

        [Fact]
        public void AnUnfilteredRandomReference_ShouldResolveToOneOfTheDeclaredPoints()
        {
            var harness = new Harness(BothEdges());

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { X = 400f, Y = 400f, SpawnPoint = "random" },
            }));

            float left = harness.Camera.Left - OffscreenMargin;
            float right = Math.Min(harness.Camera.Right, SectionWidth) + OffscreenMargin;
            float actual = harness.Factory.Requests[0].SpawnPosition.X;
            Assert.True(actual == left || actual == right, $"expected one of the declared edges, got {actual}");
        }

        // ---------------------------------------------------------------- difficulty & bookkeeping

        [Fact]
        public void EverySpawn_ShouldCarryTheWaveDifficulty()
        {
            var harness = new Harness();

            harness.Manager.SpawnWave(new SpawnArea(0f, 3, hitsToKnockdown: 7, delay: 0f));

            Assert.All(harness.Factory.Requests, r => Assert.Equal(7, r.HitsToKnockdown));
        }

        [Fact]
        public void EverySpawn_ShouldCarryTheResolvedTuning()
        {
            var harness = new Harness();

            harness.Manager.SpawnWave(new SpawnArea(0f, 2, 3, 0f));

            Assert.All(harness.Factory.Requests, r => Assert.Equal(30, r.Tuning.MaxHealth));
        }

        [Fact]
        public void SpawningAWave_ShouldClearTheSlotsFromThePreviousOne()
        {
            var harness = new Harness();
            var stale = new TestFighter();
            harness.Slots.Reserve(stale, Vector2.Zero);
            harness.Slots.TryAcquireAttackToken(stale);

            harness.Manager.SpawnWave(new SpawnArea(0f, 1, 3, 0f));

            Assert.True(harness.Slots.TryAcquireAttackToken(new TestFighter()));
        }

        [Fact]
        public void SpawnPositions_ShouldFollowTheCamera()
        {
            var harness = new Harness(BothEdges());
            harness.Camera.SetX(1200f);

            harness.Manager.SpawnWave(new SpawnArea(0f, 0, 3, 0f, new[]
            {
                new SpawnDef { X = 1500f, Y = 400f, SpawnPoint = "left" },
            }));

            Assert.Equal(1200f - OffscreenMargin, harness.Factory.Requests[0].SpawnPosition.X);
        }
    }

    /// <summary>Where a named entry point puts an enemy, relative to the live view.</summary>
    public class SpawnPointTests
    {
        [Fact]
        public void ALeftPoint_ShouldSitBeyondTheLeftEdge_OnItsOwnLane()
        {
            var camera = new Camera2D(800f, 2400f);
            camera.SetX(500f);
            var point = new SpawnPoint("l", "L", new Vector2(0f, 410f), SpawnPointType.Left);

            Vector2 position = point.ResolveSpawnPosition(camera, 2400f, 60f);

            Assert.Equal(440f, position.X);
            Assert.Equal(410f, position.Y);
        }

        [Fact]
        public void ARightPoint_ShouldSitBeyondTheRightEdge()
        {
            var camera = new Camera2D(800f, 2400f);
            camera.SetX(500f);
            var point = new SpawnPoint("r", "R", new Vector2(0f, 360f), SpawnPointType.Right);

            Vector2 position = point.ResolveSpawnPosition(camera, 2400f, 60f);

            Assert.Equal(1360f, position.X);
            Assert.Equal(360f, position.Y);
        }

        [Fact]
        public void ARightPoint_ShouldNotReachPastTheEndOfTheSection()
        {
            var camera = new Camera2D(800f, 1000f);
            camera.SetX(200f);
            var point = new SpawnPoint("r", "R", new Vector2(0f, 360f), SpawnPointType.Right);

            Vector2 position = point.ResolveSpawnPosition(camera, 1000f, 60f);

            Assert.Equal(1060f, position.X);
        }

        [Fact]
        public void ACustomPoint_ShouldBeUsedVerbatim()
        {
            var camera = new Camera2D(800f, 2400f);
            camera.SetX(500f);
            var point = new SpawnPoint("c", "C", new Vector2(77f, 88f), SpawnPointType.Custom);

            Assert.Equal(new Vector2(77f, 88f), point.ResolveSpawnPosition(camera, 2400f, 60f));
        }
    }
}
