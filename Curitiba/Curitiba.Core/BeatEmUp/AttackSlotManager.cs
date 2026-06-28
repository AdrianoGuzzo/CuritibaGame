using Microsoft.Xna.Framework;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Coordinates a crowd of enemies around the player the way classic beat 'em ups do.
    /// It owns a ring of <em>slots</em> (offsets relative to the player's feet) — at most one
    /// enemy per slot — so enemies surround the player instead of stacking on one point, plus
    /// a small pool of <em>attack tokens</em> that caps how many may swing at once. Everyone
    /// else circles and waits its turn.
    ///
    /// X is screen-horizontal (the two side lanes are the main attack approaches); Y is corridor
    /// depth. The ring sits a little outside melee range — an enemy steps in from its slot to
    /// strike, then backs off, which reads as the arcade "take turns" rhythm.
    /// </summary>
    internal sealed class AttackSlotManager
    {
        private static readonly Vector2[] SlotOffsets =
        {
            new Vector2(-70f,   0f), new Vector2(70f,   0f),
            new Vector2(-58f, -30f), new Vector2(58f, -30f),
            new Vector2(-58f,  30f), new Vector2(58f,  30f),
            new Vector2(  0f, -46f), new Vector2( 0f,  46f),
        };

        private readonly Fighter[] slotOwners;
        private readonly Fighter[] attackers;
        private readonly int maxAttackers;

        public AttackSlotManager(int maxAttackers = 2)
        {
            this.maxAttackers = maxAttackers;
            slotOwners = new Fighter[SlotOffsets.Length];
            attackers = new Fighter[maxAttackers];
        }

        public int SlotCount => SlotOffsets.Length;
        public int MaxAttackers => maxAttackers;

        /// <summary>
        /// Reserves a slot for <paramref name="enemy"/>: keeps the one it already holds, otherwise
        /// grabs the free slot whose world position is nearest the enemy (so it doesn't cross the
        /// ring). Returns the slot index, or -1 if every slot is taken.
        /// </summary>
        public int Reserve(Fighter enemy, Vector2 playerPos)
        {
            for (int i = 0; i < slotOwners.Length; i++)
            {
                if (slotOwners[i] == enemy)
                    return i;
            }

            int best = -1;
            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < slotOwners.Length; i++)
            {
                if (slotOwners[i] != null)
                    continue;

                float d = Vector2.DistanceSquared(enemy.Position, playerPos + SlotOffsets[i]);
                if (d < bestDistanceSq)
                {
                    bestDistanceSq = d;
                    best = i;
                }
            }

            if (best >= 0)
                slotOwners[best] = enemy;
            return best;
        }

        /// <summary>World standing point of a slot: its offset added to the player's feet.</summary>
        public Vector2 WorldPosition(int slotIndex, Vector2 playerPos) => playerPos + SlotOffsets[slotIndex];

        /// <summary>Frees this enemy's slot and any attack token (call on death, knockdown, despawn).</summary>
        public void Release(Fighter enemy)
        {
            for (int i = 0; i < slotOwners.Length; i++)
            {
                if (slotOwners[i] == enemy)
                    slotOwners[i] = null;
            }
            ReleaseAttackToken(enemy);
        }

        /// <summary>
        /// Tries to claim one of the limited attack tokens. Returns true if the enemy already holds
        /// one or there was a free slot in the pool; false when the attacker limit is already met
        /// (so the enemy must keep circling and wait its turn).
        /// </summary>
        public bool TryAcquireAttackToken(Fighter enemy)
        {
            for (int i = 0; i < attackers.Length; i++)
            {
                if (attackers[i] == enemy)
                    return true;
            }
            for (int i = 0; i < attackers.Length; i++)
            {
                if (attackers[i] == null)
                {
                    attackers[i] = enemy;
                    return true;
                }
            }
            return false;
        }

        public void ReleaseAttackToken(Fighter enemy)
        {
            for (int i = 0; i < attackers.Length; i++)
            {
                if (attackers[i] == enemy)
                    attackers[i] = null;
            }
        }

        /// <summary>Clears all slots and tokens — call when a new wave's crowd spawns.</summary>
        public void Reset()
        {
            for (int i = 0; i < slotOwners.Length; i++)
                slotOwners[i] = null;
            for (int i = 0; i < attackers.Length; i++)
                attackers[i] = null;
        }
    }
}
