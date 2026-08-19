namespace Game.Gameplay.Vfx
{
    /// <summary>
    /// Where a cast VFX plays. Caster/Target parent to the unit (follow).
    /// Ground/Impact stay in the world at the cast-time point.
    /// <see cref="Unspecified"/> is the serialized default on existing assets so the first
    /// resolve can seed Caster/Target/Ground/Impact from ability id.
    /// </summary>
    public enum AbilityVfxAnchor
    {
        Unspecified = 0,
        /// <summary>Parent to caster model. Travels with the unit.</summary>
        Caster = 1,
        /// <summary>Parent to target model at body height. Travels with the target.</summary>
        Target = 2,
        /// <summary>World point on the ground at the caster's feet. Does not follow.</summary>
        Ground = 3,
        /// <summary>World splash / spawn / building hit at <c>cast.CenterPosition</c>. Does not follow.</summary>
        Impact = 4,
    }
}
