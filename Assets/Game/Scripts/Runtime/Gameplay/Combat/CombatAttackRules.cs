using Game.Gameplay.Data;

namespace Game.Gameplay.Combat
{
    /// <summary>Attack delivery timing and trajectory rules.</summary>
    public static class CombatAttackRules
    {
        /// <summary>
        /// Default normalized time (0–1) within the attack clip for melee impact / projectile spawn.
        /// Paired with Attack animator speed = clipLength / attackInterval.
        /// </summary>
        public const float SwingImpactNormalizedTime = 0.5f;

        /// <summary>Archers release the arrow earlier in the draw (TT archer clip).</summary>
        public const float RangedSwingImpactNormalizedTime = 0.25f;

        /// <summary>Casters release the fireball slightly before mid-staff swing.</summary>
        public const float CasterSwingImpactNormalizedTime = 0.35f;

        /// <summary>Ballista (Super) releases near the start of the TT ballista attack clip.</summary>
        public const float SuperSwingImpactNormalizedTime = 0.1f;

        /// <summary>Legacy alias — prefer <see cref="ResolveSwingImpactDelay"/>.</summary>
        public const float MeleeStrikeDuration = 0.14f;

        public const float MeleeLungeDistance = 0.55f;
        public const float ProjectileSpeed = 22f;
        public const float ParabolicArcHeight = 2.8f;
        public const float ProjectileBodyHeight = 1.1f;

        public static float ResolveSwingImpactNormalizedTime(UnitRole role) =>
            role switch
            {
                UnitRole.Ranged => RangedSwingImpactNormalizedTime,
                UnitRole.Caster => CasterSwingImpactNormalizedTime,
                UnitRole.Super => SuperSwingImpactNormalizedTime,
                _ => SwingImpactNormalizedTime,
            };

        public static float ResolveSwingImpactDelay(float attackIntervalSeconds, UnitRole role = UnitRole.Melee) =>
            UnityEngine.Mathf.Max(
                0.05f,
                attackIntervalSeconds * ResolveSwingImpactNormalizedTime(role));

        public static bool UsesMeleeStrike(UnitRole role) =>
            role is UnitRole.Melee or UnitRole.Siege or UnitRole.Titan;

        /// <summary>
        /// Non-projectile auto-attacks (melee creeps, siege, titan, king/paladin heroes).
        /// Priest shoots; other heroes strike.
        /// </summary>
        public static bool UsesMeleeStrike(UnitRole role, bool isHero, int heroSlot)
        {
            if (isHero)
            {
                return heroSlot != HeroAbilityRules.PriestSlot;
            }

            return UsesMeleeStrike(role);
        }

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
