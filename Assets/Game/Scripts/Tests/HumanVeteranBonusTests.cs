using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>PRE-006b: veteran champion bonus slots 7–10 and Human race uniques 11–12.</summary>
    public sealed class HumanVeteranBonusTests
    {
        // ------------------------------------------------------------------ slot mapping

        [Test]
        public void VeteranSlotMapping_MatchesOnlyOwnChampion()
        {
            Assert.AreEqual(7, HumanBonusUnitRules.EffectiveBonusSlotForHero(7, 1));
            Assert.AreEqual(0, HumanBonusUnitRules.EffectiveBonusSlotForHero(7, 2));
            Assert.AreEqual(9, HumanBonusUnitRules.EffectiveBonusSlotForHero(9, 3));
            Assert.AreEqual(0, HumanBonusUnitRules.EffectiveBonusSlotForHero(10, 1));
            Assert.AreEqual(10, HumanBonusUnitRules.EffectiveBonusSlotForTitan(10));
            Assert.AreEqual(0, HumanBonusUnitRules.EffectiveBonusSlotForTitan(7));

            Assert.IsTrue(HumanBonusUnitRules.MatchesUnit(7, UnitRole.Hero, 1));
            Assert.IsFalse(HumanBonusUnitRules.MatchesUnit(7, UnitRole.Hero, 2));
            Assert.IsTrue(HumanBonusUnitRules.MatchesUnit(10, UnitRole.Titan, 0));
            Assert.IsFalse(HumanBonusUnitRules.MatchesUnit(10, UnitRole.Hero, 1));
            Assert.IsFalse(HumanBonusUnitRules.MatchesUnit(11, UnitRole.Melee, 0));

            Assert.IsTrue(HumanBonusUnitRules.IsChampionBonusSlot(7));
            Assert.IsTrue(HumanBonusUnitRules.IsTitanBonusSlot(10));
            Assert.IsTrue(HumanBonusUnitRules.IsRaceUniqueSlot(11));
            Assert.IsTrue(HumanBonusUnitRules.IsRaceUniqueSlot(12));
            Assert.IsFalse(HumanBonusUnitRules.IsChampionBonusSlot(6));
            Assert.IsFalse(HumanBonusUnitRules.IsRaceUniqueSlot(10));
        }

        // ------------------------------------------------------------------ kits

        [Test]
        public void VeteranKits_SignatureReplacesBaseAbility()
        {
            CollectionAssert.AreEquivalent(
                new[] { AbilityIds.Heal, AbilityIds.KingsCommand, AbilityIds.Strike, AbilityIds.AuraDamagePercent },
                Ids(AbilityKitDefaults.CreateKingBonus()));
            CollectionAssert.AreEquivalent(
                new[] { AbilityIds.Aegis, AbilityIds.Consecration, AbilityIds.Smite, AbilityIds.AuraAttackSpeedPercent },
                Ids(AbilityKitDefaults.CreatePaladinBonus()));
            CollectionAssert.AreEquivalent(
                new[] { AbilityIds.Sanctuary, AbilityIds.Revive, AbilityIds.HolyNova, AbilityIds.AuraArmorPercent },
                Ids(AbilityKitDefaults.CreatePriestBonus()));
            CollectionAssert.AreEquivalent(
                new[] { AbilityIds.Rally, AbilityIds.Stomp, AbilityIds.Slam, AbilityIds.GreaterColossus },
                Ids(AbilityKitDefaults.CreateTitanBonus()));
        }

        [Test]
        public void VeteranKits_DisplayNamesAreUniqueAcrossAllKits()
        {
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (var kit in new[]
                     {
                         AbilityKitDefaults.CreateKingBonus(),
                         AbilityKitDefaults.CreatePaladinBonus(),
                         AbilityKitDefaults.CreatePriestBonus(),
                         AbilityKitDefaults.CreateTitanBonus(),
                     })
            {
                foreach (var def in kit)
                {
                    Assert.IsTrue(names.Add(def.DisplayName), $"duplicate display name {def.DisplayName}");
                }
            }
        }

        [Test]
        public void CreateForSpawn_ResolvesVeteranKitBySlot()
        {
            var kit = AbilityKitDefaults.CreateForSpawn(UnitRole.Hero, 1, 7);
            Assert.Contains(AbilityIds.KingsCommand, Ids(kit));

            var titanKit = AbilityKitDefaults.CreateForSpawn(UnitRole.Titan, 0, 10);
            Assert.Contains(AbilityIds.GreaterColossus, Ids(titanKit));

            var baseKit = AbilityKitDefaults.CreateForSpawn(UnitRole.Hero, 1, 0);
            Assert.Contains(AbilityIds.Ultimate, Ids(baseKit));
        }

        // ------------------------------------------------------------------ stats

        [Test]
        public void VeteranStats_MultipliersAppliedToFallback()
        {
            var stats = UnitStatsResolver.ResolveBase(
                null, null, GameIds.Races.Human, UnitRole.Hero, heroSlot: 1, bonusSlot: 7);
            Assert.AreEqual(600f * HumanBonusUnitRules.VeteranHpMultiplier, stats.MaxHp, 0.01f);
            Assert.AreEqual(4f + HumanBonusUnitRules.VeteranArmorBonus, stats.Armor, 0.01f);
            Assert.AreEqual(35f * HumanBonusUnitRules.VeteranDamageMultiplier, stats.DamageMin, 0.01f);
            Assert.AreEqual(45f * HumanBonusUnitRules.VeteranDamageMultiplier, stats.DamageMax, 0.01f);
        }

        [Test]
        public void VeteranTitanStats_StackOnTitanScale()
        {
            var baseTitan = UnitStatsResolver.ResolveBase(
                null, null, GameIds.Races.Human, UnitRole.Titan, heroSlot: 0, bonusSlot: 0);
            var veteran = UnitStatsResolver.ResolveBase(
                null, null, GameIds.Races.Human, UnitRole.Titan, heroSlot: 0, bonusSlot: 10);
            Assert.AreEqual(baseTitan.MaxHp * HumanBonusUnitRules.VeteranHpMultiplier, veteran.MaxHp, 0.01f);
            Assert.AreEqual(baseTitan.DamageMax * HumanBonusUnitRules.VeteranDamageMultiplier, veteran.DamageMax, 0.01f);
        }

        // ------------------------------------------------------------------ King's Command

        [Test]
        public void KingsCommand_BuffsWholeArmy()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 1, level: 10, bonusSlot: 7);
            var nearAlly = SpawnAlly(controller, 22f);
            var farAlly = SpawnAlly(controller, 40f);
            var enemy = SpawnEnemy(controller, hero.WorldPosition + new Vector3(3f, 0f, 0f));

            controller.Combat.Tick(0.1f);

            Assert.Greater(hero.UltimateBuffRemaining, 0f, "caster must be buffed");
            Assert.Greater(nearAlly.UltimateBuffRemaining, 0f, "near ally must be buffed");
            Assert.Greater(farAlly.UltimateBuffRemaining, 0f, "army-wide buff must reach far allies");
            Assert.AreEqual(
                HeroAbilityRules.KingsCommandPercent,
                farAlly.UltimateBuffPercent,
                0.001f);
            Assert.AreEqual(0f, enemy.UltimateBuffRemaining, "enemies must not be buffed");

            var casts = controller.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(AbilityIds.KingsCommand, casts[0].Def.AbilityId);
        }

        // ------------------------------------------------------------------ Aegis

        [Test]
        public void Aegis_AbsorbConsumesDamageBeforeHp()
        {
            var controller = CreateEarlyMatch();
            var attacker = SpawnAlly(controller, 20f);
            var target = SpawnAlly(controller, 22f);
            target.AbsorbRemaining = 50f;
            target.AbsorbSecondsRemaining = 6f;

            var hpBefore = target.CurrentHp;
            controller.Combat.ApplyDamage(attacker, target, 20f, attacker.OwnerSlot);

            Assert.AreEqual(hpBefore, target.CurrentHp, "absorb must consume damage before HP");
            Assert.AreEqual(30f, target.AbsorbRemaining, 0.01f);
        }

        [Test]
        public void Aegis_AbsorbSpillsRemainderToHp()
        {
            var controller = CreateEarlyMatch();
            var attacker = SpawnAlly(controller, 20f);
            var target = SpawnAlly(controller, 22f);
            target.AbsorbRemaining = 5f;
            target.AbsorbSecondsRemaining = 6f;

            var hpBefore = target.CurrentHp;
            controller.Combat.ApplyDamage(attacker, target, 20f, attacker.OwnerSlot);

            Assert.AreEqual(hpBefore - 15f, target.CurrentHp, 0.01f);
            Assert.AreEqual(0f, target.AbsorbRemaining, 0.01f);
        }

        [Test]
        public void Aegis_AbsorbExpiresWithTime()
        {
            var controller = CreateEarlyMatch();
            var target = SpawnAlly(controller, 22f);
            target.AbsorbRemaining = 50f;
            target.AbsorbSecondsRemaining = 1f;

            controller.Combat.Tick(1.5f);

            Assert.AreEqual(0f, target.AbsorbRemaining, 0.01f);
            Assert.AreEqual(0f, target.AbsorbSecondsRemaining, 0.01f);
        }

        [Test]
        public void Aegis_CastGrantsArmorAndShieldToAllies()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 2, level: 4, bonusSlot: 8);
            var ally = SpawnAlly(controller, hero.WorldPosition + new Vector3(3f, 0f, 0f));
            SpawnEnemy(controller, hero.WorldPosition + new Vector3(4f, 0f, 0f));

            controller.Combat.Tick(0.1f);

            Assert.Greater(ally.AbsorbRemaining, 0f, "ally must receive an absorb pool");
            Assert.Greater(ally.AbsorbSecondsRemaining, 0f);
            var expectedShield = ally.Stats.MaxHp * HeroAbilityRules.AegisShieldMaxHpFraction;
            Assert.AreEqual(expectedShield, ally.AbsorbRemaining, 0.51f);
        }

        // ------------------------------------------------------------------ Sanctuary

        [Test]
        public void Sanctuary_ZoneFollowsCaster()
        {
            var controller = CreateEarlyMatch();
            var hero = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Hero, HeroStats(),
                distanceAlongLane: 20f, isHero: true, heroSlot: 3, level: 4, bonusSlot: 9);
            var ally = SpawnAlly(controller, hero.WorldPosition + new Vector3(2f, 0f, 0f));
            ally.CurrentHp = ally.Stats.MaxHp - 100f;

            hero.Abilities = Only(AbilityKitDefaults.CreatePriestBonus(), AbilityIds.Sanctuary);
            hero.AbilityCooldownRemaining = new float[hero.Abilities.Length];

            controller.Combat.Tick(0.1f);
            Assert.IsTrue(controller.Combat.HasActiveHealZone(hero.UnitId), "sanctuary must be cast");

            // Move the caster away; ally stays at the old spot (out of every radius now).
            var healedAtOldSpot = ally.CurrentHp;
            controller.Combat.Tick(1f);
            Assert.Greater(ally.CurrentHp, healedAtOldSpot, "zone must heal while ally is inside");

            hero.WorldPosition = ally.WorldPosition + new Vector3(30f, 0f, 0f);
            var follower = SpawnAlly(controller, hero.WorldPosition + new Vector3(1f, 0f, 0f));
            follower.CurrentHp = follower.Stats.MaxHp - 100f;
            var followerBefore = follower.CurrentHp;
            var allyBeforeMove = ally.CurrentHp;

            controller.Combat.Tick(0.2f);

            Assert.Greater(follower.CurrentHp, followerBefore, "zone must follow the caster to the new position");
            Assert.AreEqual(allyBeforeMove, ally.CurrentHp, "ally left behind must stop regenerating");
        }

        // ------------------------------------------------------------------ Greater Colossus

        [Test]
        public void GreaterColossus_AuraCoversTwentyFivePercent()
        {
            var controller = CreateEarlyMatch();
            var titan = controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Titan, TitanStats(),
                distanceAlongLane: 20f, level: 7, bonusSlot: 10);
            var nearAlly = SpawnAlly(controller, titan.WorldPosition + new Vector3(3f, 0f, 0f));
            var farAlly = SpawnAlly(controller, titan.WorldPosition + new Vector3(30f, 0f, 0f));

            Assert.AreEqual(
                nearAlly.Stats.MaxHp * (1f + HeroAbilityRules.AuraMaxHpVeteranBonusPercent),
                controller.Combat.GetEffectiveMaxHp(nearAlly),
                0.01f);
            Assert.AreEqual(
                farAlly.Stats.MaxHp,
                controller.Combat.GetEffectiveMaxHp(farAlly),
                0.01f);
        }

        // ------------------------------------------------------------------ race uniques

        [Test]
        public void MarchDiscipline_MultipliesMoveSpeed()
        {
            var player = new MatchPlayerState(0, GameIds.Races.Human, 1000) { BonusPickSlot = 11 };
            var stats = new UnitCombatStats(UnitRole.Melee, 100f, 0f, 5f, 5f, 1f, 1.5f, 4f, 8);
            var boosted = RaceUpgradeStatsRules.Apply(stats, player);
            Assert.AreEqual(4f * HumanBonusUnitRules.MarchDisciplineMultiplier, boosted.MoveSpeed, 0.001f);

            var untouched = RaceUpgradeStatsRules.Apply(stats, new MatchPlayerState(1, GameIds.Races.Human, 1000));
            Assert.AreEqual(4f, untouched.MoveSpeed, 0.001f);

            Assert.AreEqual(
                4f * HumanBonusUnitRules.MarchDisciplineMultiplier,
                HumanBonusUnitRules.ApplyMarchDiscipline(player, 4f),
                0.001f);
        }

        [Test]
        public void StoneMasonry_ScalesBuildingHpRetroactively()
        {
            var controller = CreateEarlyMatch();
            BuildingState main = null;
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == 0 && building.BuildingId == GameIds.Buildings.Main)
                {
                    main = building;
                }
            }

            Assert.IsNotNull(main);
            var baseMaxHp = main.MaxHp;
            main.ApplyDamage(main.CurrentHp * 0.5f);
            var damagedFraction = main.CurrentHp / main.MaxHp;

            Assert.IsTrue(controller.TrySetBonusPick(0, HumanBonusUnitRules.RaceUnique2Slot));

            Assert.AreEqual(baseMaxHp * HumanBonusUnitRules.StoneMasonryHpMultiplier, main.MaxHp, 0.01f);
            Assert.AreEqual(
                damagedFraction,
                main.CurrentHp / main.MaxHp,
                0.01f,
                "current HP must scale proportionally");

            // Other players' buildings are untouched.
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot != 0)
                {
                    Assert.AreNotEqual(
                        baseMaxHp * HumanBonusUnitRules.StoneMasonryHpMultiplier,
                        building.MaxHp,
                        building.BuildingId);
                }
            }
        }

        // ------------------------------------------------------------------ spawn flow

        [Test]
        public void HeroDeploy_WithVeteranPick_SpawnsVeteranSlot()
        {
            var controller = CreateEarlyMatch();
            var player = controller.Players[0];
            player.Gold = 10_000;
            Assert.IsTrue(controller.TrySetBonusPick(0, HumanBonusUnitRules.BonusSlotForHeroSlot(1)));
            Assert.IsTrue(controller.TryHireHero(0, 1));
            controller.DebugCompleteResearchForOwner(0);

            var barracks = FindBarracks(controller, 0);
            Assert.IsTrue(controller.TryDeployHero(0, barracks.InstanceId, 1));

            MatchUnitState deployed = null;
            foreach (var unit in controller.Combat.Units)
            {
                if (unit.Role == UnitRole.Hero && unit.HeroSlot == 1)
                {
                    deployed = unit;
                }
            }

            Assert.IsNotNull(deployed);
            Assert.AreEqual(7, deployed.BonusSlot, "deployed hero must carry the veteran bonus slot");
            Assert.AreEqual(
                600f * HumanBonusUnitRules.VeteranHpMultiplier,
                deployed.Stats.MaxHp,
                0.01f);
        }

        // ------------------------------------------------------------------ helpers

        static System.Collections.Generic.List<int> Ids(UnitAbilityDef[] kit)
        {
            var ids = new System.Collections.Generic.List<int>();
            foreach (var def in kit)
            {
                ids.Add(def.AbilityId);
            }

            return ids;
        }

        static UnitAbilityDef[] Only(UnitAbilityDef[] kit, int abilityId)
        {
            foreach (var def in kit)
            {
                if (def.AbilityId == abilityId)
                {
                    return new[] { def };
                }
            }

            Assert.Fail($"ability {abilityId} not in kit");
            return System.Array.Empty<UnitAbilityDef>();
        }

        static MatchController CreateEarlyMatch()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            return controller;
        }

        static MatchUnitState SpawnAlly(MatchController controller, float distance) =>
            controller.Combat.SpawnUnit(
                0, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: distance);

        static MatchUnitState SpawnAlly(MatchController controller, Vector3 position)
        {
            var unit = SpawnAlly(controller, 20f);
            unit.WorldPosition = position;
            return unit;
        }

        static MatchUnitState SpawnEnemy(MatchController controller, Vector3 position)
        {
            var unit = controller.Combat.SpawnUnit(
                1, GameIds.Lanes.Left, UnitRole.Melee, MeleeStats(),
                distanceAlongLane: 24f);
            unit.WorldPosition = position;
            return unit;
        }

        static BuildingState FindBarracks(MatchController controller, int ownerSlot)
        {
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == ownerSlot && BuildingRules.IsBarracks(building.BuildingId))
                {
                    return building;
                }
            }

            Assert.Fail("no barracks found");
            return null;
        }

        static UnitCombatStats HeroStats() =>
            new(UnitRole.Hero, 600f, 4f, 35f, 45f, 1f, 1.5f, 4f, 80);

        static UnitCombatStats TitanStats() =>
            new(UnitRole.Titan, 1800f, 12f, 105f, 135f, 1f, 3f, 4f, 80);

        static UnitCombatStats MeleeStats() =>
            new(UnitRole.Melee, 600f, 0f, 35f, 45f, 1f, 1.5f, 4f, 80);
    }
}
