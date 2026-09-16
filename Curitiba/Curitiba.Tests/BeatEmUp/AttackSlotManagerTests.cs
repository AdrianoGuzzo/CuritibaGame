using Curitiba.Core.BeatEmUp;
using Curitiba.Tests.TestSupport;
using Microsoft.Xna.Framework;
using Xunit;

namespace Curitiba.Tests.BeatEmUp
{
    /// <summary>
    /// The crowd-control rules: a ring of standing slots around the player so enemies surround
    /// rather than stack, and a small pool of attack tokens so only a couple swing at once.
    /// </summary>
    public class AttackSlotManagerTests
    {
        private static readonly Vector2 PlayerPosition = new Vector2(400f, 400f);

        [Fact]
        public void Manager_ShouldOfferEightSlots()
        {
            var slots = new AttackSlotManager();

            Assert.Equal(8, slots.SlotCount);
        }

        [Fact]
        public void Manager_ShouldDefaultToTwoSimultaneousAttackers()
        {
            var slots = new AttackSlotManager();

            Assert.Equal(2, slots.MaxAttackers);
        }

        [Fact]
        public void Reserve_ShouldGiveAFreeSlot()
        {
            var slots = new AttackSlotManager();
            var enemy = new TestFighter { Position = PlayerPosition };

            int slot = slots.Reserve(enemy, PlayerPosition);

            Assert.InRange(slot, 0, slots.SlotCount - 1);
        }

        [Fact]
        public void Reserve_ShouldKeepTheSlotTheEnemyAlreadyHolds()
        {
            var slots = new AttackSlotManager();
            var enemy = new TestFighter { Position = PlayerPosition };
            int first = slots.Reserve(enemy, PlayerPosition);

            // Even from a completely different position, an enemy never swaps slots mid-fight.
            enemy.Position = new Vector2(2000f, 100f);
            int second = slots.Reserve(enemy, PlayerPosition);

            Assert.Equal(first, second);
        }

        [Fact]
        public void Reserve_ShouldPickTheNearestFreeSlot_SoEnemiesDoNotCrossTheRing()
        {
            var slots = new AttackSlotManager();
            // Far to the player's right: the nearest slot is the right-hand one at (+70, 0).
            var enemy = new TestFighter { Position = PlayerPosition + new Vector2(300f, 0f) };

            int slot = slots.Reserve(enemy, PlayerPosition);

            Assert.Equal(PlayerPosition + new Vector2(70f, 0f), slots.WorldPosition(slot, PlayerPosition));
        }

        [Fact]
        public void Reserve_ShouldGiveEachEnemyItsOwnSlot()
        {
            var slots = new AttackSlotManager();
            var first = new TestFighter { Position = PlayerPosition };
            var second = new TestFighter { Position = PlayerPosition };

            int a = slots.Reserve(first, PlayerPosition);
            int b = slots.Reserve(second, PlayerPosition);

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void Reserve_ShouldReturnMinusOne_WhenEverySlotIsTaken()
        {
            var slots = new AttackSlotManager();
            for (int i = 0; i < slots.SlotCount; i++)
                slots.Reserve(new TestFighter { Position = PlayerPosition }, PlayerPosition);

            int overflow = slots.Reserve(new TestFighter { Position = PlayerPosition }, PlayerPosition);

            Assert.Equal(-1, overflow);
        }

        [Fact]
        public void WorldPosition_ShouldFollowThePlayer()
        {
            var slots = new AttackSlotManager();
            var enemy = new TestFighter { Position = PlayerPosition };
            int slot = slots.Reserve(enemy, PlayerPosition);

            Vector2 here = slots.WorldPosition(slot, PlayerPosition);
            Vector2 there = slots.WorldPosition(slot, PlayerPosition + new Vector2(500f, 0f));

            Assert.Equal(here + new Vector2(500f, 0f), there);
        }

        [Fact]
        public void Release_ShouldFreeTheSlotForSomeoneElse()
        {
            var slots = new AttackSlotManager();
            var enemy = new TestFighter { Position = PlayerPosition };
            for (int i = 0; i < slots.SlotCount - 1; i++)
                slots.Reserve(new TestFighter { Position = PlayerPosition }, PlayerPosition);
            slots.Reserve(enemy, PlayerPosition);
            Assert.Equal(-1, slots.Reserve(new TestFighter { Position = PlayerPosition }, PlayerPosition));

            slots.Release(enemy);

            Assert.NotEqual(-1, slots.Reserve(new TestFighter { Position = PlayerPosition }, PlayerPosition));
        }

        [Fact]
        public void TryAcquireAttackToken_ShouldSucceed_WhileThePoolHasRoom()
        {
            var slots = new AttackSlotManager(maxAttackers: 2);

            Assert.True(slots.TryAcquireAttackToken(new TestFighter()));
            Assert.True(slots.TryAcquireAttackToken(new TestFighter()));
        }

        [Fact]
        public void TryAcquireAttackToken_ShouldFail_OnceTheAttackerLimitIsMet()
        {
            var slots = new AttackSlotManager(maxAttackers: 2);
            slots.TryAcquireAttackToken(new TestFighter());
            slots.TryAcquireAttackToken(new TestFighter());

            // The third enemy must keep circling and wait its turn.
            Assert.False(slots.TryAcquireAttackToken(new TestFighter()));
        }

        [Fact]
        public void TryAcquireAttackToken_ShouldBeIdempotent_ForAnEnemyThatAlreadyHoldsOne()
        {
            var slots = new AttackSlotManager(maxAttackers: 1);
            var enemy = new TestFighter();
            slots.TryAcquireAttackToken(enemy);

            Assert.True(slots.TryAcquireAttackToken(enemy));
            Assert.False(slots.TryAcquireAttackToken(new TestFighter()));
        }

        [Fact]
        public void ReleaseAttackToken_ShouldLetTheNextEnemyTakeATurn()
        {
            var slots = new AttackSlotManager(maxAttackers: 1);
            var first = new TestFighter();
            slots.TryAcquireAttackToken(first);

            slots.ReleaseAttackToken(first);

            Assert.True(slots.TryAcquireAttackToken(new TestFighter()));
        }

        [Fact]
        public void Release_ShouldAlsoDropTheAttackToken()
        {
            var slots = new AttackSlotManager(maxAttackers: 1);
            var enemy = new TestFighter { Position = PlayerPosition };
            slots.Reserve(enemy, PlayerPosition);
            slots.TryAcquireAttackToken(enemy);

            // This is what happens on death, knockdown or despawn.
            slots.Release(enemy);

            Assert.True(slots.TryAcquireAttackToken(new TestFighter()));
        }

        [Fact]
        public void Reset_ShouldClearEverySlotAndToken()
        {
            var slots = new AttackSlotManager(maxAttackers: 2);
            for (int i = 0; i < slots.SlotCount; i++)
            {
                var enemy = new TestFighter { Position = PlayerPosition };
                slots.Reserve(enemy, PlayerPosition);
                slots.TryAcquireAttackToken(enemy);
            }

            slots.Reset();

            Assert.NotEqual(-1, slots.Reserve(new TestFighter { Position = PlayerPosition }, PlayerPosition));
            Assert.True(slots.TryAcquireAttackToken(new TestFighter()));
        }
    }
}
