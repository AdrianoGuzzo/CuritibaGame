using Microsoft.Xna.Framework;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Describes the active hitbox produced by a fighter while it is in the
    /// "active frames" of an attack. It only exists for the few frames the
    /// blow can connect; each swing may only land on a given target once.
    /// </summary>
    internal readonly struct AttackData
    {
        /// <summary>World-space rectangle that deals damage while active.</summary>
        public readonly Rectangle Hitbox;

        /// <summary>Damage applied on a successful hit.</summary>
        public readonly int Damage;

        /// <summary>Velocity imparted to whatever is struck (already oriented).</summary>
        public readonly Vector2 Knockback;

        /// <summary>
        /// When true this blow launches the target into the <see cref="FighterState.Thrown"/>
        /// flight (the finisher kick) instead of a normal stagger/knockdown: the struck fighter
        /// is flung the full <see cref="Knockback"/> and can bowl into other fighters mid-air.
        /// </summary>
        public readonly bool Launches;

        public AttackData(Rectangle hitbox, int damage, Vector2 knockback, bool launches = false)
        {
            Hitbox = hitbox;
            Damage = damage;
            Knockback = knockback;
            Launches = launches;
        }
    }
}
