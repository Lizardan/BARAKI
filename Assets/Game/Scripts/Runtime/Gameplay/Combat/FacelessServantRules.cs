using Game.Gameplay.Data;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Прислужник Древних (servant) — ключевой юнит-мотив расы (Plan0909, Фаза 1).
    /// Заменяет прежний масштабируемый от живого Melee мини-юнит: фиксированный профиль.
    /// Спавн всегда через <see cref="MatchCombatSystem.SummonMinion"/> (bonusSlot = SummonBonusSlot-маркер),
    /// визуал — префаб Faceless_Servant: меш/материалы как у Melee, оружие скрыто через AxHandle01_16 scale 0.
    /// Источник дизайн-чисел — ассет UNIT_FACELESS_SERVANT (зеркало этих констант).
    /// </summary>
    public static class FacelessServantRules
    {
        // --- Фиксированный servant-профиль (canon: 60 / 0 / 4-5 / AS 1 / range 1.5 / speed 4 / bounty 0) ---
        public const UnitRole Role = UnitRole.Melee;
        public const float MaxHp = 60f;
        public const float Armor = 0f;
        public const float DamageMin = 4f;
        public const float DamageMax = 5f;
        public const float AttackSpeed = 1f;
        public const float AttackRange = 1.5f;
        public const float MoveSpeed = 4f;
        public const int GoldBounty = 0;

        /// <summary>Servant combat profile (same shape as other race units).</summary>
        public static UnitCombatStats BuildStats() =>
            new(
                Role,
                MaxHp,
                Armor,
                DamageMin,
                DamageMax,
                AttackSpeed,
                AttackRange,
                MoveSpeed,
                GoldBounty,
                maxMana: 0f);

        /// <summary>True when a unit is a summoned servant (bonus-slot summon marker).</summary>
        public static bool IsServant(int bonusSlot) => bonusSlot == BonusKitRules.SummonBonusSlot;
    }
}