using Game.Core;
using Game.Editor;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public sealed class UnitVisualCatalogTests
    {
        UnitVisualCatalog _catalog;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            UnitVisualPrefabBuilder.EnsureContent();
            _catalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
        }

        [Test]
        public void Catalog_ExistsAfterEnsureContent()
        {
            Assert.IsNotNull(_catalog);
        }

        [Test]
        public void TryGetPrefab_HumanMelee_ReturnsCanonicalUnitsPath()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Melee, out var prefab));
            Assert.IsNotNull(prefab);
            Assert.AreEqual("Human_Melee", prefab.name);
            Assert.AreEqual(
                UnitVisualPrefabBuilder.HumanPath + "/Human_Melee.prefab",
                AssetDatabase.GetAssetPath(prefab));
        }

        [Test]
        public void TryGetPrefab_HumanRanged_ReturnsCanonicalUnitsPath()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Ranged, out var prefab));
            Assert.IsNotNull(prefab);
            Assert.AreEqual("Human_Ranged", prefab.name);
            Assert.AreEqual(
                UnitVisualPrefabBuilder.HumanPath + "/Human_Ranged.prefab",
                AssetDatabase.GetAssetPath(prefab));
        }

        [Test]
        public void TryGetPrefab_HumanCaster_ReturnsCanonicalUnitsPath()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Caster, out var prefab));
            Assert.IsNotNull(prefab);
            Assert.AreEqual("Human_Caster", prefab.name);
            Assert.AreEqual(
                UnitVisualPrefabBuilder.HumanPath + "/Human_Caster.prefab",
                AssetDatabase.GetAssetPath(prefab));
        }

        [Test]
        public void TryGetPrefab_BugCaster_ReturnsNamedPrefab()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Bug, UnitRole.Caster, out var prefab));
            Assert.IsNotNull(prefab);
            Assert.AreEqual("Bug_Caster", prefab.name);
        }

        [Test]
        public void AllTwelvePrefabs_AreAssigned()
        {
            foreach (var raceId in new[] { GameIds.Races.Human, GameIds.Races.Bug })
            {
                foreach (UnitRole role in System.Enum.GetValues(typeof(UnitRole)))
                {
                    Assert.IsTrue(
                        _catalog.TryGetPrefab(raceId, role, out var prefab),
                        $"Missing prefab for {raceId} {role}");
                    Assert.IsNotNull(prefab, $"Null prefab for {raceId} {role}");
                }
            }
        }

        [Test]
        public void AllTwelvePortraits_AreAssigned()
        {
            UnitPortraitBaker.BakeIntoCatalog(_catalog);
            foreach (var raceId in new[] { GameIds.Races.Human, GameIds.Races.Bug })
            {
                foreach (UnitRole role in System.Enum.GetValues(typeof(UnitRole)))
                {
                    Assert.IsTrue(
                        _catalog.TryGetPortrait(raceId, role, out var portrait),
                        $"Missing portrait for {raceId} {role}");
                    Assert.IsNotNull(portrait, $"Null portrait for {raceId} {role}");
                }
            }
        }

        [Test]
        public void HumanMeleePrefab_HasAnimatorWithCombatParameters()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Melee, out var prefab));
            var animator = prefab.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator);
            Assert.IsNotNull(animator.runtimeAnimatorController);

            var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            Assert.IsNotNull(controller);

            var names = new System.Collections.Generic.HashSet<string>();
            foreach (var parameter in controller.parameters)
            {
                names.Add(parameter.name);
            }

            Assert.IsTrue(names.Contains(UnitCombatAnimatorDriver.SpeedParam));
            Assert.IsTrue(names.Contains(UnitCombatAnimatorDriver.AttackParam));
            Assert.IsTrue(names.Contains(UnitCombatAnimatorDriver.DeathParam));
        }

        [Test]
        public void HumanMeleePrefab_FacesUnityForwardAxis()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Melee, out var prefab));
            Assert.AreEqual(
                UnitGreyboxVisuals.AnimatedHumanModelYawDegrees,
                prefab.transform.localEulerAngles.y,
                0.1f);
            Assert.AreEqual(0.5f, UnitGreyboxVisuals.AnimatedHumanScaleFactor, 0.001f);
        }

        [Test]
        public void HumanMeleePrefab_HasTeamAccentsTintedBySlotColor()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Melee, out var prefab));
            Assert.GreaterOrEqual(
                UnitVisualAccent.CountAccents(prefab.transform),
                1,
                "Human_Melee should expose TeamAccent geosets for player color.");

            var instance = Object.Instantiate(prefab);
            try
            {
                var color = new Color(0.1f, 0.4f, 0.9f, 1f);
                UnitVisualAccent.ApplyTeamColor(instance.transform, color);
                var accents = 0;
                foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith(UnitVisualAccent.TeamAccentTransformName))
                    {
                        continue;
                    }

                    var renderer = t.GetComponent<Renderer>();
                    Assert.IsNotNull(renderer);
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    var tinted = block.GetColor("_BaseColor");
                    Assert.AreEqual(color.r, tinted.r, 0.001f);
                    Assert.AreEqual(color.g, tinted.g, 0.001f);
                    Assert.AreEqual(color.b, tinted.b, 0.001f);
                    accents++;
                }

                Assert.GreaterOrEqual(accents, 1);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BugMeleePrefab_HasTeamAccent()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Bug, UnitRole.Melee, out var prefab));
            Assert.GreaterOrEqual(UnitVisualAccent.CountAccents(prefab.transform), 1,
                "Bug_Melee should include at least one TeamAccent carapace mesh.");
        }
    }

    public sealed class UnitCombatAnimatorDriverTests
    {
        [Test]
        public void ResolveSpeed_MoveAndChase_AreWalking()
        {
            Assert.AreEqual(1f, UnitCombatAnimatorDriver.ResolveSpeed(UnitBehaviorState.Move));
            Assert.AreEqual(1f, UnitCombatAnimatorDriver.ResolveSpeed(UnitBehaviorState.Chase));
        }

        [Test]
        public void ResolveSpeed_Attack_IsStanding()
        {
            Assert.AreEqual(0f, UnitCombatAnimatorDriver.ResolveSpeed(UnitBehaviorState.Attack));
        }
    }

    public sealed class MatchCombatPresenterTests
    {
        [Test]
        public void SyncNow_MeleeStrike_DoesNotOffsetModelLocalPosition()
        {
            MatchPickLayers.InitializeFromName();
            Editor.RaceContentBuilder.EnsureContent();
            UnitVisualPrefabBuilder.EnsureContent();

            var catalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            var raceCatalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(Editor.RaceContentBuilder.CatalogPath);

            var root = new GameObject("MatchCombatPresenterTest");
            try
            {
                var runtime = root.AddComponent<MatchRuntime>();
                var soRuntime = new SerializedObject(runtime);
                soRuntime.FindProperty("_raceCatalog").objectReferenceValue = raceCatalog;
                soRuntime.ApplyModifiedPropertiesWithoutUndo();

                root.AddComponent<MatchSelectionBridge>();
                var presenter = root.AddComponent<MatchCombatPresenter>();
                var soPresenter = new SerializedObject(presenter);
                soPresenter.FindProperty("_runtime").objectReferenceValue = runtime;
                soPresenter.FindProperty("_visualCatalog").objectReferenceValue = catalog;
                soPresenter.ApplyModifiedPropertiesWithoutUndo();

                runtime.StartMatch(
                    new[] { GameIds.Races.Human, GameIds.Races.Bug, GameIds.Races.Slot3, GameIds.Races.Slot4 },
                    localPlayerSlot: 0);

                var stats = new UnitCombatStats(
                    UnitRole.Melee,
                    maxHp: 100f,
                    armor: 0f,
                    damageMin: 10f,
                    damageMax: 10f,
                    attackSpeed: 1f,
                    attackRange: 1.5f,
                    moveSpeed: 4f,
                    goldBounty: 1);
                var attacker = runtime.Controller.Combat.SpawnUnit(
                    0, GameIds.Lanes.Center, UnitRole.Melee, stats, distanceAlongLane: 8f);
                var target = runtime.Controller.Combat.SpawnUnit(
                    2, GameIds.Lanes.Center, UnitRole.Melee, stats, distanceAlongLane: 10f);

                presenter.SyncNow();

                Assert.IsTrue(presenter.TryGetUnitGroundRing(attacker.UnitId, out _, out _));

                var combatUnits = root.transform.Find("CombatUnits");
                Assert.IsNotNull(combatUnits);
                Transform model = null;
                foreach (Transform child in combatUnits)
                {
                    if (child.name.Contains(attacker.UnitId.ToString()))
                    {
                        model = child.GetChild(0);
                        break;
                    }
                }

                Assert.IsNotNull(model);
                model.localPosition = new Vector3(0.4f, 0f, 0.2f);

                var strikeField = typeof(MatchCombatSystem).GetField(
                    "_meleeStrikes",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(strikeField);
                var strikes = (System.Collections.IList)strikeField.GetValue(runtime.Controller.Combat);
                strikes.Add(new CombatMeleeStrikeState(attacker.UnitId, target.UnitId, 10f, 0.14f)
                {
                    TimeRemaining = 0.07f,
                });

                presenter.SyncNow();
                Assert.AreEqual(Vector3.zero, model.localPosition);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
