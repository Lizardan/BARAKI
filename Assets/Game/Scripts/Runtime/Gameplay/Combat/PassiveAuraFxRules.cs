using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>Which CFXR loop prefab to use for a passive army aura.</summary>
    public enum PassiveAuraFxKind
    {
        /// <summary>CFXR2 Shiny Item — soft sparkle (reserved; unused by current auras).</summary>
        Shiny = 0,
        /// <summary>CFXR3 Magic Aura A (Runic) — ground runic ring for all passive army auras.</summary>
        Runic = 1,
    }

    /// <summary>
    /// Maps passive aura ability ids to CFXR style + authentic tint.
    /// Siege regen uses holy yellow (paladin-light heal), not plain green.
    /// </summary>
    public static class PassiveAuraFxRules
    {
        /// <summary>Authored radius of CFXR3 Magic Aura A before scaling.</summary>
        public const float RunicReferenceRadius = 1.1f;

        /// <summary>Authored footprint of CFXR2 Shiny Item before scaling.</summary>
        public const float ShinyReferenceRadius = 1.4f;

        /// <summary>
        /// Visual-only shrink vs gameplay <c>auraRadius</c>. FX footprint is much smaller than
        /// the mechanical radius so the ring reads as a unit halo, not a full AoE disc.
        /// </summary>
        public const float VisualFootprintMultiplier = 0.2f;

        public static PassiveAuraFxKind ResolveKind(int abilityId) => abilityId switch
        {
            AbilityIds.AuraHpRegen => PassiveAuraFxKind.Runic,
            AbilityIds.AuraAttackSpeedPercent => PassiveAuraFxKind.Runic,
            AbilityIds.AuraDamagePercent => PassiveAuraFxKind.Runic,
            AbilityIds.AuraArmorPercent => PassiveAuraFxKind.Runic,
            AbilityIds.AuraMaxHpPercent => PassiveAuraFxKind.Runic,
            _ => PassiveAuraFxKind.Runic,
        };

        public static Color ResolveTint(int abilityId) => abilityId switch
        {
            AbilityIds.AuraHpRegen => AbilityFxColors.AuraHpRegen,
            AbilityIds.AuraAttackSpeedPercent => AbilityFxColors.AuraAttackSpeed,
            AbilityIds.AuraDamagePercent => AbilityFxColors.AuraDamage,
            AbilityIds.AuraArmorPercent => AbilityFxColors.AuraArmor,
            AbilityIds.AuraMaxHpPercent => AbilityFxColors.AuraMaxHp,
            _ => Color.white,
        };

        public static float ResolveScale(PassiveAuraFxKind kind, float auraRadius)
        {
            var reference = kind == PassiveAuraFxKind.Shiny ? ShinyReferenceRadius : RunicReferenceRadius;
            var scale = auraRadius / Mathf.Max(0.1f, reference) * VisualFootprintMultiplier;
            return Mathf.Max(0.08f, scale);
        }

        /// <summary>Client snapshot has color only — pick the nearest known aura tint.</summary>
        public static int ResolveAbilityIdFromColor(Color color)
        {
            var bestId = AbilityIds.AuraDamagePercent;
            var bestScore = float.MaxValue;
            Try(AbilityIds.AuraHpRegen, AbilityFxColors.AuraHpRegen);
            Try(AbilityIds.AuraAttackSpeedPercent, AbilityFxColors.AuraAttackSpeed);
            Try(AbilityIds.AuraDamagePercent, AbilityFxColors.AuraDamage);
            Try(AbilityIds.AuraArmorPercent, AbilityFxColors.AuraArmor);
            Try(AbilityIds.AuraMaxHpPercent, AbilityFxColors.AuraMaxHp);
            return bestId;

            void Try(int id, Color candidate)
            {
                var dr = color.r - candidate.r;
                var dg = color.g - candidate.g;
                var db = color.b - candidate.b;
                var score = dr * dr + dg * dg + db * db;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestId = id;
                }
            }
        }
    }
}
