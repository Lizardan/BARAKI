using Game.Gameplay.Combat;
using Game.Gameplay.Data;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Default kits used when a prefab has no <see cref="UnitAbilityKit"/> (EditMode tests)
    /// and as the seed written onto hero/caster/titan prefabs.
    /// Slot order = AI cast priority (passives are skipped).
    /// </summary>
    public static class AbilityKitDefaults
    {
        public static UnitAbilitySlot[] Create(UnitRole role, int heroSlot)
        {
            if (role == UnitRole.Titan)
            {
                return CreateTitan();
            }

            if (role == UnitRole.Caster)
            {
                return CreateCaster();
            }

            if (role == UnitRole.Hero)
            {
                return heroSlot switch
                {
                    HeroAbilityRules.PaladinSlot => CreatePaladin(),
                    HeroAbilityRules.PriestSlot => CreatePriest(),
                    _ => CreateKing(),
                };
            }

            return System.Array.Empty<UnitAbilitySlot>();
        }

        public static UnitAbilitySlot[] CreateKing() => new[]
        {
            Active(
                AbilityType.Heal,
                4,
                "Heal",
                "Лечение героя и союзников вокруг.",
                heal: HeroAbilityRules.HealAmount,
                radius: HeroAbilityRules.HealRadius,
                cooldownSeconds: HeroAbilityRules.HealCooldownSeconds),
            Active(
                AbilityType.Ultimate,
                10,
                "Ultimate",
                "Большой удар вокруг героя и краткое усиление собственного урона.",
                damage: HeroAbilityRules.UltimateDamage,
                radius: HeroAbilityRules.UltimateRadius,
                cooldownSeconds: HeroAbilityRules.UltimateCooldownSeconds,
                durationSeconds: HeroAbilityRules.UltimateSelfBuffSeconds,
                percent: HeroAbilityRules.UltimateSelfDamageBonusPercent),
            Active(
                AbilityType.Strike,
                1,
                "Strike",
                "Урон по всем врагам вокруг героя.",
                damage: HeroAbilityRules.StrikeDamage,
                radius: HeroAbilityRules.StrikeRadius,
                cooldownSeconds: HeroAbilityRules.StrikeCooldownSeconds),
            Passive(
                AbilityType.AuraDamagePercent,
                7,
                "Aura",
                "Пока герой жив, армия владельца наносит больше урона.",
                percent: HeroAbilityRules.AuraDamageBonusPercent),
        };

        public static UnitAbilitySlot[] CreatePaladin() => new[]
        {
            Active(
                AbilityType.Shield,
                4,
                "Shield",
                "Щит: броня герою и союзникам рядом на короткое время. Кастуется, если рядом есть враг.",
                radius: HeroAbilityRules.ShieldRadius,
                cooldownSeconds: HeroAbilityRules.ShieldCooldownSeconds,
                durationSeconds: HeroAbilityRules.ShieldDurationSeconds,
                flatBonus: HeroAbilityRules.ShieldArmorBonus),
            Active(
                AbilityType.Consecration,
                10,
                "Consecration",
                "Освящение: урон и краткое оглушение врагов вокруг.",
                damage: HeroAbilityRules.ConsecrationDamage,
                radius: HeroAbilityRules.ConsecrationRadius,
                cooldownSeconds: HeroAbilityRules.ConsecrationCooldownSeconds,
                stunSeconds: HeroAbilityRules.ConsecrationStunSeconds),
            Active(
                AbilityType.Smite,
                1,
                "Smite",
                "Кара: высокий урон по ближайшему врагу.",
                damage: HeroAbilityRules.SmiteDamage,
                radius: HeroAbilityRules.SmiteRadius,
                cooldownSeconds: HeroAbilityRules.SmiteCooldownSeconds),
            Passive(
                AbilityType.AuraAttackSpeedPercent,
                7,
                "Aura",
                "Пока герой жив, армия владельца атакует быстрее.",
                percent: HeroAbilityRules.AuraAttackSpeedBonusPercent),
        };

        public static UnitAbilitySlot[] CreatePriest() => new[]
        {
            Active(
                AbilityType.GreaterHeal,
                4,
                "Greater Heal",
                "Создаёт большую зону, в которой союзники лечатся, пока стоят внутри.",
                healPerSecond: HeroAbilityRules.GreaterHealHealPerSecond,
                radius: HeroAbilityRules.GreaterHealRadius,
                cooldownSeconds: HeroAbilityRules.GreaterHealCooldownSeconds,
                durationSeconds: HeroAbilityRules.GreaterHealDurationSeconds),
            Active(
                AbilityType.Revive,
                10,
                "Revive",
                "Возрождает ближайший союзный труп и лечит союзников вокруг.",
                heal: HeroAbilityRules.ReviveHealAmount,
                radius: HeroAbilityRules.ReviveRadius,
                cooldownSeconds: HeroAbilityRules.ReviveCooldownSeconds,
                secondaryRadius: HeroAbilityRules.ReviveHealRadius,
                secondaryHeal: HeroAbilityRules.ReviveHealAmount),
            Active(
                AbilityType.HolyNova,
                1,
                "Holy Nova",
                "Вспышка вокруг выбранного союзника: лечит своих и бьёт врагов рядом с ним.",
                damage: HeroAbilityRules.NovaDamage,
                heal: HeroAbilityRules.NovaHealAmount,
                radius: HeroAbilityRules.NovaRadius,
                castRange: HeroAbilityRules.NovaCastRange,
                cooldownSeconds: HeroAbilityRules.NovaCooldownSeconds),
            Passive(
                AbilityType.AuraArmorPercent,
                7,
                "Aura",
                "Пока герой жив, армия владельца получает больше брони.",
                percent: HeroAbilityRules.AuraArmorBonusPercent),
        };

        public static UnitAbilitySlot[] CreateTitan() => new[]
        {
            Active(
                AbilityType.Rally,
                4,
                "Rally",
                "Клич: герой и союзники рядом получают броню.",
                radius: HeroAbilityRules.RallyRadius,
                cooldownSeconds: HeroAbilityRules.RallyCooldownSeconds,
                durationSeconds: HeroAbilityRules.RallyDurationSeconds,
                flatBonus: HeroAbilityRules.RallyArmorBonus),
            Active(
                AbilityType.Stomp,
                10,
                "Stomp",
                "Топот: урон и оглушение врагов вокруг.",
                damage: HeroAbilityRules.StompDamage,
                radius: HeroAbilityRules.StompRadius,
                cooldownSeconds: HeroAbilityRules.StompCooldownSeconds,
                stunSeconds: HeroAbilityRules.StompStunSeconds),
            Active(
                AbilityType.Slam,
                1,
                "Slam",
                "Мощный удар по всем врагам вокруг.",
                damage: HeroAbilityRules.SlamDamage,
                radius: HeroAbilityRules.SlamRadius,
                cooldownSeconds: HeroAbilityRules.SlamCooldownSeconds),
            Passive(
                AbilityType.AuraMaxHpPercent,
                7,
                "Colossus",
                "Пока титан жив, армия владельца крепче (больше запаса здоровья).",
                percent: HeroAbilityRules.AuraMaxHpBonusPercent),
        };

        public static UnitAbilitySlot[] CreateCaster() => new[]
        {
            UnitAbilitySlot.Create(
                AbilityType.CasterHeal,
                AbilityKind.Active,
                AbilityUnlock.MagicLevel,
                CasterSpellRules.HealRequiredMagicLevel,
                "Heal",
                "Лечит самого раненого союзника в радиусе каста.",
                heal: CasterSpellRules.HealAmount,
                castRange: CasterSpellRules.CastRange,
                cooldownSeconds: CasterSpellRules.HealCooldownSeconds,
                manaCost: CasterSpellRules.HealManaCost),
            UnitAbilitySlot.Create(
                AbilityType.Frost,
                AbilityKind.Active,
                AbilityUnlock.MagicLevel,
                CasterSpellRules.FrostRequiredMagicLevel,
                "Frost",
                "Ледяной взрыв по скоплению врагов: урон и краткая заморозка.",
                damage: CasterSpellRules.FrostDamage,
                radius: CasterSpellRules.FrostRadius,
                castRange: CasterSpellRules.CastRange,
                cooldownSeconds: CasterSpellRules.FrostCooldownSeconds,
                manaCost: CasterSpellRules.FrostManaCost,
                stunSeconds: CasterSpellRules.FrostFreezeSeconds),
            UnitAbilitySlot.Create(
                AbilityType.Resurrect,
                AbilityKind.Active,
                AbilityUnlock.MagicLevel,
                CasterSpellRules.ResurrectRequiredMagicLevel,
                "Resurrect",
                "Поднимает недавний союзный труп с полным здоровьем.",
                castRange: CasterSpellRules.CastRange,
                cooldownSeconds: CasterSpellRules.ResurrectCooldownSeconds,
                durationSeconds: CasterSpellRules.ResurrectCorpseMaxAgeSeconds,
                manaCost: CasterSpellRules.ResurrectManaCost),
        };

        static UnitAbilitySlot Active(
            AbilityType type,
            int heroLevel,
            string name,
            string description,
            float damage = 0f,
            float heal = 0f,
            float healPerSecond = 0f,
            float radius = 0f,
            float castRange = 0f,
            float cooldownSeconds = 0f,
            float durationSeconds = 0f,
            float percent = 0f,
            float stunSeconds = 0f,
            float flatBonus = 0f,
            float secondaryRadius = 0f,
            float secondaryHeal = 0f) =>
            UnitAbilitySlot.Create(
                type,
                AbilityKind.Active,
                AbilityUnlock.HeroLevel,
                heroLevel,
                name,
                description,
                damage,
                heal,
                healPerSecond,
                radius,
                castRange,
                cooldownSeconds,
                durationSeconds,
                percent,
                stunSeconds: stunSeconds,
                flatBonus: flatBonus,
                secondaryRadius: secondaryRadius,
                secondaryHeal: secondaryHeal);

        static UnitAbilitySlot Passive(
            AbilityType type,
            int heroLevel,
            string name,
            string description,
            float percent) =>
            UnitAbilitySlot.Create(
                type,
                AbilityKind.Passive,
                AbilityUnlock.HeroLevel,
                heroLevel,
                name,
                description,
                percent: percent);
    }
}
