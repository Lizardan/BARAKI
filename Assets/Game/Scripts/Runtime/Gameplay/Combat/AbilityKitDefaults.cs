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
                "Пока герой жив, армия владельца рядом с ним наносит больше урона.",
                7,
                Aura(AuraStat.Damage),
                percent: HeroAbilityRules.AuraDamageBonusPercent,
                radius: HeroAbilityRules.AuraRadius,
                fx: AbilityFx.Ring(AbilityFxColors.AuraDamage)),
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
                "Пока герой жив, армия владельца рядом с ним атакует быстрее.",
                7,
                Aura(AuraStat.AttackSpeed),
                percent: HeroAbilityRules.AuraAttackSpeedBonusPercent,
                radius: HeroAbilityRules.AuraRadius,
                fx: AbilityFx.Ring(AbilityFxColors.AuraAttackSpeed)),
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
                "Пока герой жив, армия владельца рядом с ним получает больше брони.",
                7,
                Aura(AuraStat.Armor),
                percent: HeroAbilityRules.AuraArmorBonusPercent,
                radius: HeroAbilityRules.AuraRadius,
                fx: AbilityFx.Ring(AbilityFxColors.AuraArmor)),
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
                "Пока титан жив, армия владельца рядом с ним крепче (больше запаса здоровья).",
                7,
                Aura(AuraStat.MaxHp),
                percent: HeroAbilityRules.AuraMaxHpBonusPercent,
                radius: HeroAbilityRules.AuraRadius,
                fx: AbilityFx.Ring(AbilityFxColors.AuraMaxHp)),
        };

        /// <summary>Siege unit bonus: passive HP regen aura (AbilityId 50).</summary>
        public static UnitAbilityDef[] CreateSiegeRegen() => new[]
        {
            Passive(
                AbilityIds.AuraHpRegen,
                "Siege Regen Aura",
                "Пока носитель жив, союзники рядом восстанавливают здоровье.",
                0,
                Aura(AuraStat.HpRegen),
                unlock: AbilityUnlock.Always,
                flatBonus: HumanBonusUnitRules.RegenAuraFlatBonus,
                radius: HeroAbilityRules.AuraRadius,
                fx: AbilityFx.Ring(AbilityFxColors.AuraHpRegen)),
        };

        /// <summary>Melee unit bonus: on-hit cleave (AbilityId 51).</summary>
        public static UnitAbilityDef[] CreateMeleeBonus() => new[]
        {
            Passive(
                AbilityIds.MeleeCleave,
                "Cleave",
                "При ударе 15% шанс: урон удара по врагам в радиусе 2 (цель не дублируется).",
                0,
                Trait(),
                unlock: AbilityUnlock.Always,
                percent: HumanBonusUnitRules.OnHitProcChance,
                radius: HumanBonusUnitRules.MeleeAoeRadius,
                fx: AbilityFx.Ring(AbilityFxColors.Strike, 0.55f)),
        };

        /// <summary>Ranged unit bonus: on-hit crit (AbilityId 52).</summary>
        public static UnitAbilityDef[] CreateRangedBonus() => new[]
        {
            Passive(
                AbilityIds.RangedCrit,
                "Deadeye",
                "При попадании 15% шанс: урон выстрела ×2.",
                0,
                Trait(),
                unlock: AbilityUnlock.Always,
                percent: HumanBonusUnitRules.OnHitProcChance,
                damage: HumanBonusUnitRules.RangedCritMultiplier,
                fx: AbilityFx.Burst(AbilityFxColors.Strike)),
        };

        /// <summary>Caster unit bonus: spell kit + hybrid melee under 2 m (AbilityId 53).</summary>
        public static UnitAbilityDef[] CreateCasterBonus()
        {
            var spells = CreateCaster();
            var hybrid = Passive(
                AbilityIds.CasterHybrid,
                "Battlemace",
                "Ближе 2 м бьёт булавой (8–10 × MeleeDamageLevel), иначе обычная ranged-атака. Спеллы кастера сохраняются.",
                0,
                Trait(),
                unlock: AbilityUnlock.Always,
                radius: HumanBonusUnitRules.HybridMeleeRange,
                damage: HumanBonusUnitRules.HybridMeleeDamageMax,
                fx: AbilityFx.Ring(AbilityFxColors.Paladin, 0.45f));
            var kit = new UnitAbilityDef[spells.Length + 1];
            System.Array.Copy(spells, kit, spells.Length);
            kit[spells.Length] = hybrid;
            return kit;
        }

        /// <summary>Flying unit bonus: on-death spawn (AbilityId 54).</summary>
        public static UnitAbilityDef[] CreateFlyingBonus() => new[]
        {
            Passive(
                AbilityIds.FlyingSpawn,
                "Last Call",
                "При гибели 25% шанс призвать базового ranged на месте смерти.",
                0,
                Trait(),
                unlock: AbilityUnlock.Always,
                percent: HumanBonusUnitRules.OnDeathSpawnChance,
                fx: AbilityFx.Plus(AbilityFxColors.Resurrect)),
        };

        /// <summary>Super unit bonus: catapult parabola + splash (AbilityId 55).</summary>
        public static UnitAbilityDef[] CreateSuperBonus() => new[]
        {
            Passive(
                AbilityIds.SuperCatapult,
                "Catapult",
                "Параболический снаряд: splash 50% урона в радиусе 3 от точки прилёта.",
                0,
                Trait(),
                unlock: AbilityUnlock.Always,
                percent: HumanBonusUnitRules.CatapultAoeDamagePercent,
                radius: HumanBonusUnitRules.CatapultAoeRadius,
                fx: AbilityFx.RingBurst(AbilityFxColors.Ultimate, ringDuration: 0.7f, burstHeight: 1.6f)),
        };

        /// <summary>Bonus-unit kit for a role (slots 1–6). Empty for non-bonus roles.</summary>
        public static UnitAbilityDef[] CreateBonus(UnitRole role) => role switch
        {
            UnitRole.Melee => CreateMeleeBonus(),
            UnitRole.Ranged => CreateRangedBonus(),
            UnitRole.Caster => CreateCasterBonus(),
            UnitRole.Siege => CreateSiegeRegen(),
            UnitRole.Flying => CreateFlyingBonus(),
            UnitRole.Super => CreateSuperBonus(),
            _ => System.Array.Empty<UnitAbilityDef>(),
        };

        /// <summary>Prefab / spawn kit: bonus slot wins over base role kit.</summary>
        public static UnitAbilityDef[] CreateForSpawn(UnitRole role, int heroSlot, int bonusSlot)
        {
            if (HumanBonusUnitRules.IsBonusSlot(bonusSlot)
                && HumanBonusUnitRules.RoleForBonusSlot(bonusSlot) == role)
            {
                return CreateBonus(role);
            }

            return Create(role, heroSlot);
        }

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
            float percent = 0f,
            float radius = 0f,
            AbilityFx fx = default,
            AbilityUnlock unlock = AbilityUnlock.HeroLevel,
            float flatBonus = 0f,
            float damage = 0f) =>
            UnitAbilityDef.Create(
                abilityId,
                name,
                description,
                AbilityKind.Passive,
                unlock,
                unlockValue,
                behaviour,
                fx,
                damage: damage,
                percent: percent,
                radius: radius,
                flatBonus: flatBonus);

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

        static PassiveTraitBehaviour Trait() => ScriptableObject.CreateInstance<PassiveTraitBehaviour>();
    }
}
