using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchSnapshotApplyTests
    {
        [Test]
        public void Capture_IncludesWinnerSlotWhenMatchEnded()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            controller.EndMatch(1);

            var snapshot = MatchSnapshotCodec.Capture(controller);
            Assert.AreEqual(1, snapshot.WinnerSlot);
            Assert.AreEqual((int)MatchPhase.End, snapshot.Phase);
        }

        [Test]
        public void RoundTrip_PreservesWinnerSlot()
        {
            var original = new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = (int)MatchPhase.End,
                MatchTimeSeconds = 90f,
                WinnerSlot = 0,
                Players = new[]
                {
                    new MatchPlayerSnapshot { Slot = 0, Gold = 100, IsEliminated = false },
                    new MatchPlayerSnapshot { Slot = 1, Gold = 0, IsEliminated = true },
                },
            };

            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(original));
            Assert.AreEqual(0, restored.WinnerSlot);
            Assert.AreEqual(100, restored.Players[0].Gold);
            Assert.IsTrue(restored.Players[1].IsEliminated);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_UpdatesGoldAndEndsMatch()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            controller.Players[0].Gold = 500;

            var ended = false;
            controller.MatchEnded += _ => ended = true;

            var snapshot = new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = (int)MatchPhase.End,
                MatchTimeSeconds = 42f,
                WinnerSlot = 1,
                Players = new[]
                {
                    new MatchPlayerSnapshot { Slot = 0, Gold = 777, IsEliminated = true },
                    new MatchPlayerSnapshot { Slot = 1, Gold = 200, IsEliminated = false },
                },
            };

            controller.ApplyAuthoritativeSnapshot(snapshot);

            Assert.AreEqual(777, controller.Players[0].Gold);
            Assert.IsTrue(controller.Players[0].IsEliminated);
            Assert.AreEqual(MatchPhase.End, controller.Phase);
            Assert.AreEqual(1, controller.WinnerSlot);
            Assert.AreEqual(42f, controller.MatchTimeSeconds);
            Assert.IsTrue(ended);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_UpdatesBuildingHpAndRuins()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            var main = FindBuilding(host, 1, GameIds.Buildings.Main);
            main.SetAuthoritativeHp(0f);

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Capture(host));

            var clientMain = FindBuilding(client, 1, GameIds.Buildings.Main);
            Assert.IsTrue(clientMain.IsRuins);
            Assert.AreEqual(0f, clientMain.CurrentHp);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_UpdatesUpgradeLevels()
        {
            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));

            var snapshot = new MatchSnapshot
            {
                PlayerCount = 2,
                Players = new[]
                {
                    new MatchPlayerSnapshot
                    {
                        Slot = 0, Gold = 0, IsEliminated = false,
                        MainLevel = 3, MagicLevel = 2,
                        MeleeDamageLevel = 5, RangedDamageLevel = 6, HpArmorLevel = 4,
                    },
                },
            };

            client.ApplyAuthoritativeSnapshot(snapshot);

            Assert.AreEqual(3, client.Players[0].MainLevel);
            Assert.AreEqual(2, client.Players[0].MagicLevel);
            Assert.AreEqual(5, client.Players[0].MeleeDamageLevel);
            Assert.AreEqual(6, client.Players[0].RangedDamageLevel);
            Assert.AreEqual(4, client.Players[0].HpArmorLevel);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_UpdatesDivineBlessingFields()
        {
            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));

            var snapshot = new MatchSnapshot
            {
                PlayerCount = 2,
                Players = new[]
                {
                    new MatchPlayerSnapshot
                    {
                        Slot = 0,
                        DivineBlessingComplete = true,
                        MainExtraAbilityId = 3,
                        MainMana = 175f,
                        MainExtraAbilityCooldownRemaining = 12f,
                        MainLevel = 2,
                    },
                },
            };

            client.ApplyAuthoritativeSnapshot(snapshot);

            Assert.IsTrue(client.Players[0].DivineBlessingComplete);
            Assert.AreEqual(3, client.Players[0].MainExtraAbilityId);
            Assert.AreEqual(175f, client.Players[0].MainMana, 0.01f);
            Assert.AreEqual(12f, client.Players[0].MainExtraAbilityCooldownRemaining, 0.01f);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_ForwardsSpellCastsToClientPresenterBuffer()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.Players[0].MagicLevel = 2;
            var casterStats = new UnitCombatStats(UnitRole.Caster, 100f, 0f, 1f, 1f, 0.1f, 30f, 0f, 1, maxMana: 200f);
            var enemyStats = new UnitCombatStats(UnitRole.Melee, 100f, 0f, 1f, 1f, 0.1f, 1f, 0f, 1);
            var caster = host.Combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Caster, casterStats);
            var enemy = host.Combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, enemyStats);
            caster.WorldPosition = new Vector3(0f, 0.15f, 0f);
            enemy.WorldPosition = new Vector3(0f, 0.15f, 2f);

            host.Combat.Tick(0.1f);
            var snapshot = MatchSnapshotCodec.Capture(host);
            Assert.AreEqual(1, snapshot.SpellCasts.Length, "Host snapshot should carry the Frost cast event.");
            Assert.AreEqual((ushort)AbilityIds.Frost, snapshot.SpellCasts[0].AbilityId);

            var catalog = ScriptableObject.CreateInstance<UnitAbilityCatalog>();
            catalog.ReplaceAbilities(AbilityKitDefaults.CreateCaster());

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.Combat.AbilityCatalog = catalog;
            client.ApplyAuthoritativeSnapshot(snapshot);

            var casts = client.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count, "Client should forward the snapshot cast to the presenter buffer.");
            Assert.AreEqual(AbilityIds.Frost, casts[0].Def.AbilityId);
            Assert.AreEqual(snapshot.SpellCasts[0].Serial, casts[0].Serial);

            Assert.AreEqual(0, client.Combat.ConsumePendingAbilityCasts().Count, "Consumed buffer should be empty.");

            client.ApplyAuthoritativeSnapshot(snapshot);
            Assert.AreEqual(
                0,
                client.Combat.ConsumePendingAbilityCasts().Count,
                "Re-applying the same serial must not duplicate.");
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_ForwardsProjectileSpawnEventsToClient()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.BeginEarlyPhase();
            var rangedStats = new UnitCombatStats(UnitRole.Ranged, 100f, 0f, 1f, 8f, 10f, 12f, 0f, 1);
            var enemyStats = new UnitCombatStats(UnitRole.Melee, 100f, 0f, 1f, 1f, 0.1f, 1f, 0f, 1);
            var ranged = host.Combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Ranged, rangedStats);
            var enemy = host.Combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, enemyStats);
            ranged.WorldPosition = new Vector3(0f, 0.15f, 0f);
            enemy.WorldPosition = new Vector3(0f, 0.15f, 3f);

            for (var i = 0; i < 20 && host.Combat.Projectiles.Count == 0; i++)
            {
                host.Combat.Tick(0.05f);
            }

            Assert.Greater(host.Combat.Projectiles.Count, 0, "Host should have spawned a ranged projectile.");
            Assert.Greater(host.Combat.NetworkProjectileSpawns.Count, 0, "Spawn must queue a network event.");

            var snapshot = MatchSnapshotCodec.Capture(host);
            Assert.Greater(snapshot.Projectiles.Length, 0, "Snapshot must carry spawn events.");
            Assert.AreEqual(0, host.Combat.NetworkProjectileSpawns.Count, "Capture drains the network buffer.");

            var secondCapture = MatchSnapshotCodec.Capture(host);
            Assert.AreEqual(0, secondCapture.Projectiles.Length, "No new shots → empty event list.");

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(snapshot);

            Assert.AreEqual(snapshot.Projectiles.Length, client.Combat.Projectiles.Count);
            Assert.AreEqual(snapshot.Projectiles[0].ProjectileId, client.Combat.Projectiles[0].ProjectileId);
            Assert.AreEqual(
                snapshot.Projectiles[0].FlightDuration,
                client.Combat.Projectiles[0].FlightDuration,
                0.01f);

            client.ApplyAuthoritativeSnapshot(secondCapture);
            Assert.AreEqual(
                snapshot.Projectiles.Length,
                client.Combat.Projectiles.Count,
                "Empty spawn list must not clear in-flight client visuals.");

            client.ApplyAuthoritativeSnapshot(snapshot);
            Assert.AreEqual(
                snapshot.Projectiles.Length,
                client.Combat.Projectiles.Count,
                "Re-applying same ProjectileId must not duplicate.");

            var flight = client.Combat.Projectiles[0].FlightDuration;
            client.Combat.AdvanceProjectilePresentation(flight + 0.05f);
            Assert.AreEqual(0, client.Combat.Projectiles.Count, "Client presentation removes finished shots.");
        }

        [Test]
        public void ApplyAuthoritativeProjectiles_SetsAppliesSplashAoeOnClient()
        {
            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = (int)MatchPhase.Early,
                Projectiles = new[]
                {
                    new MatchProjectileSnapshot
                    {
                        ProjectileId = 42,
                        AttackerOwnerSlot = 1,
                        AttackerRole = (byte)UnitRole.Super,
                        StartX = 0f,
                        StartY = 1f,
                        FlightDuration = 0.5f,
                        IsParabolic = true,
                        TargetBuildingInstanceId = -1,
                        SourceBuildingInstanceId = -1,
                        SourceBuildingId = string.Empty,
                        AppliesSplashAoe = true,
                    },
                },
            });

            Assert.AreEqual(1, client.Combat.Projectiles.Count);
            Assert.IsTrue(client.Combat.Projectiles[0].AppliesSplashAoe);
            Assert.AreEqual(UnitRole.Super, client.Combat.Projectiles[0].AttackerRole);
        }

        [Test]
        public void ApplyAuthoritativeSpellCasts_FxOnlyDefWithoutBehaviour_StillQueuesCast()
        {
            var vfx = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var def = ScriptableObject.CreateInstance<UnitAbilityDef>();
            def.Configure(
                42,
                "FxOnly",
                string.Empty,
                AbilityKind.Active,
                AbilityUnlock.Always,
                0,
                behaviour: null,
                fx: new AbilityFx { Color = Color.red, VfxPrefab = vfx });
            var catalog = ScriptableObject.CreateInstance<UnitAbilityCatalog>();
            catalog.ReplaceAbilities(new[] { def });

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.Combat.AbilityCatalog = catalog;
            client.ApplyAuthoritativeSnapshot(new MatchSnapshot
            {
                PlayerCount = 2,
                Phase = (int)MatchPhase.Early,
                SpellCasts = new[]
                {
                    new MatchSpellSnapshot
                    {
                        Serial = 1,
                        AbilityId = 42,
                        CasterUnitId = 1,
                        OwnerSlot = 0,
                        CenterX = 3f,
                        CenterZ = 4f,
                        Radius = 2f,
                    },
                },
            });

            var casts = client.Combat.ConsumePendingAbilityCasts();
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(42, casts[0].Def.AbilityId);
            Assert.IsNull(casts[0].Def.Behaviour);
            Assert.IsNotNull(casts[0].Def.Fx.VfxPrefab);

            Object.DestroyImmediate(vfx);
            Object.DestroyImmediate(def);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_UpdatesUnitsIntoCombat()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.BeginEarlyPhase();
            var stats = new UnitCombatStats(UnitRole.Melee, 100f, 0f, 1f, 1f, 1f, 1.5f, 4f, 1);
            var unit = host.Combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, 5f);
            unit.WorldPosition = new Vector3(12f, 0.15f, 3f);
            unit.FacingDirection = Vector3.right;

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Capture(host));

            Assert.AreEqual(1, client.Combat.Units.Count);
            var restored = client.Combat.GetUnit(unit.UnitId);
            Assert.IsNotNull(restored);
            Assert.AreEqual(12f, restored.WorldPosition.x, 0.01f);
            Assert.AreEqual(3f, restored.WorldPosition.z, 0.01f);
            Assert.AreEqual(GameIds.Lanes.Center, restored.LaneId);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_UpdatesPassiveGoldAndResearch()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.Players[0].PassiveGoldLevel = 2;
            host.Players[0].MainLevel = 2;
            var main = FindBuilding(host, 0, GameIds.Buildings.Main);
            Assert.IsTrue(host.Research.TryEnqueue(new BuildingResearchState(
                main.InstanceId,
                0,
                GameIds.Buildings.Main,
                GameIds.Upgrades.MainPassiveGold,
                costPaid: 200,
                durationSeconds: 25f)));
            host.Research.TryGetActive(main.InstanceId, out var active);
            active.RemainingSeconds = 9f;

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Capture(host));

            Assert.AreEqual(2, client.Players[0].PassiveGoldLevel);
            Assert.AreEqual(2, client.Players[0].MainLevel);
            Assert.IsTrue(client.Research.TryGetActive(main.InstanceId, out var clientResearch));
            Assert.AreEqual(GameIds.Upgrades.MainPassiveGold, clientResearch.UpgradeId);
            Assert.AreEqual(9f, clientResearch.RemainingSeconds, 0.01f);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_UpdatesBarracksLevel()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.WaveScheduler.SetBarracksLevel(0, GameIds.Buildings.BarracksLeft, 3);

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Capture(host));

            var barracks = client.WaveScheduler.GetBarracks(0, GameIds.Buildings.BarracksLeft);
            Assert.AreEqual(3, barracks.Level);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_UpdatesBarracksWaveTimer()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.BeginEarlyPhase();
            host.Tick(4f);
            var hostBarracks = host.WaveScheduler.GetBarracks(0, GameIds.Buildings.BarracksCenter);
            Assert.IsNotNull(hostBarracks);
            var expected = hostBarracks.TimeUntilNextWaveSeconds;
            Assert.Less(expected, 35f);

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Capture(host));

            var clientBarracks = client.WaveScheduler.GetBarracks(0, GameIds.Buildings.BarracksCenter);
            Assert.AreEqual(expected, clientBarracks.TimeUntilNextWaveSeconds, 0.01f);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_UpdatesUnitBehaviorAndAttackSwing()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.BeginEarlyPhase();
            var stats = new UnitCombatStats(UnitRole.Melee, 100f, 0f, 1f, 1f, 1f, 1.5f, 4f, 1);
            var unit = host.Combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, 5f);
            unit.BehaviorState = UnitBehaviorState.Attack;
            unit.AttackSwingSerial = 6;

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Capture(host));

            var restored = client.Combat.GetUnit(unit.UnitId);
            Assert.IsNotNull(restored);
            Assert.AreEqual(UnitBehaviorState.Attack, restored.BehaviorState);
            Assert.AreEqual(6, restored.AttackSwingSerial);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_BuildsRenderSamplesForInterpolation()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.BeginEarlyPhase();
            var stats = new UnitCombatStats(UnitRole.Melee, 100f, 0f, 1f, 1f, 1f, 1.5f, 4f, 1);
            var unit = host.Combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats, 5f);
            unit.WorldPosition = new Vector3(12f, 0.15f, 3f);
            unit.FacingDirection = Vector3.right;
            unit.AttackSwingSerial = 4;

            var first = MatchSnapshotCodec.Capture(host);

            host.Tick(1f);
            unit.WorldPosition = new Vector3(14f, 0.15f, 3f);
            unit.FacingDirection = Vector3.back;
            unit.AttackSwingSerial = 5;
            var second = MatchSnapshotCodec.Capture(host);
            Assert.Greater(second.MatchTimeSeconds, first.MatchTimeSeconds);

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(first);
            client.ApplyAuthoritativeSnapshot(second);

            var midTime = (first.MatchTimeSeconds + second.MatchTimeSeconds) * 0.5f;
            Assert.IsTrue(
                client.Combat.TryGetUnitRenderPair(unit.UnitId, midTime, out var prev, out var next, out var alpha));
            Assert.AreEqual(first.MatchTimeSeconds, prev.TimeSeconds);
            Assert.AreEqual(second.MatchTimeSeconds, next.TimeSeconds);
            Assert.Greater(alpha, 0.2f);
            Assert.Less(alpha, 0.8f);

            var interpolated = Vector3.Lerp(prev.Position, next.Position, alpha);
            Assert.Greater(interpolated.x, 12f);
            Assert.Less(interpolated.x, 14f);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_RetargetsCenterOpponentSlot()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(4));
            Assert.IsTrue(host.Graph.TryGetLane(0, GameIds.Lanes.Center, out var hostLane));
            var oldOpponent = hostLane.OpponentSlot;
            var next = CenterMarchRetargetRules.ResolveNextAliveClockwise(oldOpponent, host.Players, 0);
            Assert.IsNotNull(next);

            host.Players[oldOpponent].IsEliminated = true;
            CenterLaneRetarget.Apply(oldOpponent, host.Players, host.Layout, host.Graph, host.Combat);
            Assert.IsTrue(host.Graph.TryGetLane(0, GameIds.Lanes.Center, out hostLane));
            Assert.AreEqual(next.Value, hostLane.OpponentSlot);

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(4));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Capture(host));

            Assert.IsTrue(client.Graph.TryGetLane(0, GameIds.Lanes.Center, out var clientLane));
            Assert.AreEqual(next.Value, clientLane.OpponentSlot);
            var newMain = client.Layout.Slots[next.Value].GetBuildingWorldPosition(GameIds.Buildings.Main);
            newMain.y = 0f;
            var end = clientLane.Path.End;
            end.y = 0f;
            Assert.Less(Vector3.Distance(end, newMain), 0.5f);
        }

        [Test]
        public void ResolveLocalGold_PrefersSnapshotOnClient()
        {
            Assert.AreEqual(
                333,
                MatchHudGoldRules.ResolveLocalGold(
                    localSlot: 0,
                    controllerGold: 500,
                    snapshotGold: 333,
                    useSnapshot: true));
            Assert.AreEqual(
                500,
                MatchHudGoldRules.ResolveLocalGold(
                    localSlot: 0,
                    controllerGold: 500,
                    snapshotGold: 333,
                    useSnapshot: false));
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_CarriesMeleeTargetToClient()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.BeginEarlyPhase();
            var attackerStats = new UnitCombatStats(UnitRole.Melee, 200f, 0f, 10f, 10f, 1f, 1.2f, 0f, 1);
            var victimStats = new UnitCombatStats(UnitRole.Melee, 500f, 0f, 1f, 1f, 0.1f, 1f, 0f, 1);
            var attacker = host.Combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, attackerStats);
            var victim = host.Combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, victimStats);
            attacker.WorldPosition = new Vector3(0f, 0.15f, 0f);
            victim.WorldPosition = new Vector3(0f, 0.15f, 1f);

            var sawTarget = false;
            for (var i = 0; i < 40; i++)
            {
                host.Combat.Tick(0.05f);
                if (attacker.CurrentTargetId.HasValue)
                {
                    sawTarget = true;
                    break;
                }
            }

            Assert.IsTrue(sawTarget, "Host melee should acquire a target.");
            Assert.AreEqual(victim.UnitId, attacker.CurrentTargetId.Value);

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Capture(host));

            var clientAttacker = client.Combat.GetUnit(attacker.UnitId);
            Assert.IsNotNull(clientAttacker, "Attacker must exist on the client.");
            Assert.IsNotNull(
                clientAttacker.CurrentTargetId,
                "Melee target must reach the client — without it melee impact FX never plays off-host.");
            Assert.AreEqual(victim.UnitId, clientAttacker.CurrentTargetId.Value);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_CarriesAuraAbilityIdAndAttackCommit()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();
            var stats = new UnitCombatStats(UnitRole.Melee, 100f, 0f, 1f, 1f, 0.1f, 1f, 0f, 1);
            var unit = controller.Combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, stats);
            unit.WorldPosition = new Vector3(0f, 0.15f, 0f);

            var snapshot = MatchSnapshotCodec.Capture(controller);
            Assert.Greater(snapshot.Units.Length, 0, "Scenario needs at least one unit.");

            snapshot.Units[0].AuraAbilityId = AbilityIds.AuraDamagePercent;
            snapshot.Units[0].IsAttackCommitted = true;

            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(snapshot));
            Assert.AreEqual(AbilityIds.AuraDamagePercent, restored.Units[0].AuraAbilityId);
            Assert.IsTrue(restored.Units[0].IsAttackCommitted);

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(restored);

            var clientUnit = client.Combat.GetUnit(snapshot.Units[0].UnitId);
            Assert.IsNotNull(clientUnit);
            Assert.AreEqual(
                AbilityIds.AuraDamagePercent,
                clientUnit.AuraAbilityId,
                "Aura ability id must reach MatchUnitState so clients draw the right aura.");
            Assert.Greater(
                clientUnit.AttackCommitRemainingSeconds,
                0f,
                "Attack commit flag must reach clients (Super ammo hide).");
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_RestoresHeroOrderAndConfirmed()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            Assert.IsTrue(host.TrySetHeroOrder(0, new[] { 3, 1, 2 }));
            Assert.IsTrue(host.TrySetHeroOrder(1, new[] { 2, 3, 1 }));

            var bytes = MatchSnapshotCodec.Serialize(MatchSnapshotCodec.Capture(host));
            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.ApplyAuthoritativeSnapshot(MatchSnapshotCodec.Deserialize(bytes));

            Assert.IsTrue(client.Players[0].HeroOrderConfirmed);
            CollectionAssert.AreEqual(new[] { 3, 1, 2 }, client.Players[0].HeroOrder);
            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, client.Players[1].HeroOrder);
        }

        [Test]
        public void ApplyAuthoritativeSnapshot_IgnoresInvalidHeroOrder()
        {
            var host = new MatchController();
            host.StartMatch(MatchConfig.MvpDefault(2));
            host.Players[0].ConfirmHeroOrder(new[] { 3, 1, 2 });

            var snapshot = MatchSnapshotCodec.Capture(host);
            snapshot.Players[0].HeroOrder = new[] { 9, 9, 9 };

            var client = new MatchController();
            client.StartMatch(MatchConfig.MvpDefault(2));
            client.Players[0].ConfirmHeroOrder(new[] { 3, 1, 2 });
            client.ApplyAuthoritativeSnapshot(snapshot);

            CollectionAssert.AreEqual(new[] { 3, 1, 2 }, client.Players[0].HeroOrder);
            Assert.IsTrue(client.Players[0].HeroOrderConfirmed);
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

            Assert.Fail($"Building {buildingId} not found for slot {ownerSlot}");
            return null;
        }
    }
}
