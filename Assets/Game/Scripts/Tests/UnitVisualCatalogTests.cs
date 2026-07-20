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
        public void AllTwelvePrefabs_AreAssigned()
        {
            foreach (var raceId in new[] { GameIds.Races.Human, GameIds.Races.Bug })
            {
                foreach (var role in CombatUnitRoles)
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
                foreach (var role in CombatUnitRoles)
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
            Assert.AreEqual(0.8f, UnitGreyboxVisuals.GetAnimatedHumanRoleScale(UnitRole.Melee), 0.001f);
            Assert.AreEqual(1.6f, UnitGreyboxVisuals.GetAnimatedHumanRoleScale(UnitRole.Caster), 0.001f);
            Assert.AreEqual(
                UnitGreyboxVisuals.FlyingHoverHeight,
                UnitGreyboxVisuals.GetModelLocalOffset(UnitRole.Flying).y,
                0.001f);
            Assert.AreEqual(0f, UnitGreyboxVisuals.GetModelLocalOffset(UnitRole.Melee).y, 0.001f);
        }

        [Test]
        public void HumanFlyingPrefab_UsesYaw90AndStaysAboveGroundInCombatClips()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Flying, out var prefab));
            Assert.AreEqual(
                UnitGreyboxVisuals.AnimatedHumanFlyingModelYawDegrees,
                prefab.transform.localEulerAngles.y,
                0.1f);
            Assert.AreEqual(
                90f,
                UnitGreyboxVisuals.GetAnimatedHumanModelYawDegrees(UnitRole.Flying),
                0.001f);

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
        public void HumanSuperPrefab_CorrectsBackwardLeanAndStaysAboveGround()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Super, out var prefab));
            var expected = UnitGreyboxVisuals.GetAnimatedHumanModelEuler(UnitRole.Super);
            Assert.AreEqual(expected.y, prefab.transform.localEulerAngles.y, 0.1f);
            Assert.AreEqual(expected.z, prefab.transform.localEulerAngles.z, 0.1f);

            var root = new GameObject("SuperLeanProbe").transform;
            root.rotation = Quaternion.LookRotation(Vector3.forward);
            var instance = Object.Instantiate(prefab, root);
            try
            {
                instance.transform.localPosition = UnitGreyboxVisuals.GetModelLocalOffset(UnitRole.Super);
                instance.transform.localRotation = Quaternion.Euler(expected);
                instance.transform.localScale =
                    prefab.transform.localScale
                    * UnitGreyboxVisuals.Scale
                    * UnitGreyboxVisuals.AnimatedHumanScaleFactor;

                var animator = instance.GetComponentInChildren<Animator>();
                Assert.IsNotNull(animator);
                var walk = System.Array.Find(
                    animator.runtimeAnimatorController.animationClips,
                    c => c.name.IndexOf("Walk", System.StringComparison.OrdinalIgnoreCase) >= 0);
                Assert.IsNotNull(walk);

                walk.SampleAnimation(instance, walk.length * 0.3f);
                var head = FindChild(instance.transform, "Bone_Head");
                var pelvis = FindChild(instance.transform, "Bone_Pelvis");
                Assert.IsNotNull(head);
                Assert.IsNotNull(pelvis);

                var torso = (head.position - pelvis.position).normalized;
                var leanBack = -Vector3.Dot(torso, Vector3.forward);
                Assert.LessOrEqual(
                    Mathf.Abs(leanBack),
                    0.06f,
                    $"Super Walk leanBack={leanBack:F3} should be near upright");

                var minY = float.MaxValue;
                foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!smr.gameObject.activeInHierarchy || smr.sharedMesh == null)
                    {
                        continue;
                    }

                    minY = Mathf.Min(minY, smr.bounds.min.y);
                }

                Assert.GreaterOrEqual(minY, -0.05f, $"Super should not sink under ground (minY={minY})");
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        [Test]
        public void HumanAnimatedPrefabs_AreNormalizedNearGreyboxHeight()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Bug, UnitRole.Melee, out var bugPrefab));
            var bug = Object.Instantiate(bugPrefab);
            try
            {
                bug.transform.position = Vector3.zero;
                bug.transform.rotation = Quaternion.identity;
                bug.transform.localScale = Vector3.one * UnitGreyboxVisuals.Scale;
                var bugHeight = MeasureActiveRendererHeight(bug);

                float minHuman = float.MaxValue;
                float maxHuman = 0f;
                foreach (var role in CombatUnitRoles)
                {
                    Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, role, out var humanPrefab));
                    var human = Object.Instantiate(humanPrefab);
                    try
                    {
                        human.transform.position = Vector3.zero;
                        human.transform.rotation = Quaternion.identity;
                        human.transform.localScale =
                            humanPrefab.transform.localScale
                            * UnitGreyboxVisuals.Scale
                            * UnitGreyboxVisuals.AnimatedHumanScaleFactor;

                        var humanHeight = MeasureBodyHeight(human);
                        var expectedHeight = bugHeight * UnitGreyboxVisuals.GetAnimatedHumanRoleScale(role);
                        Assert.AreEqual(
                            expectedHeight,
                            humanHeight,
                            expectedHeight * 0.08f,
                            $"{role} body height {humanHeight} should be near {expectedHeight}");
                        Assert.IsNotNull(
                            human.GetComponentInChildren<Animator>(),
                            $"{role} should keep Animator");

                        if (Mathf.Abs(UnitGreyboxVisuals.GetAnimatedHumanRoleScale(role) - 1f) > 0.01f)
                        {
                            continue;
                        }

                        if (humanHeight < minHuman)
                        {
                            minHuman = humanHeight;
                        }

                        if (humanHeight > maxHuman)
                        {
                            maxHuman = humanHeight;
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(human);
                    }
                }

                Assert.AreEqual(
                    maxHuman,
                    minHuman,
                    bugHeight * 0.05f,
                    $"Baseline Human body height span {minHuman}..{maxHuman} should stay within 5%");
            }
            finally
            {
                Object.DestroyImmediate(bug);
            }
        }

        static float MeasureActiveRendererHeight(GameObject root)
        {
            var enc = new Bounds();
            var has = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!has)
                {
                    enc = renderer.bounds;
                    has = true;
                }
                else
                {
                    enc.Encapsulate(renderer.bounds);
                }
            }

            Assert.IsTrue(has, "Expected active renderers");
            return enc.size.y;
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
                    if (!UnitVisualAccent.IsTeamColorTarget(t.name))
                    {
                        continue;
                    }

                    var renderer = t.GetComponent<Renderer>();
                    Assert.IsNotNull(renderer);
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    var tinted = block.GetColor("_BaseColor");
                    Assert.AreEqual(color.r, tinted.r, 0.001f, t.name);
                    Assert.AreEqual(color.g, tinted.g, 0.001f, t.name);
                    Assert.AreEqual(color.b, tinted.b, 0.001f, t.name);
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
        public void HumanCasterPrefab_CloakIsTeamTinted()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Caster, out var prefab));
            var cloak = FindChild(prefab.transform, "TeamTint_0");
            Assert.IsNotNull(cloak, "Caster cloak geoset should be TeamTint_0 (Arthas robe)");
            var cloakRenderer = cloak.GetComponent<SkinnedMeshRenderer>();
            Assert.IsNotNull(cloakRenderer);
            Assert.IsNotNull(cloakRenderer.sharedMaterial);
            Assert.AreEqual(
                "Mat_Arthas_TeamCloak",
                cloakRenderer.sharedMaterial.name,
                "Cloak should use team-color diffuse (gold cloth → slot color)");
            Assert.IsNull(
                FindChild(prefab.transform, "TeamTint_2"),
                "Head/banditMage geoset must not be forced as cloak tint");

            var instance = Object.Instantiate(prefab);
            try
            {
                var color = new Color(0.9f, 0.2f, 0.1f, 1f);
                UnitVisualAccent.ApplyTeamColor(instance.transform, color);
                var tintedCloak = FindChild(instance.transform, "TeamTint_0");
                var block = new MaterialPropertyBlock();
                tintedCloak.GetComponent<Renderer>().GetPropertyBlock(block);
                Assert.AreEqual(color.r, block.GetColor("_BaseColor").r, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void HumanSuperPrefab_HoodIsTeamAccentWithoutFallbackPrimitives()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Super, out var prefab));
            var hood = FindChild(prefab.transform, "TeamAccent_3");
            Assert.IsNotNull(hood, "Super hood geoset should be TeamAccent_3");
            Assert.IsNotNull(hood.GetComponent<SkinnedMeshRenderer>());
            Assert.IsNull(
                FindChild(prefab.transform, "TeamAccent_Cape"),
                "Super should use real hood mesh, not fallback Cape primitive");
            Assert.IsNull(FindChild(prefab.transform, "TeamAccent_Plume"));

            var instance = Object.Instantiate(prefab);
            try
            {
                var color = new Color(0.1f, 0.8f, 0.2f, 1f);
                UnitVisualAccent.ApplyTeamColor(instance.transform, color);
                var tintedHood = FindChild(instance.transform, "TeamAccent_3");
                var block = new MaterialPropertyBlock();
                tintedHood.GetComponent<Renderer>().GetPropertyBlock(block);
                Assert.AreEqual(color.g, block.GetColor("_BaseColor").g, 0.001f);
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
