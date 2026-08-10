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

        static readonly UnitRole[] CombatUnitRoles =
        {
            UnitRole.Melee,
            UnitRole.Ranged,
            UnitRole.Caster,
            UnitRole.Siege,
            UnitRole.Flying,
            UnitRole.Super,
        };

        [Test]
        public void AllSixPrefabs_AreAssigned()
        {
            foreach (var role in CombatUnitRoles)
            {
                Assert.IsTrue(
                    _catalog.TryGetPrefab(GameIds.Races.Human, role, out var prefab),
                    $"Missing prefab for {GameIds.Races.Human} {role}");
                Assert.IsNotNull(prefab, $"Null prefab for {GameIds.Races.Human} {role}");
            }
        }

        [Test]
        public void AllSixPortraits_AreAssigned()
        {
            UnitPortraitBaker.BakeIntoCatalog(_catalog);
            foreach (var role in CombatUnitRoles)
            {
                Assert.IsTrue(
                    _catalog.TryGetPortrait(GameIds.Races.Human, role, out var portrait),
                    $"Missing portrait for {GameIds.Races.Human} {role}");
                Assert.IsNotNull(portrait, $"Null portrait for {GameIds.Races.Human} {role}");
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
        public void HumanMeleePrefab_UsesNativeTtPose()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Melee, out var prefab));
            Assert.AreEqual(0f, prefab.transform.localEulerAngles.y, 0.1f);
            Assert.AreEqual(1f, prefab.transform.localScale.x, 0.001f);
            Assert.AreEqual(1f, prefab.transform.localScale.y, 0.001f);
            Assert.AreEqual(1f, prefab.transform.localScale.z, 0.001f);
            Assert.AreEqual(
                UnitGreyboxVisuals.FlyingHoverHeight,
                UnitGreyboxVisuals.GetModelLocalOffset(UnitRole.Flying).y,
                0.001f);
            Assert.AreEqual(0f, UnitGreyboxVisuals.GetModelLocalOffset(UnitRole.Melee).y, 0.001f);
        }

        [Test]
        public void HumanFlyingPrefab_StaysAboveGroundInCombatClips()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Flying, out var prefab));
            Assert.AreEqual(0f, prefab.transform.localEulerAngles.y, 0.1f);

            var instance = Object.Instantiate(prefab);
            try
            {
                instance.transform.position = Vector3.up * UnitGreyboxVisuals.FlyingHoverHeight;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale =
                    prefab.transform.localScale
                    * UnitGreyboxVisuals.Scale
                    * UnitGreyboxVisuals.AnimatedHumanScaleFactor;

                var animator = instance.GetComponentInChildren<Animator>();
                Assert.IsNotNull(animator);
                var controller = animator.runtimeAnimatorController;
                Assert.IsNotNull(controller);

                foreach (var clip in controller.animationClips)
                {
                    var name = clip.name.ToLowerInvariant();
                    if (!(name.Contains("stand") || name.Contains("walk") || name.Contains("attack")))
                    {
                        continue;
                    }

                    if (name.Contains("death"))
                    {
                        continue;
                    }

                    var minY = float.MaxValue;
                    const int steps = 12;
                    for (var i = 0; i <= steps; i++)
                    {
                        var t = clip.length <= 0f ? 0f : clip.length * i / steps;
                        clip.SampleAnimation(instance, t);
                        foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        {
                            if (!smr.gameObject.activeInHierarchy || smr.sharedMesh == null)
                            {
                                continue;
                            }

                            minY = Mathf.Min(minY, smr.bounds.min.y);
                        }
                    }

                    Assert.GreaterOrEqual(
                        minY,
                        0.15f,
                        $"{clip.name} should stay above ground (minY={minY})");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void HumanSuperPrefab_StaysAboveGroundInCombatClips()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Super, out var prefab));
            Assert.AreEqual(0f, prefab.transform.localEulerAngles.y, 0.1f);
            Assert.AreEqual(0f, prefab.transform.localEulerAngles.z, 0.1f);

            var root = new GameObject("SuperProbe").transform;
            root.rotation = Quaternion.LookRotation(Vector3.forward);
            var instance = Object.Instantiate(prefab, root);
            try
            {
                instance.transform.localPosition = UnitGreyboxVisuals.GetModelLocalOffset(UnitRole.Super);
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = prefab.transform.localScale;

                var animator = instance.GetComponentInChildren<Animator>();
                Assert.IsNotNull(animator);
                var controller = animator.runtimeAnimatorController;
                Assert.IsNotNull(controller);

                foreach (var clip in controller.animationClips)
                {
                    var name = clip.name.ToLowerInvariant();
                    if (!(name.Contains("idle")
                          || name.Contains("stand")
                          || name.Contains("walk")
                          || name.Contains("move")
                          || name.Contains("attack")))
                    {
                        continue;
                    }

                    if (name.Contains("death") || name.Contains("damage"))
                    {
                        continue;
                    }

                    var minY = float.MaxValue;
                    const int steps = 12;
                    for (var i = 0; i <= steps; i++)
                    {
                        var t = clip.length <= 0f ? 0f : clip.length * i / steps;
                        clip.SampleAnimation(instance, t);
                        foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        {
                            if (!smr.gameObject.activeInHierarchy || smr.sharedMesh == null)
                            {
                                continue;
                            }

                            minY = Mathf.Min(minY, smr.bounds.min.y);
                        }
                    }

                    Assert.GreaterOrEqual(
                        minY,
                        -0.05f,
                        $"{clip.name} should stay above ground (minY={minY})");
                }
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void HumanAnimatedPrefabs_KeepNativeTtScaleAndAnimator()
        {
            foreach (var role in CombatUnitRoles)
            {
                Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, role, out var humanPrefab));
                Assert.AreEqual(1f, humanPrefab.transform.localScale.x, 0.001f, $"{role} scale.x");
                Assert.AreEqual(1f, humanPrefab.transform.localScale.y, 0.001f, $"{role} scale.y");
                Assert.AreEqual(1f, humanPrefab.transform.localScale.z, 0.001f, $"{role} scale.z");
                Assert.IsNotNull(
                    humanPrefab.GetComponentInChildren<Animator>(),
                    $"{role} should keep Animator");

                var human = Object.Instantiate(humanPrefab);
                try
                {
                    human.transform.position = Vector3.zero;
                    human.transform.rotation = Quaternion.identity;
                    human.transform.localScale = humanPrefab.transform.localScale;

                    var humanHeight = MeasureBodyHeight(human);
                    Assert.Greater(humanHeight, 0.5f, $"{role} body height should be plausible");
                    Assert.Less(humanHeight, 4f, $"{role} body height should be plausible");
                }
                finally
                {
                    Object.DestroyImmediate(human);
                }
            }
        }

        /// <summary>Tallest active skinned mesh local height in world units (prefab scale applied).</summary>
        static float MeasureBodyHeight(GameObject root)
        {
            var best = 0f;
            var has = false;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr.gameObject.activeInHierarchy || smr.sharedMesh == null)
                {
                    continue;
                }

                has = true;
                var worldHeight = smr.localBounds.size.y * smr.transform.lossyScale.y;
                if (worldHeight > best)
                {
                    best = worldHeight;
                }
            }

            Assert.IsTrue(has, "Expected active skinned meshes");
            return best;
        }

        [Test]
        public void HumanAnimatedPrefabs_WalkClipsLoopWithCombatDuration()
        {
            foreach (var role in CombatUnitRoles)
            {
                Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, role, out var prefab));
                var animator = prefab.GetComponentInChildren<Animator>();
                Assert.IsNotNull(animator, $"{role} missing Animator");
                Assert.IsNotNull(animator.runtimeAnimatorController, $"{role} missing controller");

                var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                Assert.IsNotNull(controller, $"{role} controller type");

                AnimationClip walk = null;
                foreach (var state in controller.layers[0].stateMachine.states)
                {
                    if (state.state.name == "Walk")
                    {
                        walk = state.state.motion as AnimationClip;
                        break;
                    }
                }

                Assert.IsNotNull(walk, $"{role} missing Walk state clip");
                var settings = AnimationUtility.GetAnimationClipSettings(walk);
                Assert.IsTrue(settings.loopTime, $"{role} Walk must loop");
                Assert.Greater(walk.length, 0.3f, $"{role} Walk too short");
                Assert.Less(walk.length, 8f, $"{role} Walk too long (likely unscaled WC3 ms): {walk.length}");
            }
        }

        [Test]
        public void HumanMeleePrefab_HasTeamTexturesTintedBySlotColor()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Melee, out var prefab));
            Assert.GreaterOrEqual(
                UnitVisualAccent.CountAccents(prefab.transform),
                4,
                "Human_Melee should expose the four TT team-color textures.");

            var instance = Object.Instantiate(prefab);
            try
            {
                var color = new Color(0.1f, 0.4f, 0.9f, 1f);
                UnitVisualAccent.ApplyTeamColor(instance.transform, color);
                var tt = instance.GetComponentInChildren<TtUnitTeamColor>(true);
                Assert.IsNotNull(tt);
                Assert.AreEqual(4, tt.TeamTextures.Length);

                var slot = MatchPlayerColors.NearestSlotIndex(color);
                var applied = 0;
                foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!smr.gameObject.activeInHierarchy || smr.sharedMesh == null)
                    {
                        continue;
                    }

                    var block = new MaterialPropertyBlock();
                    smr.GetPropertyBlock(block);
                    var texture = block.GetTexture("_BaseMap");
                    Assert.IsNotNull(texture, "Active SMR should receive _BaseMap team texture");
                    Assert.AreEqual(
                        tt.TeamTextures[slot],
                        texture,
                        "SMR should be tinted by the nearest slot texture");
                    applied++;
                }

                Assert.GreaterOrEqual(applied, 1);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void HumanCasterPrefab_UsesTtTeamColor()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Caster, out var prefab));
            var tt = prefab.GetComponentInChildren<TtUnitTeamColor>(true);
            Assert.IsNotNull(tt, "Caster should carry TtUnitTeamColor");
            Assert.AreEqual(4, tt.TeamTextures.Length);
            Assert.GreaterOrEqual(UnitVisualAccent.CountAccents(prefab.transform), 4);

            var instance = Object.Instantiate(prefab);
            try
            {
                var color = new Color(0.9f, 0.2f, 0.1f, 1f);
                UnitVisualAccent.ApplyTeamColor(instance.transform, color);
                var slot = MatchPlayerColors.NearestSlotIndex(color);
                foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!smr.gameObject.activeInHierarchy || smr.sharedMesh == null)
                    {
                        continue;
                    }

                    var block = new MaterialPropertyBlock();
                    smr.GetPropertyBlock(block);
                    Assert.AreEqual(
                        tt.TeamTextures[slot],
                        block.GetTexture("_BaseMap"),
                        "Caster body should be tinted by red-slot texture");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void HumanSuperPrefab_HasTtTeamColorOnNativeModel()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Super, out var prefab));
            Assert.AreEqual(0f, prefab.transform.localEulerAngles.y, 0.1f);
            var tt = prefab.GetComponentInChildren<TtUnitTeamColor>(true);
            Assert.IsNotNull(tt, "Super should carry TtUnitTeamColor");
            Assert.AreEqual(4, tt.TeamTextures.Length);

            var instance = Object.Instantiate(prefab);
            try
            {
                var color = new Color(0.1f, 0.8f, 0.2f, 1f);
                UnitVisualAccent.ApplyTeamColor(instance.transform, color);
                var slot = MatchPlayerColors.NearestSlotIndex(color);
                foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!smr.gameObject.activeInHierarchy || smr.sharedMesh == null)
                    {
                        continue;
                    }

                    var block = new MaterialPropertyBlock();
                    smr.GetPropertyBlock(block);
                    Assert.AreEqual(
                        tt.TeamTextures[slot],
                        block.GetTexture("_BaseMap"),
                        "Super body should be tinted by green-slot texture");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void HumanMeleePrefab_HasTeamAccent()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Melee, out var prefab));
            Assert.GreaterOrEqual(UnitVisualAccent.CountAccents(prefab.transform), 4,
                "Human_Melee should expose the four TT team-color textures.");
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

        [Test]
        public void ResolveLocomotionState_UsesWalkForMoveAndStandOtherwise()
        {
            Assert.AreEqual(UnitCombatAnimatorDriver.WalkState, UnitCombatAnimatorDriver.ResolveLocomotionState(UnitBehaviorState.Move));
            Assert.AreEqual(UnitCombatAnimatorDriver.WalkState, UnitCombatAnimatorDriver.ResolveLocomotionState(UnitBehaviorState.Chase));
            Assert.AreEqual(UnitCombatAnimatorDriver.StandState, UnitCombatAnimatorDriver.ResolveLocomotionState(UnitBehaviorState.Attack));
        }

        [Test]
        public void ResolveDesiredState_AttackStatusBlendsToAttackImmediately()
        {
            Assert.AreEqual(
                UnitCombatAnimatorDriver.AttackState,
                UnitCombatAnimatorDriver.ResolveDesiredState(
                    UnitBehaviorState.Attack,
                    fireAttack: false,
                    fireDeath: false,
                    isDead: false));
        }

        [Test]
        public void ResolveDesiredState_MoveLeavesAttackWithoutWaiting()
        {
            Assert.AreEqual(
                UnitCombatAnimatorDriver.WalkState,
                UnitCombatAnimatorDriver.ResolveDesiredState(
                    UnitBehaviorState.Move,
                    fireAttack: false,
                    fireDeath: false,
                    isDead: false));
        }

        [Test]
        public void ResolveDesiredState_FireAttackOverridesLocomotion()
        {
            Assert.AreEqual(
                UnitCombatAnimatorDriver.AttackState,
                UnitCombatAnimatorDriver.ResolveDesiredState(
                    UnitBehaviorState.Chase,
                    fireAttack: true,
                    fireDeath: false,
                    isDead: false));
        }

        [Test]
        public void ShouldForceRestartAttack_OnlyOnNewSwing()
        {
            Assert.IsTrue(UnitCombatAnimatorDriver.ShouldForceRestartAttack(
                fireAttack: true,
                desiredState: UnitCombatAnimatorDriver.AttackState));
            Assert.IsFalse(UnitCombatAnimatorDriver.ShouldForceRestartAttack(
                fireAttack: false,
                desiredState: UnitCombatAnimatorDriver.AttackState));
            Assert.IsFalse(UnitCombatAnimatorDriver.ShouldForceRestartAttack(
                fireAttack: true,
                desiredState: UnitCombatAnimatorDriver.WalkState));
        }

        [Test]
        public void IsInStateOrTransitioningTo_NullAnimator_ReturnsFalse()
        {
            Assert.IsFalse(UnitCombatAnimatorDriver.IsInStateOrTransitioningTo(null, UnitCombatAnimatorDriver.WalkState));
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
                    new[] { GameIds.Races.Human, GameIds.Races.Human, GameIds.Races.Slot3, GameIds.Races.Slot4 },
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
                  var strikes = (CombatMeleeStrikeSystem)strikeField.GetValue(runtime.Controller.Combat);
                  strikes.Spawn(new CombatMeleeStrikeState(attacker.UnitId, target.UnitId, 10f, 0.14f)
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
