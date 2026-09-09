namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Faceless champion (hero/titan) kit tuning — Call of the Deep (Plan0909, Фаза 5).
    /// One Human slot on each Faceless hero/titan base and veteran kit is replaced by the servant
    /// summon: <c>CallOfTheDeep</c> consumes a recent corpse in range and summons two servants
    /// (one if no corpse is available, spawned beside the champion).
    /// Numbers here are fallbacks for the runtime kits; the editor builder bakes real values
    /// into the def assets (<see cref="Data.UnitAbilityDef"/>).
    /// </summary>
    public static class FacelessHeroRules
    {
        /// <summary>Corpse-search radius / effective cast range of Call of the Deep.</summary>
        public const float CallCastRange = 6f;

        /// <summary>Corpses younger than this can be consumed by Call of the Deep.</summary>
        public const float CallCorpseMaxAgeSeconds = 15f;

        /// <summary>Cooldown between servant summons (shares the replaced slot's unlock value).</summary>
        public const float CallCooldownSeconds = 25f;

        /// <summary>Servants summoned when a corpse in range is consumed.</summary>
        public const int ServantsWithCorpse = 2;

        /// <summary>Servants summoned next to the champion when no corpse is in range.</summary>
        public const int ServantsWithoutCorpse = 1;
    }
}