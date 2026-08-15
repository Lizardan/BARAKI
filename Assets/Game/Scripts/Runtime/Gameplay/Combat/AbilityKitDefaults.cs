using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Default kits used when a prefab has no abilities in <see cref="UnitCombatSettings"/> (EditMode tests)
    /// and as the seed written onto hero/caster/titan prefabs.
    /// Slot order = AI cast priority (passives are skipped).
    /// Tuning constants still come from <see cref="HeroAbilityRules"/> / <see cref="CasterSpellRules"/>
    /// as zero-value fallbacks; the editor builder bakes real numbers into the def assets.
    /// </summary>
    public static class AbilityKitDefaults
    {
        public static UnitAbilityDef[] Create(UnitRole role, int heroSlot)
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

            return System.Array.Empty<UnitAbilityDef>();
        }

        public static UnitAbilityDef[] CreateKing() => new[]
        {
            Active(
                AbilityIds.Heal,
                "Group Heal",
                "Лечение героя и союзников вокруг.",
                4,
                HealArea(),
                AbilityFx.RingPlus(AbilityFxColors.Heal, 0.85f),
                heal: HeroAbilityRules.HealAmount,
                radius: HeroAbilityRules.HealRadius,
                cooldownSeconds: HeroAbilityRules.HealCooldownSeconds),
            Active(
                AbilityIds.Ultimate,
                "Ultimate",
                "Большой удар вокруг героя и краткое усиление собственного урона.",
                10,
                GroundAoe(applyUltimateSelfBuff: true),
                AbilityFx.RingPlus(AbilityFxColors.Ultimate, 1.2f),
                damage: HeroAbilityRules.UltimateDamage,
                radius: HeroAbilityRules.UltimateRadius,
                cooldownSeconds: HeroAbilityRules.UltimateCooldownSeconds,
                durationSeconds: HeroAbilityRules.UltimateSelfBuffSeconds,
                percent: HeroAbilityRules.UltimateSelfDamageBonusPercent),
            Active(
                AbilityIds.Strike,
                "Strike",
                "Урон по всем врагам вокруг героя.",
                1,
                GroundAoe(),
                AbilityFx.Ring(AbilityFxColors.Strike, 0.75f),
                damage: HeroAbilityRules.StrikeDamage,
                radius: HeroAbilityRules.StrikeRadius,
                cooldownSeconds: HeroAbilityRules.StrikeCooldownSeconds),
            Passive(
                AbilityIds.AuraDamagePercent,
                "Attack Aura",
                "Пока герой жив, армия владельца наносит больше урона.",
                7,
                Aura(AuraStat.Damage),
                percent: HeroAbilityRules.AuraDamageBonusPercent),
        };

        public static UnitAbilityDef[] CreatePaladin() => new[]
        {
            Active(
                AbilityIds.Shield,
                "Shield",
                "Щит: броня герою и союзникам рядом на короткое время. Кастуется, если рядом есть враг.",
                4,
                ArmorShout(),
                AbilityFx.Ring(AbilityFxColors.Paladin, 1.0f),
                radius: HeroAbilityRules.ShieldRadius,
                cooldownSeconds: HeroAbilityRules.ShieldCooldownSeconds,
                durationSeconds: HeroAbilityRules.ShieldDurationSeconds,
                flatBonus: HeroAbilityRules.ShieldArmorBonus),
            Active(
                AbilityIds.Consecration,
                "Consecration",
                "Освящение: урон и краткое оглушение врагов вокруг.",
                10,
                GroundAoe(),
                AbilityFx.RingBurst(AbilityFxColors.Paladin, ringDuration: 1.3f, burstHeight: 2.8f),
                damage: HeroAbilityRules.ConsecrationDamage,
                radius: HeroAbilityRules.ConsecrationRadius,
                cooldownSeconds: HeroAbilityRules.ConsecrationCooldownSeconds,
                stunSeconds: HeroAbilityRules.ConsecrationStunSeconds),
            Active(
                AbilityIds.Smite,
                "Smite",
                "Кара: высокий урон по ближайшему врагу.",
                1,
                DamageBurst(),
                AbilityFx.Burst(AbilityFxColors.Paladin),
                damage: HeroAbilityRules.SmiteDamage,
                radius: HeroAbilityRules.SmiteRadius,
                cooldownSeconds: HeroAbilityRules.SmiteCooldownSeconds),
            Passive(
                AbilityIds.AuraAttackSpeedPercent,
                "Haste Aura",
                "Пока герой жив, армия владельца атакует быстрее.",
                7,
                Aura(AuraStat.AttackSpeed),
                percent: HeroAbilityRules.AuraAttackSpeedBonusPercent),
        };

        public static UnitAbilityDef[] CreatePriest() => new[]
        {
            Active(
                AbilityIds.GreaterHeal,
                "Greater Heal",
                "Создаёт большую зону, в которой союзники лечатся, пока стоят внутри.",
                4,
                GreaterHealZone(),
                AbilityFx.RingPlus(AbilityFxColors.Priest, HeroAbilityRules.GreaterHealDurationSeconds),
                healPerSecond: HeroAbilityRules.GreaterHealHealPerSecond,
                radius: HeroAbilityRules.GreaterHealRadius,
                cooldownSeconds: HeroAbilityRules.GreaterHealCooldownSeconds,
                durationSeconds: HeroAbilityRules.GreaterHealDurationSeconds),
            Active(
                AbilityIds.Revive,
                "Revive",
                "Возрождает ближайший союзный труп и лечит союзников вокруг.",
                10,
                Revive(),
                AbilityFx.RingPlus(AbilityFxColors.Priest, 1.1f),
                heal: HeroAbilityRules.ReviveHealAmount,
                radius: HeroAbilityRules.ReviveRadius,
                cooldownSeconds: HeroAbilityRules.ReviveCooldownSeconds,
                secondaryRadius: HeroAbilityRules.ReviveHealRadius,
                secondaryHeal: HeroAbilityRules.ReviveHealAmount),
            Active(
                AbilityIds.HolyNova,
                "Holy Nova",
                "Вспышка вокруг выбранного союзника: лечит своих и бьёт врагов рядом с ним.",
                1,
                Nova(),
                AbilityFx.RingPlus(AbilityFxColors.Priest, 0.9f),
                damage: HeroAbilityRules.NovaDamage,
                heal: HeroAbilityRules.NovaHealAmount,
                radius: HeroAbilityRules.NovaRadius,
                castRange: HeroAbilityRules.NovaCastRange,
                cooldownSeconds: HeroAbilityRules.NovaCooldownSeconds),
            Passive(
                AbilityIds.AuraArmorPercent,
                "Iron Aura",
                "Пока герой жив, армия владельца получает больше брони.",
                7,
                Aura(AuraStat.Armor),
                percent: HeroAbilityRules.AuraArmorBonusPercent),
        };

        public static UnitAbilityDef[] CreateTitan() => new[]
        {
            Active(
                AbilityIds.Rally,
                "Rally",
                "Клич: герой и союзники рядом получают броню.",
                4,
                ArmorShout(),
                AbilityFx.Ring(AbilityFxColors.Paladin, 1.0f),
                radius: HeroAbilityRules.RallyRadius,
                cooldownSeconds: HeroAbilityRules.RallyCooldownSeconds,
                durationSeconds: HeroAbilityRules.RallyDurationSeconds,
                flatBonus: HeroAbilityRules.RallyArmorBonus),
            Active(
                AbilityIds.Stomp,
                "Stomp",
                "Топот: урон и оглушение врагов вокруг.",
                10,
                GroundAoe(),
                AbilityFx.RingBurst(AbilityFxColors.Paladin, ringDuration: 1.3f, burstHeight: 2.8f),
                damage: HeroAbilityRules.StompDamage,
                radius: HeroAbilityRules.StompRadius,
                cooldownSeconds: HeroAbilityRules.StompCooldownSeconds,
                stunSeconds: HeroAbilityRules.StompStunSeconds),
            Active(
                AbilityIds.Slam,
                "Slam",
                "Мощный удар по всем врагам вокруг.",
                1,
                GroundAoe(),
                AbilityFx.Ring(AbilityFxColors.Strike, 0.75f),
                damage: HeroAbilityRules.SlamDamage,
                radius: HeroAbilityRules.SlamRadius,
                cooldownSeconds: HeroAbilityRules.SlamCooldownSeconds),
            Passive(
                AbilityIds.AuraMaxHpPercent,
                "Colossus",
                "Пока титан жив, армия владельца крепче (больше запаса здоровья).",
                7,
                Aura(AuraStat.MaxHp),
                percent: HeroAbilityRules.AuraMaxHpBonusPercent),
        };

        public static UnitAbilityDef[] CreateCaster() => new[]
        {
            Active(
                AbilityIds.CasterHeal,
                "Mend",
                "Лечит самого раненого союзника в радиусе каста.",
                CasterSpellRules.HealRequiredMagicLevel,
                HealSingle(),
                AbilityFx.Plus(AbilityFxColors.Heal),
                unlock: AbilityUnlock.MagicLevel,
                heal: CasterSpellRules.HealAmount,
                castRange: CasterSpellRules.CastRange,
                cooldownSeconds: CasterSpellRules.HealCooldownSeconds,
                manaCost: CasterSpellRules.HealManaCost),
            Active(
                AbilityIds.Frost,
                "Frost",
                "Ледяной взрыв по скоплению врагов: урон и краткая заморозка.",
                CasterSpellRules.FrostRequiredMagicLevel,
                GroundAoe(),
                AbilityFx.Ring(AbilityFxColors.Frost, 0.9f),
                unlock: AbilityUnlock.MagicLevel,
                damage: CasterSpellRules.FrostDamage,
                radius: CasterSpellRules.FrostRadius,
                castRange: CasterSpellRules.CastRange,
                cooldownSeconds: CasterSpellRules.FrostCooldownSeconds,
                manaCost: CasterSpellRules.FrostManaCost,
                stunSeconds: CasterSpellRules.FrostFreezeSeconds),
            Active(
                AbilityIds.Resurrect,
                "Resurrect",
                "Поднимает недавний союзный труп с полным здоровьем.",
                CasterSpellRules.ResurrectRequiredMagicLevel,
                ResurrectCorpse(),
                AbilityFx.Plus(AbilityFxColors.Resurrect),
                unlock: AbilityUnlock.MagicLevel,
                castRange: CasterSpellRules.CastRange,
                cooldownSeconds: CasterSpellRules.ResurrectCooldownSeconds,
                durationSeconds: CasterSpellRules.ResurrectCorpseMaxAgeSeconds,
                manaCost: CasterSpellRules.ResurrectManaCost),
        };

        static UnitAbilityDef Active(
            int abilityId,
            string name,
            string description,
            int unlockValue,
            UnitAbilityBehaviour behaviour,
            AbilityFx fx,
            AbilityUnlock unlock = AbilityUnlock.HeroLevel,
            float damage = 0f,
            float heal = 0f,
            float healPerSecond = 0f,
            float radius = 0f,
            float castRange = 0f,
            float cooldownSeconds = 0f,
            float durationSeconds = 0f,
            float percent = 0f,
            float manaCost = 0f,
            float stunSeconds = 0f,
            float flatBonus = 0f,
            float secondaryRadius = 0f,
            float secondaryHeal = 0f) =>
            UnitAbilityDef.Create(
                abilityId,
                name,
                description,
                AbilityKind.Active,
                unlock,
                unlockValue,
                behaviour,
                fx,
                damage,
                heal,
                healPerSecond,
                radius,
                castRange,
                cooldownSeconds,
                durationSeconds,
                percent,
                manaCost,
                stunSeconds,
                flatBonus,
                secondaryRadius,
                secondaryHeal);

        static UnitAbilityDef Passive(
            int abilityId,
            string name,
            string description,
            int unlockValue,
            UnitAbilityBehaviour behaviour,
            float percent) =>
            UnitAbilityDef.Create(
                abilityId,
                name,
                description,
                AbilityKind.Passive,
                AbilityUnlock.HeroLevel,
                unlockValue,
                behaviour,
                percent: percent);

        static HealAreaBehaviour HealArea() => ScriptableObject.CreateInstance<HealAreaBehaviour>();

        static HealSingleBehaviour HealSingle() => ScriptableObject.CreateInstance<HealSingleBehaviour>();

        static GroundAoeBehaviour GroundAoe(bool applyUltimateSelfBuff = false)
        {
            var b = ScriptableObject.CreateInstance<GroundAoeBehaviour>();
            b.Configure(applyUltimateSelfBuff);
            return b;
        }

        static DamageBurstBehaviour DamageBurst()
        {
            var b = ScriptableObject.CreateInstance<DamageBurstBehaviour>();
            b.Configure(singleTarget: true);
            return b;
        }

        static ArmorShoutBehaviour ArmorShout() => ScriptableObject.CreateInstance<ArmorShoutBehaviour>();

        static GreaterHealZoneBehaviour GreaterHealZone() => ScriptableObject.CreateInstance<GreaterHealZoneBehaviour>();

        static ReviveBehaviour Revive() => ScriptableObject.CreateInstance<ReviveBehaviour>();

        static NovaBehaviour Nova() => ScriptableObject.CreateInstance<NovaBehaviour>();

        static ResurrectCorpseBehaviour ResurrectCorpse() => ScriptableObject.CreateInstance<ResurrectCorpseBehaviour>();

        static AuraBehaviour Aura(AuraStat stat)
        {
            var b = ScriptableObject.CreateInstance<AuraBehaviour>();
            b.Configure(stat);
            return b;
        }
    }
}
