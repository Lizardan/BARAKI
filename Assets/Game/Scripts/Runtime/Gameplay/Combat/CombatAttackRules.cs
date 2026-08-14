using Game.Gameplay.Data;

namespace Game.Gameplay.Combat
{
    /// <summary>Attack delivery timing and trajectory rules.</summary>
    public static class CombatAttackRules
    {
        public const float MeleeStrikeDuration = 0.14f;
        public const float MeleeLungeDistance = 0.55f;
        public const float ProjectileSpeed = 22f;
        public const float ParabolicArcHeight = 2.8f;
        public const float ProjectileBodyHeight = 1.1f;

        public static bool UsesMeleeStrike(UnitRole role) =>
            role is UnitRole.Melee or UnitRole.Siege;

        public static bool UsesMeleeStrike(UnitRole role, bool isHero, int heroSlot) =>
            !(isHero && heroSlot == HeroAbilityRules.PriestSlot)
            && UsesMeleeStrike(role);

        public static bool UsesProjectile(UnitRole role) =>
            role is UnitRole.Ranged
                or UnitRole.Caster
                or UnitRole.Flying
                or UnitRole.Super;

        /// <summary>Priest (hero slot 3) shoots like a caster; other heroes stay melee.</summary>
        public static bool UsesProjectile(UnitRole role, bool isHero, int heroSlot) =>
            (isHero && heroSlot == HeroAbilityRules.PriestSlot)
            || UsesProjectile(role);

        public static bool UsesParabolicArc(UnitRole role) =>
            role is UnitRole.Ranged or UnitRole.Flying;
    }
}
