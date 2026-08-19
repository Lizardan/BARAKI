namespace Game.Gameplay.Vfx
{
    /// <summary>
    /// Where a cast VFX plays. <see cref="Unspecified"/> is the serialized default on existing
    /// assets so the first resolve can seed Caster/Target/Ground/Impact from ability id.
    /// </summary>
    public enum AbilityVfxAnchor
    {
        Unspecified = 0,
        Caster = 1,
        Target = 2,
        Ground = 3,
        /// <summary>Splash / spawn / building hit at <c>cast.CenterPosition</c>.</summary>
        Impact = 4,
    }
}
