using Game.Gameplay.Data;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Builds <see cref="UnitCombatIdentity"/> (race + role + slots) for a unit.
    /// The race is resolved from the owning player; the combat system is the source
    /// of truth for that lookup (<see cref="MatchCombatSystem.GetPlayerRaceId"/>).
    /// </summary>
    public static class UnitCombatIdentityFactory
    {
        /// <summary>Identity for a live unit in the combat simulation.</summary>
        public static UnitCombatIdentity From(
            MatchUnitState unit,
            string ownerRaceId) =>
            new(
                ownerRaceId,
                unit.Role,
                unit.IsHero,
                unit.HeroSlot,
                unit.BonusSlot);

        /// <summary>Identity from explicit fields (used by editor/tests without a live unit).</summary>
        public static UnitCombatIdentity Of(
            string raceId,
            UnitRole role,
            bool isHero = false,
            int heroSlot = 0,
            int bonusSlot = 0) =>
            new(raceId, role, isHero, heroSlot, bonusSlot);
    }
}
