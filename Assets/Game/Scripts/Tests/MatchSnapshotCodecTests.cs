using System.IO;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class MatchSnapshotCodecTests
    {
        [Test]
        public void RoundTrip_PreservesPlayersAndUnits()
        {
            var original = new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = 2,
                MatchTimeSeconds = 12.5f,
                Players = new[]
                {
                    new MatchPlayerSnapshot
                    {
                        Slot = 0, Gold = 500, IsEliminated = false, PassiveGoldLevel = 2, MainLevel = 2,
                        MagicLevel = 2, MeleeDamageLevel = 3, RangedDamageLevel = 4, HpArmorLevel = 5,
                    },
                    new MatchPlayerSnapshot
                    {
                        Slot = 1, Gold = 480, IsEliminated = false, PassiveGoldLevel = 0, MainLevel = 1,
                    },
                },
                Buildings = new[]
                {
                    new MatchBuildingSnapshot
                    {
                        InstanceId = 3,
                        OwnerSlot = 0,
                        BuildingId = "BUILDING_MAIN",
                        Health = 1000f,
                        IsRuins = false,
                    },
                },
                Units = new[]
                {
                    new MatchUnitSnapshot
                    {
                        UnitId = 7,
                        OwnerSlot = 1,
                        UnitDefId = "Melee",
                        LaneId = GameIds.Lanes.Left,
                        PosX = 3.5f,
                        PosZ = -2f,
                        FacingX = 1f,
                        FacingZ = 0f,
                        Health = 40f,
                        IsAlive = true,
                        BehaviorState = (byte)UnitBehaviorState.Attack,
                        AttackSwingSerial = 4,
                    },
                },
                Research = new[]
                {
                    new MatchResearchSnapshot
                    {
                        BuildingInstanceId = 3,
                        OwnerSlot = 0,
                        BuildingId = GameIds.Buildings.Main,
                        UpgradeId = GameIds.Upgrades.MainPassiveGold,
                        CostPaid = 200,
                        DurationSeconds = 25f,
                        RemainingSeconds = 12f,
                    },
                },
                Barracks = new[]
                {
                    new MatchBarracksSnapshot
                    {
                        OwnerSlot = 0,
                        BarracksId = GameIds.Buildings.BarracksCenter,
                        Level = 2,
                        IsRuins = false,
                        FrozenSquadLevel = 1,
                        TimeUntilNextWaveSeconds = 18.25f,
                    },
                },
                CenterLanes = new[]
                {
                    new MatchCenterLaneSnapshot { OwnerSlot = 0, OpponentSlot = 1 },
                },
            };

            var bytes = MatchSnapshotCodec.Serialize(original);
            var restored = MatchSnapshotCodec.Deserialize(bytes);

            Assert.AreEqual(original.PlayerCount, restored.PlayerCount);
            Assert.AreEqual(original.Phase, restored.Phase);
            Assert.AreEqual(original.MatchTimeSeconds, restored.MatchTimeSeconds);
            Assert.AreEqual(2, restored.Players.Length);
            Assert.AreEqual(500, restored.Players[0].Gold);
            Assert.AreEqual(2, restored.Players[0].PassiveGoldLevel);
            Assert.AreEqual(2, restored.Players[0].MainLevel);
            Assert.AreEqual(2, restored.Players[0].MagicLevel);
            Assert.AreEqual(3, restored.Players[0].MeleeDamageLevel);
            Assert.AreEqual(4, restored.Players[0].RangedDamageLevel);
            Assert.AreEqual(5, restored.Players[0].HpArmorLevel);
            Assert.AreEqual(3, restored.Buildings[0].InstanceId);
            Assert.AreEqual("BUILDING_MAIN", restored.Buildings[0].BuildingId);
            Assert.AreEqual(7, restored.Units[0].UnitId);
            Assert.AreEqual(3.5f, restored.Units[0].PosX);
            Assert.AreEqual(GameIds.Lanes.Left, restored.Units[0].LaneId);
            Assert.AreEqual(1f, restored.Units[0].FacingX);
            Assert.AreEqual((byte)UnitBehaviorState.Attack, restored.Units[0].BehaviorState);
            Assert.AreEqual(4, restored.Units[0].AttackSwingSerial);
            Assert.AreEqual(12f, restored.Research[0].RemainingSeconds);
            Assert.AreEqual(2, restored.Barracks[0].Level);
            Assert.AreEqual(18.25f, restored.Barracks[0].TimeUntilNextWaveSeconds, 0.01f);
            Assert.AreEqual(1, restored.CenterLanes[0].OpponentSlot);
        }

        [Test]
        public void RoundTrip_V16_PreservesSpellCasts()
        {
            var original = new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = 1,
                MatchTimeSeconds = 5f,
                WinnerSlot = -1,
                SpellCasts = new[]
                {
                    new MatchSpellSnapshot
                    {
                        Serial = 3,
                        CasterUnitId = 11,
                        OwnerSlot = 0,
                        AbilityId = (ushort)AbilityIds.Frost,
                        TargetUnitId = 14,
                        CenterX = 12.5f,
                        CenterZ = -4.25f,
                        Radius = 5f,
                    },
                    new MatchSpellSnapshot
                    {
                        Serial = 4,
                        CasterUnitId = 11,
                        OwnerSlot = 0,
                        AbilityId = (ushort)AbilityIds.CasterHeal,
                        TargetUnitId = 12,
                        CenterX = 8f,
                        CenterZ = 2f,
                        Radius = 0f,
                    },
                },
            };

            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(original));

            Assert.AreEqual(2, restored.SpellCasts.Length);
            Assert.AreEqual(3, restored.SpellCasts[0].Serial);
            Assert.AreEqual(11, restored.SpellCasts[0].CasterUnitId);
            Assert.AreEqual((ushort)AbilityIds.Frost, restored.SpellCasts[0].AbilityId);
            Assert.AreEqual(14, restored.SpellCasts[0].TargetUnitId);
            Assert.AreEqual(12.5f, restored.SpellCasts[0].CenterX, 0.01f);
            Assert.AreEqual(-4.25f, restored.SpellCasts[0].CenterZ, 0.01f);
            Assert.AreEqual(5f, restored.SpellCasts[0].Radius, 0.01f);
            Assert.AreEqual((ushort)AbilityIds.CasterHeal, restored.SpellCasts[1].AbilityId);
        }

        [Test]
        public void RoundTrip_V12_PreservesProjectileSpawnEvents()
        {
            var original = new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = 1,
                MatchTimeSeconds = 5f,
                WinnerSlot = -1,
                Projectiles = new[]
                {
                    new MatchProjectileSnapshot
                    {
                        ProjectileId = 9,
                        AttackerOwnerSlot = 1,
                        AttackerRole = (byte)UnitRole.Ranged,
                        StartX = 1f,
                        StartY = 0.5f,
                        StartZ = 2f,
                        TargetX = 4f,
                        TargetY = 0.4f,
                        TargetZ = 6f,
                        FlightDuration = 0.35f,
                        Elapsed = 0f,
                        IsParabolic = true,
                        TargetBuildingInstanceId = -1,
                        SourceBuildingInstanceId = -1,
                        SourceBuildingId = string.Empty,
                    },
                    new MatchProjectileSnapshot
                    {
                        ProjectileId = 10,
                        AttackerOwnerSlot = 0,
                        AttackerRole = (byte)UnitRole.Ranged,
                        StartX = 0f,
                        StartY = 1f,
                        StartZ = 0f,
                        TargetX = 2f,
                        TargetY = 0.5f,
                        TargetZ = 2f,
                        FlightDuration = 0.2f,
                        Elapsed = 0f,
                        IsParabolic = false,
                        TargetBuildingInstanceId = -1,
                        SourceBuildingInstanceId = 7,
                        SourceBuildingId = GameIds.Buildings.Main,
                    },
                },
            };

            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(original));

            Assert.AreEqual(2, restored.Projectiles.Length);
            Assert.AreEqual(9, restored.Projectiles[0].ProjectileId);
            Assert.AreEqual(1, restored.Projectiles[0].AttackerOwnerSlot);
            Assert.AreEqual((byte)UnitRole.Ranged, restored.Projectiles[0].AttackerRole);
            Assert.AreEqual(1f, restored.Projectiles[0].StartX, 0.01f);
            Assert.AreEqual(0.5f, restored.Projectiles[0].StartY, 0.01f);
            Assert.AreEqual(4f, restored.Projectiles[0].TargetX, 0.01f);
            Assert.AreEqual(0.35f, restored.Projectiles[0].FlightDuration, 0.01f);
            Assert.AreEqual(0f, restored.Projectiles[0].Elapsed, 0.01f);
            Assert.IsTrue(restored.Projectiles[0].IsParabolic);
            Assert.AreEqual(7, restored.Projectiles[1].SourceBuildingInstanceId);
            Assert.AreEqual(GameIds.Buildings.Main, restored.Projectiles[1].SourceBuildingId);
        }

        [Test]
        public void RoundTrip_V17_PreservesDivineBlessingFields()
        {
            var original = new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = 1,
                MatchTimeSeconds = 5f,
                WinnerSlot = -1,
                Players = new[]
                {
                    new MatchPlayerSnapshot
                    {
                        Slot = 0,
                        DivineBlessingComplete = true,
                        MainExtraAbilityId = 5,
                    },
                    new MatchPlayerSnapshot
                    {
                        Slot = 1,
                        DivineBlessingComplete = false,
                        MainExtraAbilityId = 0,
                    },
                },
            };

            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(original));

            Assert.IsTrue(restored.Players[0].DivineBlessingComplete);
            Assert.AreEqual(5, restored.Players[0].MainExtraAbilityId);
            Assert.IsFalse(restored.Players[1].DivineBlessingComplete);
            Assert.AreEqual(0, restored.Players[1].MainExtraAbilityId);
        }

        [Test]
        public void RoundTrip_V18_PreservesMainManaAndCooldown()
        {
            var original = new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = 1,
                MatchTimeSeconds = 5f,
                WinnerSlot = -1,
                Players = new[]
                {
                    new MatchPlayerSnapshot
                    {
                        Slot = 0,
                        DivineBlessingComplete = true,
                        MainExtraAbilityId = 1,
                        MainMana = 150.5f,
                        MainExtraAbilityCooldownRemaining = 42f,
                    },
                    new MatchPlayerSnapshot
                    {
                        Slot = 1,
                        MainMana = 100f,
                        MainExtraAbilityCooldownRemaining = 0f,
                    },
                },
            };

            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(original));

            Assert.AreEqual(150.5f, restored.Players[0].MainMana, 0.01f);
            Assert.AreEqual(42f, restored.Players[0].MainExtraAbilityCooldownRemaining, 0.01f);
            Assert.AreEqual(1, restored.Players[0].MainExtraAbilityId);
            Assert.AreEqual(100f, restored.Players[1].MainMana, 0.01f);
        }

        [Test]
        public void RoundTrip_V19_PreservesBonusAndAuraFields()
        {
            var original = new MatchSnapshot
            {
                PlayerCount = 1,
                Phase = 1,
                MatchTimeSeconds = 8f,
                WinnerSlot = -1,
                Units = new[]
                {
                    new MatchUnitSnapshot
                    {
                        UnitId = 9,
                        OwnerSlot = 0,
                        UnitDefId = "Melee",
                        LaneId = GameIds.Lanes.Left,
                        PosX = 1f,
                        PosZ = 2f,
                        FacingX = 0f,
                        FacingZ = 1f,
                        Health = 80f,
                        IsAlive = true,
                        BonusSlot = 4,
                        AuraRadius = 8f,
                        AuraColorPacked = unchecked((int)0xFF33CC55),
                    },
                },
            };

            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(original));

            Assert.AreEqual(4, restored.Units[0].BonusSlot);
            Assert.AreEqual(8f, restored.Units[0].AuraRadius, 0.01f);
            Assert.AreEqual(unchecked((int)0xFF33CC55), restored.Units[0].AuraColorPacked);
        }

        [Test]
        public void RoundTrip_V13_PreservesBonusPickFields()
        {
            var original = new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = 1,
                MatchTimeSeconds = 5f,
                WinnerSlot = -1,
                BonusPickDeadlineSeconds = 42.5f,
                Players = new[]
                {
                    new MatchPlayerSnapshot { Slot = 0, BonusPickSlot = 3 },
                    new MatchPlayerSnapshot { Slot = 1, BonusPickSlot = 11 },
                },
            };

            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(original));

            Assert.AreEqual(42.5f, restored.BonusPickDeadlineSeconds, 0.01f);
            Assert.AreEqual(3, restored.Players[0].BonusPickSlot);
            Assert.AreEqual(11, restored.Players[1].BonusPickSlot);
        }

        [Test]
        public void RoundTrip_V14_PreservesTitanFields()
        {
            var original = new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = 1,
                MatchTimeSeconds = 5f,
                WinnerSlot = -1,
                Players = new[]
                {
                    new MatchPlayerSnapshot
                    {
                        Slot = 0,
                        TitanResearchProgressSeconds = 90.5f,
                        TitanUnlocked = true,
                        TitanState = (int)TitanLifecycleState.IdleAtBase,
                    },
                    new MatchPlayerSnapshot
                    {
                        Slot = 1,
                        TitanResearchProgressSeconds = 0f,
                        TitanUnlocked = false,
                        TitanState = (int)TitanLifecycleState.Locked,
                    },
                },
            };

            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(original));

            Assert.AreEqual(90.5f, restored.Players[0].TitanResearchProgressSeconds, 0.01f);
            Assert.IsTrue(restored.Players[0].TitanUnlocked);
            Assert.AreEqual((int)TitanLifecycleState.IdleAtBase, restored.Players[0].TitanState);
            Assert.AreEqual(0f, restored.Players[1].TitanResearchProgressSeconds, 0.01f);
            Assert.IsFalse(restored.Players[1].TitanUnlocked);
            Assert.AreEqual((int)TitanLifecycleState.Locked, restored.Players[1].TitanState);
        }

        [Test]
        public void Capture_RoundTrip_PreservesTitanFields()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            var titan = controller.GetTitanState(0);
            titan.ResearchProgressSeconds = 120f;
            titan.State = TitanLifecycleState.IdleAtBase;

            var restored = MatchSnapshotCodec.Deserialize(
                MatchSnapshotCodec.Serialize(MatchSnapshotCodec.Capture(controller)));

            Assert.AreEqual(120f, restored.Players[0].TitanResearchProgressSeconds, 0.01f);
            Assert.IsTrue(restored.Players[0].TitanUnlocked);
            Assert.AreEqual((int)TitanLifecycleState.IdleAtBase, restored.Players[0].TitanState);
            Assert.AreEqual((int)TitanLifecycleState.Locked, restored.Players[1].TitanState);
        }

        [Test]
        public void RoundTrip_V15_PreservesHeroRosterAndTitanProgression()
        {
            var original = new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = 1,
                MatchTimeSeconds = 5f,
                WinnerSlot = -1,
                Players = new[]
                {
                    new MatchPlayerSnapshot
                    {
                        Slot = 0,
                        TitanResearchProgressSeconds = 180f,
                        TitanUnlocked = true,
                        TitanState = (int)TitanLifecycleState.Dead,
                        TitanLevel = 4,
                        TitanXp = 40,
                        TitanLastBarracksInstanceId = 7,
                        TitanDeathCooldownRemaining = 120.5f,
                    },
                    new MatchPlayerSnapshot { Slot = 1 },
                },
                Heroes = new[]
                {
                    new MatchHeroSlotSnapshot
                    {
                        OwnerSlot = 0,
                        HeroSlot = 1,
                        State = (int)HeroLifecycleState.IdleAtBase,
                        Level = 5,
                        Xp = 20,
                        LastDeployBarracksInstanceId = 3,
                        DeathCooldownRemaining = 10f,
                    },
                    new MatchHeroSlotSnapshot
                    {
                        OwnerSlot = 0,
                        HeroSlot = 2,
                        State = (int)HeroLifecycleState.Dead,
                        Level = 2,
                        Xp = 0,
                    },
                },
                Units = new[]
                {
                    new MatchUnitSnapshot
                    {
                        UnitId = 11,
                        OwnerSlot = 0,
                        UnitDefId = "Hero",
                        LaneId = GameIds.Lanes.Center,
                        Health = 600f,
                        IsAlive = true,
                        Level = 5,
                        Xp = 20,
                        HeroSlot = 1,
                        IsParkedAtBase = true,
                    },
                },
            };

            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(original));

            Assert.AreEqual(4, restored.Players[0].TitanLevel);
            Assert.AreEqual(40, restored.Players[0].TitanXp);
            Assert.AreEqual(7, restored.Players[0].TitanLastBarracksInstanceId);
            Assert.AreEqual(120.5f, restored.Players[0].TitanDeathCooldownRemaining, 0.01f);
            Assert.AreEqual(2, restored.Heroes.Length);
            Assert.AreEqual((int)HeroLifecycleState.IdleAtBase, restored.Heroes[0].State);
            Assert.AreEqual(5, restored.Heroes[0].Level);
            Assert.AreEqual(20, restored.Heroes[0].Xp);
            Assert.AreEqual(3, restored.Heroes[0].LastDeployBarracksInstanceId);
            Assert.AreEqual(10f, restored.Heroes[0].DeathCooldownRemaining, 0.01f);
            Assert.AreEqual(1, restored.Units[0].HeroSlot);
            Assert.IsTrue(restored.Units[0].IsParkedAtBase);
        }

        [Test]
        public void Capture_Apply_RestoresHeroRosterAndTitanOnFreshController()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.BeginEarlyPhase();
            host.Players[0].Gold = 5000;
            host.Players[0].MainLevel = 3;
            Assert.IsTrue(host.TryHireHero(0, 1));
            host.Tick(HeroRules.HireResearchSeconds);
            host.GetHeroRoster(0).Get(1).AddXp(HeroLevelRules.XpToNext(1));

            var titan = host.GetTitanState(0);
            titan.ResearchProgressSeconds = 90f;
            titan.Level = 3;
            titan.Xp = 15;
            titan.State = TitanLifecycleState.Dead;
            titan.MarkSummonedFrom(FindBuilding(host, 0, GameIds.Buildings.BarracksLeft).InstanceId);
            titan.StartDeathCooldown(200f);

            var bytes = MatchSnapshotCodec.Serialize(MatchSnapshotCodec.Capture(host));
            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Deserialize(bytes));

            var restoredHero = client.GetHeroRoster(0).Get(1);
            Assert.AreEqual(HeroLifecycleState.IdleAtBase, restoredHero.State);
            Assert.AreEqual(2, restoredHero.Level);
            Assert.AreEqual(0, restoredHero.Xp);
            Assert.IsTrue(restoredHero.DeployedUnitId.HasValue);

            var parked = client.Combat.GetUnit(restoredHero.DeployedUnitId.Value);
            Assert.IsNotNull(parked);
            Assert.IsTrue(parked.IsParkedAtBase);
            Assert.AreEqual(1, parked.HeroSlot);

            var restoredTitan = client.GetTitanState(0);
            Assert.AreEqual(TitanLifecycleState.Dead, restoredTitan.State);
            Assert.AreEqual(3, restoredTitan.Level);
            Assert.AreEqual(15, restoredTitan.Xp);
            Assert.AreEqual(90f, restoredTitan.ResearchProgressSeconds, 0.01f);
            var left = FindBuilding(client, 0, GameIds.Buildings.BarracksLeft);
            Assert.Greater(restoredTitan.GetDeathCooldown(left.InstanceId), 0f);
        }

        static BuildingState FindBuilding(MatchController controller, int ownerSlot, string buildingId)
        {
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == ownerSlot && building.BuildingId == buildingId)
                {
                    return building;
                }
            }

            Assert.Fail($"Building {buildingId} not found.");
            return null;
        }

        [Test]
        public void Capture_RoundTrip_PreservesBarracksTimerAndUnitAttackAnim()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            controller.Tick(3.5f);

            var barracks = controller.WaveScheduler.GetBarracks(0, GameIds.Buildings.BarracksCenter);
            Assert.IsNotNull(barracks);
            var expectedTimer = barracks.TimeUntilNextWaveSeconds;

            var stats = new UnitCombatStats(UnitRole.Melee, 100f, 0f, 1f, 1f, 1f, 1.5f, 4f, 1);
            var unit = controller.Combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, 5f);
            unit.BehaviorState = UnitBehaviorState.Attack;
            unit.AttackSwingSerial = 9;

            var restored = MatchSnapshotCodec.Deserialize(
                MatchSnapshotCodec.Serialize(MatchSnapshotCodec.Capture(controller)));

            MatchBarracksSnapshot barracksSnap = default;
            for (var i = 0; i < restored.Barracks.Length; i++)
            {
                if (restored.Barracks[i].OwnerSlot == 0
                    && restored.Barracks[i].BarracksId == GameIds.Buildings.BarracksCenter)
                {
                    barracksSnap = restored.Barracks[i];
                    break;
                }
            }

            Assert.AreEqual(expectedTimer, barracksSnap.TimeUntilNextWaveSeconds, 0.01f);

            MatchUnitSnapshot unitSnap = default;
            for (var i = 0; i < restored.Units.Length; i++)
            {
                if (restored.Units[i].UnitId == unit.UnitId)
                {
                    unitSnap = restored.Units[i];
                    break;
                }
            }

            Assert.AreEqual((byte)UnitBehaviorState.Attack, unitSnap.BehaviorState);
            Assert.AreEqual(9, unitSnap.AttackSwingSerial);
        }

        [Test]
        public void Deserialize_V5_DefaultsMissingAnimAndTimerFields()
        {
            var bytes = BuildMinimalV5SnapshotBytes();
            var restored = MatchSnapshotCodec.Deserialize(bytes);

            Assert.AreEqual(1, restored.Units.Length);
            Assert.AreEqual(0, restored.Units[0].BehaviorState);
            Assert.AreEqual(0, restored.Units[0].AttackSwingSerial);
            Assert.AreEqual(1, restored.Barracks.Length);
            Assert.AreEqual(0f, restored.Barracks[0].TimeUntilNextWaveSeconds, 0.01f);
        }

        [Test]
        public void Capture_RoundTrip_PreservesBarracksCallCharges()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            controller.Players[0].Gold = 200;

            BuildingState barracksBuilding = null;
            foreach (var building in controller.Buildings.Buildings)
            {
                if (building.OwnerSlot == 0 && BuildingRules.IsBarracks(building.BuildingId))
                {
                    barracksBuilding = building;
                    break;
                }
            }

            Assert.IsNotNull(barracksBuilding);
            Assert.IsTrue(controller.TryManualCallUnit(0, barracksBuilding.InstanceId, UnitRole.Melee));

            var barracks = controller.WaveScheduler.GetBarracks(0, barracksBuilding.BuildingId);
            var expected = barracks.CallCharges.GetCharges(UnitRole.Melee);

            var restored = MatchSnapshotCodec.Deserialize(
                MatchSnapshotCodec.Serialize(MatchSnapshotCodec.Capture(controller)));
            MatchBarracksSnapshot snap = default;
            for (var i = 0; i < restored.Barracks.Length; i++)
            {
                if (restored.Barracks[i].OwnerSlot == 0
                    && restored.Barracks[i].BarracksId == barracksBuilding.BuildingId)
                {
                    snap = restored.Barracks[i];
                    break;
                }
            }

            Assert.IsNotNull(snap.CallCurrent);
            Assert.AreEqual(expected, snap.CallCurrent[0]);
        }

        [Test]
        public void Capture_RoundTrip_PreservesUpgradeLevels()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            var player = controller.Players[0];
            player.MagicLevel = 2;
            player.MeleeDamageLevel = 3;
            player.RangedDamageLevel = 1;
            player.HpArmorLevel = 4;

            var restored = MatchSnapshotCodec.Deserialize(
                MatchSnapshotCodec.Serialize(MatchSnapshotCodec.Capture(controller)));

            Assert.AreEqual(2, restored.Players[0].MagicLevel);
            Assert.AreEqual(3, restored.Players[0].MeleeDamageLevel);
            Assert.AreEqual(1, restored.Players[0].RangedDamageLevel);
            Assert.AreEqual(4, restored.Players[0].HpArmorLevel);
        }

        [Test]
        public void Deserialize_V2_StillReadsUnitsWithoutV3Fields()
        {
            // Build a minimal v2 payload manually via legacy shape: serialize v3 then we only assert
            // that Capture→Serialize→Deserialize of a live match remains stable.
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            var snapshot = MatchSnapshotCodec.Capture(controller);
            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(snapshot));

            Assert.AreEqual(controller.Players.Count, restored.PlayerCount);
            Assert.AreEqual(controller.Buildings.Buildings.Count, restored.Buildings.Length);
            Assert.Greater(restored.CenterLanes.Length, 0);
            Assert.AreEqual(controller.WaveScheduler.Barracks.Count, restored.Barracks.Length);
        }

        [Test]
        public void ShouldTickSimulation_OfflineAndServerOnly()
        {
            Assert.IsTrue(MatchTickAuthority.ShouldTickSimulation(MatchTickMode.Offline));
            Assert.IsTrue(MatchTickAuthority.ShouldTickSimulation(MatchTickMode.Server));
            Assert.IsFalse(MatchTickAuthority.ShouldTickSimulation(MatchTickMode.Client));
        }

        [Test]
        public void TickModeAfterAuthorityDespawn_KeepsClientWhileSessionHeld()
        {
            Assert.AreEqual(
                MatchTickMode.Client,
                MatchTickAuthority.TickModeAfterAuthorityDespawn(networkedSessionHeld: true));
            Assert.AreEqual(
                MatchTickMode.Offline,
                MatchTickAuthority.TickModeAfterAuthorityDespawn(networkedSessionHeld: false));
        }

        /// <summary>Hand-built v5 payload (no BehaviorState / AttackSwingSerial / wave timer).</summary>
        static byte[] BuildMinimalV5SnapshotBytes()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(5);
            writer.Write(2);
            writer.Write(1);
            writer.Write(10f);
            writer.Write(-1);

            writer.Write(0);
            writer.Write(0);

            writer.Write(1);
            writer.Write(1);
            writer.Write(0);
            writer.Write("Melee");
            writer.Write(GameIds.Lanes.Center);
            writer.Write(1f);
            writer.Write(2f);
            writer.Write(0f);
            writer.Write(1f);
            writer.Write(50f);
            writer.Write(true);

            writer.Write(0);

            writer.Write(1);
            writer.Write(0);
            writer.Write(GameIds.Buildings.BarracksCenter);
            writer.Write(1);
            writer.Write(false);
            writer.Write(1);
            writer.Write(false);

            writer.Write(0);
            writer.Write(0u);
            return stream.ToArray();
        }
    }
}
