using Game.Core;
using Game.Gameplay.Data;
using Game.Gameplay.Match;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Faceless-scoped bonus tuning: unit bonuses 1–6 (FACELESS-011) combat-mechanic constants
    /// and champion veteran (7–10) signature tuning (FACELESS-012). The race-agnostic slot layout
    /// (slots 1–12, veteran multipliers, effective-slot resolvers, <c>HasBonusKit</c>) lives in
    /// <see cref="BonusKitRules"/>. See <c>wiki/rules/faceless-unit-bonuses.md</c>.
    /// </summary>
    public static class FacelessBonusUnitRules
    {
        // --- Slot 1 Melee: Hunger of the Old One (vampirism) ---
        public const float VampiricProcChance = 0.15f;
        /// <summary>Heal = <see cref="VampiricHealPercent"/> × damage actually dealt (post-armor).</summary>
        public const float VampiricHealPercent = 0.5f;

        // --- Slot 2 Ranged: Tainting Bolt (dot) ---
        public const float TaintingBoltProcChance = 0.15f;
        public const float TaintingBoltDamagePerSecond = 3f;
        public const float TaintingBoltDurationSeconds = 3f;

        // --- Slot 3 Caster: Call of the Abyss (mini-melee on kill) ---
        /// <summary>Mini-melee stats = ×0.5 of the race Melee baseline.</summary>
        public const float MiniMeleeStatScale = 0.5f;

        // --- Slot 4 Siege: Death Explosion (AoE on death) ---
        public const float DeathExplosionMaxHpPercent = 0.10f;
        public const float DeathExplosionRadius = 3f;

        // --- Slot 5 Flying: Hungering Flight (attack-speed stacks on kill) ---
        public const float HungeringFlightAttackSpeedPerStack = 0.15f;

        // --- Slot 6 Super: Feast on the Fallen (heal + attack-speed stacks on kill) ---
        public const float FeastHealFlat = 80f;
        public const float FeastAttackSpeedPerStack = 0.10f;

        // --- Shared for slots 5–6 ---
        public const float FeastBuffDurationSeconds = 3f;
        public const int MaxFeastStacks = 3;

        /// <summary>
        /// True when <paramref name="bonusSlot"/> is a Faceless unit bonus matching <paramref name="role"/>.
        /// Races other than Faceless always resolve to false.
        /// </summary>
        public static bool IsFacelessBonus(int bonusSlot, UnitRole role, string raceId) =>
            raceId == GameIds.Races.Faceless
            && BonusKitRules.IsBonusSlot(bonusSlot)
            && BonusKitRules.RoleForBonusSlot(bonusSlot) == role;

        /// <summary>Attack-speed bonus per stack for slots 5–6 (0 for any other slot).</summary>
        public static float AttackSpeedPerStackForSlot(int bonusSlot) => bonusSlot switch
        {
            5 => HungeringFlightAttackSpeedPerStack,
            6 => FeastAttackSpeedPerStack,
            _ => 0f,
        };

        // --- Champion veteran slots 7–10 (FACELESS-012) ---

        /// <summary>Veteran morale aura percent (15% = stronger than the base 10% hero aura).</summary>
        public const float VeteranMoraleAuraPercent = 0.15f;

        // --- Signature tuning (FACELESS-012) ---

        /// <summary>Ancient Mantle (slot 7): self damage buff percent (applied via the Ultimate self-buff pipeline).</summary>
        public const float AncientMantleSelfDamageBonusPercent = 0.5f;
        /// <summary>Ancient Mantle (slot 7): self buff duration in seconds.</summary>
        public const float AncientMantleSelfBuffSeconds = 8f;
        /// <summary>Ancient Mantle (slot 7): AoE impact damage multiplier vs the base Ultimate (the +30%).</summary>
        public const float AncientMantleAoeDamageMultiplier = 1.3f;

        /// <summary>Area of Miss (slot 8): radius in which enemies miss all attacks.</summary>
        public const float AreaOfMissRadius = 5f;
        /// <summary>Area of Miss (slot 8): duration in seconds enemies keep missing.</summary>
        public const float AreaOfMissSeconds = 4f;

        /// <summary>Feast Zone (slot 9): heal-field radius (follows the Berserker).</summary>
        public const float FeastZoneRadius = 8f;
        /// <summary>Feast Zone (slot 9): duration in seconds the zone follows its caster.</summary>
        public const float FeastZoneSeconds = 10f;
        /// <summary>Feast Zone (slot 9): allies inside heal this fraction of the damage they personally deal.</summary>
        public const float FeastZoneHealFraction = 0.30f;

        /// <summary>Aura of Hunger (slot 10): each owner unit lifesteals this fraction of the damage it deals.</summary>
        public const float AuraOfHungerHealFraction = 0.15f;

        // --- Race unique slots 11–12 (FACELESS-013) ---

        /// <summary>Shadow of the Void (slot 11): chance an owner troop fully avoids any incoming attack.</summary>
        public const float ShadowEvadeChance = 0.08f;
        /// <summary>Void Bastion (slot 12): chance a direct attack against an owner building misses.</summary>
        public const float VoidBastionMissChance = 0.20f;

        /// <summary>
        /// True when the player owns Shadow of the Void (Faceless race unique slot 11).
        /// Player-level pick, independent of the Faceless <c>HasBonusKit</c> runtime gate.
        /// </summary>
        public static bool HasShadowOfTheVoid(MatchPlayerState player) =>
            player != null
            && player.RaceId == GameIds.Races.Faceless
            && player.BonusPickSlot == BonusKitRules.RaceUnique1Slot;

        /// <summary>
        /// True when the player owns Void Bastion (Faceless race unique slot 12).
        /// Player-level pick, independent of the Faceless <c>HasBonusKit</c> runtime gate.
        /// </summary>
        public static bool HasVoidBastion(MatchPlayerState player) =>
            player != null
            && player.RaceId == GameIds.Races.Faceless
            && player.BonusPickSlot == BonusKitRules.RaceUnique2Slot;
    }
}