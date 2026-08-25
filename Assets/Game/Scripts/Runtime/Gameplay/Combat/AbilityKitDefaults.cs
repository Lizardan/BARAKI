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
                fx: new AbilityFx { Color = AbilityFxColors.Heal },
                heal: HeroAbilityRules.HealAmount,
                radius: HeroAbilityRules.HealRadius,
                cooldownSeconds: HeroAbilityRules.HealCooldownSeconds),
            Active(
                AbilityIds.Ultimate,
                "Ultimate",
                "Большой удар вокруг героя и краткое усиление собственного урона.",
                10,
                GroundAoe(applyUltimateSelfBuff: true),
                fx: new AbilityFx { Color = AbilityFxColors.Ultimate },
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
                fx: new AbilityFx { Color = AbilityFxColors.Strike },
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
                fx: new AbilityFx { Color = AbilityFxColors.AuraDamage }),
        };

        public static UnitAbilityDef[] CreatePaladin() => new[]
        {
            Active(
                AbilityIds.Shield,
                "Shield",
                "Щит: броня герою и союзникам рядом на короткое время. Кастуется, если рядом есть враг.",
                4,
                ArmorShout(),
                fx: new AbilityFx { Color = AbilityFxColors.Paladin },
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
                fx: new AbilityFx { Color = AbilityFxColors.Paladin },
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
                fx: new AbilityFx { Color = AbilityFxColors.Paladin },
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
                fx: new AbilityFx { Color = AbilityFxColors.AuraAttackSpeed }),
        };

        public static UnitAbilityDef[] CreatePriest() => new[]
        {
            Active(
                AbilityIds.GreaterHeal,
                "Greater Heal",
                "Создаёт большую зону, в которой союзники лечатся, пока стоят внутри.",
                4,
                GreaterHealZone(),
                fx: new AbilityFx { Color = AbilityFxColors.Priest },
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
                fx: new AbilityFx { Color = AbilityFxColors.Priest },
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
                fx: new AbilityFx { Color = AbilityFxColors.Priest },
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
                fx: new AbilityFx { Color = AbilityFxColors.AuraArmor }),
        };

        public static UnitAbilityDef[] CreateTitan() => new[]
        {
            Active(
                AbilityIds.Rally,
                "Rally",
                "Клич: герой и союзники рядом получают броню.",
                4,
                ArmorShout(),
                fx: new AbilityFx { Color = AbilityFxColors.Paladin },
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
                fx: new AbilityFx { Color = AbilityFxColors.Paladin },
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
                fx: new AbilityFx { Color = AbilityFxColors.Strike },
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
                fx: new AbilityFx { Color = AbilityFxColors.AuraMaxHp }),
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
                fx: new AbilityFx { Color = AbilityFxColors.AuraHpRegen }),
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
                fx: new AbilityFx { Color = AbilityFxColors.Strike }),
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
                fx: new AbilityFx { Color = AbilityFxColors.Strike }),
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
                fx: new AbilityFx { Color = AbilityFxColors.Paladin });
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
                fx: new AbilityFx { Color = AbilityFxColors.Resurrect }),
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
                fx: new AbilityFx { Color = AbilityFxColors.Ultimate }),
        };

        // --- PRE-006b: veteran champion kits (bonus slots 7–10) ---
        // Same kit as the base hero with ~×1.3–1.4 numbers, a +15% morale aura
        // and one signature ability replacing its base counterpart.

        /// <summary>King veteran (bonus slot 7): Ultimate → King's Command (army-wide shout).</summary>
        public static UnitAbilityDef[] CreateKingBonus() => new[]
        {
            Active(
                AbilityIds.Heal,
                "Group Heal",
                "Лечение героя и союзников вокруг.",
                4,
                HealArea(),
                fx: new AbilityFx { Color = AbilityFxColors.Heal },
                heal: HeroAbilityRules.HealAmount * HumanBonusUnitRules.VeteranDamageMultiplier,
                radius: HeroAbilityRules.HealRadius,
                cooldownSeconds: HeroAbilityRules.HealCooldownSeconds),
            Active(
                AbilityIds.KingsCommand,
                "King's Command",
                "Ветеранская ульта: вся армия владельца наносит больше урона.",
                10,
                ArmyShout(),
                fx: new AbilityFx { Color = AbilityFxColors.Ultimate },
                radius: HeroAbilityRules.UltimateRadius,
                cooldownSeconds: HeroAbilityRules.KingsCommandCooldownSeconds,
                durationSeconds: HeroAbilityRules.KingsCommandSeconds,
                percent: HeroAbilityRules.KingsCommandPercent),
            Active(
                AbilityIds.Strike,
                "Strike",
                "Урон по всем врагам вокруг героя.",
                1,
                GroundAoe(),
                fx: new AbilityFx { Color = AbilityFxColors.Strike },
                damage: HeroAbilityRules.StrikeDamage * HumanBonusUnitRules.VeteranDamageMultiplier,
                radius: HeroAbilityRules.StrikeRadius,
                cooldownSeconds: HeroAbilityRules.StrikeCooldownSeconds),
            Passive(
                AbilityIds.AuraDamagePercent,
                "Attack Aura",
                "Пока герой жив, армия владельца рядом с ним наносит больше урона.",
                7,
                Aura(AuraStat.Damage),
                percent: HeroAbilityRules.VeteranAuraBonusPercent,
                radius: HeroAbilityRules.AuraRadius,
                fx: new AbilityFx { Color = AbilityFxColors.AuraDamage }),
        };

        /// <summary>Paladin veteran (bonus slot 8): Shield → Aegis (armor + absorb shield).</summary>
        public static UnitAbilityDef[] CreatePaladinBonus() => new[]
        {
            Active(
                AbilityIds.Aegis,
                "Aegis",
                "Щит-эгида: броня и щит герою и союзникам рядом на короткое время. Кастуется, если рядом есть враг.",
                4,
                Aegis(),
                fx: new AbilityFx { Color = AbilityFxColors.Paladin },
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
                fx: new AbilityFx { Color = AbilityFxColors.Paladin },
                damage: HeroAbilityRules.ConsecrationDamage * HumanBonusUnitRules.VeteranDamageMultiplier,
                radius: HeroAbilityRules.ConsecrationRadius,
                cooldownSeconds: HeroAbilityRules.ConsecrationCooldownSeconds,
                stunSeconds: HeroAbilityRules.ConsecrationStunSeconds),
            Active(
                AbilityIds.Smite,
                "Smite",
                "Кара: высокий урон по ближайшему врагу.",
                1,
                DamageBurst(),
                fx: new AbilityFx { Color = AbilityFxColors.Paladin },
                damage: HeroAbilityRules.SmiteDamage * HumanBonusUnitRules.VeteranDamageMultiplier,
                radius: HeroAbilityRules.SmiteRadius,
                cooldownSeconds: HeroAbilityRules.SmiteCooldownSeconds),
            Passive(
                AbilityIds.AuraAttackSpeedPercent,
                "Haste Aura",
                "Пока герой жив, армия владельца рядом с ним атакует быстрее.",
                7,
                Aura(AuraStat.AttackSpeed),
                percent: HeroAbilityRules.VeteranAuraBonusPercent,
                radius: HeroAbilityRules.AuraRadius,
                fx: new AbilityFx { Color = AbilityFxColors.AuraAttackSpeed }),
        };

        /// <summary>Priest veteran (bonus slot 9): Greater Heal → Sanctuary (following heal zone).</summary>
        public static UnitAbilityDef[] CreatePriestBonus() => new[]
        {
            Active(
                AbilityIds.Sanctuary,
                "Sanctuary",
                "Создаёт святилище, которое следует за жрецом и лечит союзников внутри.",
                4,
                SanctuaryZone(),
                fx: new AbilityFx { Color = AbilityFxColors.Priest },
                healPerSecond: HeroAbilityRules.GreaterHealHealPerSecond * HumanBonusUnitRules.VeteranDamageMultiplier,
                radius: HeroAbilityRules.GreaterHealRadius,
                cooldownSeconds: HeroAbilityRules.GreaterHealCooldownSeconds,
                durationSeconds: HeroAbilityRules.GreaterHealDurationSeconds),
            Active(
                AbilityIds.Revive,
                "Revive",
                "Возрождает ближайший союзный труп и лечит союзников вокруг.",
                10,
                Revive(),
                fx: new AbilityFx { Color = AbilityFxColors.Priest },
                heal: HeroAbilityRules.ReviveHealAmount * HumanBonusUnitRules.VeteranDamageMultiplier,
                radius: HeroAbilityRules.ReviveRadius,
                cooldownSeconds: HeroAbilityRules.ReviveCooldownSeconds,
                secondaryRadius: HeroAbilityRules.ReviveHealRadius,
                secondaryHeal: HeroAbilityRules.ReviveHealAmount * HumanBonusUnitRules.VeteranDamageMultiplier),
            Active(
                AbilityIds.HolyNova,
                "Holy Nova",
                "Вспышка вокруг выбранного союзника: лечит своих и бьёт врагов рядом с ним.",
                1,
                Nova(),
                fx: new AbilityFx { Color = AbilityFxColors.Priest },
                damage: HeroAbilityRules.NovaDamage * HumanBonusUnitRules.VeteranDamageMultiplier,
                heal: HeroAbilityRules.NovaHealAmount * HumanBonusUnitRules.VeteranDamageMultiplier,
                radius: HeroAbilityRules.NovaRadius,
                castRange: HeroAbilityRules.NovaCastRange,
                cooldownSeconds: HeroAbilityRules.NovaCooldownSeconds),
            Passive(
                AbilityIds.AuraArmorPercent,
                "Iron Aura",
                "Пока герой жив, армия владельца рядом с ним получает больше брони.",
                7,
                Aura(AuraStat.Armor),
                percent: HeroAbilityRules.VeteranAuraBonusPercent,
                radius: HeroAbilityRules.AuraRadius,
                fx: new AbilityFx { Color = AbilityFxColors.AuraArmor }),
        };

        /// <summary>Titan veteran (bonus slot 10): Colossus → Greater Colossus (+25% max HP army).</summary>
        public static UnitAbilityDef[] CreateTitanBonus() => new[]
        {
            Active(
                AbilityIds.Rally,
                "Rally",
                "Клич: герой и союзники рядом получают броню.",
                4,
                ArmorShout(),
                fx: new AbilityFx { Color = AbilityFxColors.Paladin },
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
                fx: new AbilityFx { Color = AbilityFxColors.Paladin },
                damage: HeroAbilityRules.StompDamage * HumanBonusUnitRules.VeteranDamageMultiplier,
                radius: HeroAbilityRules.StompRadius,
                cooldownSeconds: HeroAbilityRules.StompCooldownSeconds,
                stunSeconds: HeroAbilityRules.StompStunSeconds),
            Active(
                AbilityIds.Slam,
                "Slam",
                "Мощный удар по всем врагам вокруг.",
                1,
                GroundAoe(),
                fx: new AbilityFx { Color = AbilityFxColors.Strike },
                damage: HeroAbilityRules.SlamDamage * HumanBonusUnitRules.VeteranDamageMultiplier,
                radius: HeroAbilityRules.SlamRadius,
                cooldownSeconds: HeroAbilityRules.SlamCooldownSeconds),
            Passive(
                AbilityIds.GreaterColossus,
                "Greater Colossus",
                "Пока титан жив, армия владельца рядом с ним крепче (больше запаса здоровья).",
                7,
                Aura(AuraStat.MaxHp),
                percent: HeroAbilityRules.AuraMaxHpVeteranBonusPercent,
                radius: HeroAbilityRules.AuraRadius,
                fx: new AbilityFx { Color = AbilityFxColors.AuraMaxHp }),
        };

        /// <summary>Veteran kit for bonus slot 7–9 (hero) or 10 (titan). Empty otherwise.</summary>
        public static UnitAbilityDef[] CreateVeteranKit(int bonusSlot) => bonusSlot switch
        {
            7 => CreateKingBonus(),
            8 => CreatePaladinBonus(),
            9 => CreatePriestBonus(),
            10 => CreateTitanBonus(),
            _ => System.Array.Empty<UnitAbilityDef>(),
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
            if (HumanBonusUnitRules.MatchesUnit(bonusSlot, role, heroSlot))
            {
                if (HumanBonusUnitRules.IsChampionBonusSlot(bonusSlot))
                {
                    return CreateVeteranKit(bonusSlot);
                }

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
                fx: new AbilityFx { Color = AbilityFxColors.Heal },
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
                fx: new AbilityFx { Color = AbilityFxColors.Frost },
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
                fx: new AbilityFx { Color = AbilityFxColors.Resurrect },
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

        static ArmyDamageShoutBehaviour ArmyShout() => ScriptableObject.CreateInstance<ArmyDamageShoutBehaviour>();

        static AegisBehaviour Aegis() => ScriptableObject.CreateInstance<AegisBehaviour>();

        static SanctuaryZoneBehaviour SanctuaryZone() => ScriptableObject.CreateInstance<SanctuaryZoneBehaviour>();

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
