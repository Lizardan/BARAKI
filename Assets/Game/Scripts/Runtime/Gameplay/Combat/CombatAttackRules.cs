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
            role is UnitRole.Melee or UnitRole.Siege or UnitRole.Super;

        public static bool UsesProjectile(UnitRole role) =>
            role is UnitRole.Ranged
                or UnitRole.Caster
                or UnitRole.Flying;

        public static bool UsesParabolicArc(UnitRole role) => role == UnitRole.Ranged;
    }
}
