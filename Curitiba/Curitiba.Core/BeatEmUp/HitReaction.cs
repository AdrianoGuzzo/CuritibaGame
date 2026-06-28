namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// How a struck fighter reacts to a blow, on top of the raw damage. Lets the same
    /// <see cref="Fighter.TakeDamage(int, Microsoft.Xna.Framework.Vector2, HitReaction)"/> drive a
    /// normal stagger, the finisher's launch, or the forced knockdown of a bowled-over bystander.
    /// </summary>
    internal enum HitReaction
    {
        /// <summary>Stagger in place / poise-driven knockdown (the default for punches and enemy hits).</summary>
        Normal,

        /// <summary>Fling the target into the <see cref="FighterState.Thrown"/> flight (the finisher kick).</summary>
        Launch,

        /// <summary>Force the target down at once (a thrown fighter bowling into a bystander).</summary>
        Knockdown,
    }
}
