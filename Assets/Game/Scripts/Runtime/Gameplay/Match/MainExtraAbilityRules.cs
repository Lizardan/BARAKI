using System;

namespace Game.Gameplay.Match
{
    public enum MainExtraAbilityTargetKind
    {
        None = 0,
        EnemyBuilding = 1,
        EnemyUnit = 2,
    }

    /// <summary>
    /// Post-Divine-Blessing main extra ability pick + combat tuning (PRE-005).
    /// Menu is 2×3 (6 slots); only ids 1–2 are implemented.
    /// </summary>
    public static class MainExtraAbilityRules
    {
        public const int None = 0;
        public const int AbilityCount = 6;
        public const int ImplementedCount = 2;
        public const int BuildingSmiteId = 1;
        public const int UnitSmiteId = 2;
        /// <summary>Last command slot on the main building panel (MAIN-001: ice ring 9, wave 10).</summary>
        public const int CommandSlotIndex = 11;

        public const int RequiredMeleeLevel = 7;
        public const int RequiredRangedLevel = 7;
        public const int RequiredArmorLevel = 7;
        public const int RequiredMagicLevel = 2;

        public const float CooldownSeconds = 180f;
        public const float ManaCost = 200f;
        public const float BuildingSmiteDamage = 1200f;
        public const float UnitSmiteDamage = 5000f;

        /// <summary>Main mana pool = this × MainLevel (L1=100, L2=200, L3=300).</summary>
        public const float ManaPerMainLevel = 100f;

        public static float GetMainManaMax(int mainLevel) =>
            ManaPerMainLevel * Math.Max(1, mainLevel);

        public static float GetMainManaRegenPerSecond(int mainLevel) =>
            GetMainManaMax(mainLevel) / CooldownSeconds;

        public static bool IsValidId(int abilityId) =>
            abilityId >= 1 && abilityId <= AbilityCount;

        public static bool IsImplemented(int abilityId) =>
            abilityId is BuildingSmiteId or UnitSmiteId;

        public static bool MeetsCombatGate(
            int meleeLevel,
            int rangedLevel,
            int armorLevel,
            int magicLevel) =>
            meleeLevel >= RequiredMeleeLevel
            && rangedLevel >= RequiredRangedLevel
            && armorLevel >= RequiredArmorLevel
            && magicLevel >= RequiredMagicLevel;

        public static bool IsUnlocked(
            int abilityId,
            int meleeLevel,
            int rangedLevel,
            int armorLevel,
            int passiveGoldLevel,
            int magicLevel)
        {
            _ = passiveGoldLevel;
            if (!IsImplemented(abilityId))
            {
                return false;
            }

            return MeetsCombatGate(meleeLevel, rangedLevel, armorLevel, magicLevel);
        }

        public static bool IsUnlocked(int abilityId, MatchPlayerState player)
        {
            if (player == null)
            {
                return false;
            }

            return IsUnlocked(
                abilityId,
                player.MeleeDamageLevel,
                player.RangedDamageLevel,
                player.HpArmorLevel,
                player.PassiveGoldLevel,
                player.MagicLevel);
        }

        public static bool CanPick(MatchPlayerState player, int abilityId)
        {
            if (player == null
                || !player.DivineBlessingComplete
                || player.MainExtraAbilityId != None
                || !IsValidId(abilityId)
                || !IsImplemented(abilityId))
            {
                return false;
            }

            return IsUnlocked(abilityId, player);
        }

        public static MainExtraAbilityTargetKind GetTargetKind(int abilityId) => abilityId switch
        {
            BuildingSmiteId => MainExtraAbilityTargetKind.EnemyBuilding,
            UnitSmiteId => MainExtraAbilityTargetKind.EnemyUnit,
            _ => MainExtraAbilityTargetKind.None,
        };

        public static float GetDamage(int abilityId) => abilityId switch
        {
            BuildingSmiteId => BuildingSmiteDamage,
            UnitSmiteId => UnitSmiteDamage,
            _ => 0f,
        };

        public static string GetDisplayName(int abilityId) => abilityId switch
        {
            BuildingSmiteId => "Кара зданий",
            UnitSmiteId => "Кара юнитов",
            _ => IsValidId(abilityId) ? "Скоро" : string.Empty,
        };

        public static string GetGateDescription(int abilityId)
        {
            if (!IsImplemented(abilityId))
            {
                return IsValidId(abilityId) ? "Скоро" : string.Empty;
            }

            return $"Мили ≥ {RequiredMeleeLevel}, дальний ≥ {RequiredRangedLevel}, " +
                   $"броня ≥ {RequiredArmorLevel}, магия ≥ {RequiredMagicLevel}";
        }

        public static string GetEffectDescription(int abilityId) => abilityId switch
        {
            BuildingSmiteId =>
                $"Наносит {BuildingSmiteDamage:0} урона вражескому зданию\n" +
                $"Перезарядка {CooldownSeconds:0}с · {ManaCost:0} маны",
            UnitSmiteId =>
                $"Наносит {UnitSmiteDamage:0} урона вражескому юниту\n" +
                $"Перезарядка {CooldownSeconds:0}с · {ManaCost:0} маны",
            _ => IsValidId(abilityId) ? "Способность пока недоступна" : string.Empty,
        };

        public static string GetMenuTooltip(int abilityId)
        {
            var name = GetDisplayName(abilityId);
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            return $"{name}\n{GetEffectDescription(abilityId)}\nТребуется: {GetGateDescription(abilityId)}";
        }

        public static bool CanCast(MatchPlayerState player)
        {
            if (player == null
                || !player.DivineBlessingComplete
                || !IsImplemented(player.MainExtraAbilityId))
            {
                return false;
            }

            return player.MainExtraAbilityCooldownRemaining <= 0f
                   && player.MainMana >= ManaCost;
        }
    }
}
