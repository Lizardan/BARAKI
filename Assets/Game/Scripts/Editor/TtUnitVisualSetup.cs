using System;
using System.IO;
using Game.Core;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds Human combat unit, hero, and titan prefabs from ToonyTinyPeople TT_RTS assets:
    /// copies the matched TT prefab, points its Animator at a generated controller built
    /// from TT clips (Stand/Walk/Attack[/Cast]/Death), zeroes root transform (native TT scale, yaw 0),
    /// attaches <see cref="TtUnitTeamColor"/> with the four slot-color texture variants,
    /// and preserves or seeds <see cref="UnitCombatSettings"/>.
    /// Run via menu BARAKI/Units/Rebuild TT Prefabs.
    /// </summary>
    public static class TtUnitVisualSetup
    {
        const string TtPrefabFolder = "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/prefabs";
        const string TtAnimationRoot = "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/animation";
        const string TtTextureFolder =
            "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Units/Textures";

        /// <summary>AnimatorController path next to its prefab (role folder / Bonus).</summary>
        public static string ControllerBeside(string prefabPath, string controllerName)
        {
            var dir = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/') ?? UnitVisualPrefabBuilder.HumanPath;
            return $"{dir}/{controllerName}.controller";
        }

        static readonly string[] TeamTextureFiles =
        {
            "TT_RTS_Units_red.tga",
            "TT_RTS_Units_blue.tga",
            "TT_RTS_Units_green.tga",
            "TT_RTS_Units_yellow.tga",
        };

        readonly struct ClipRef
        {
            public readonly string Subfolder;
            public readonly string Name;

            public ClipRef(string subfolder, string name)
            {
                Subfolder = subfolder;
                Name = name;
            }
        }

        readonly struct VisualSetup
        {
            public readonly string PrefabName;
            public readonly string DestinationPath;
            public readonly string ControllerPath;
            public readonly string TtPrefab;
            public readonly string AnimSubfolder;
            public readonly string IdleClip;
            public readonly string WalkClip;
            public readonly ClipRef[] AttackClips;
            public readonly ClipRef[] CastClips;
            public readonly string DeathClip;
            public readonly UnitRole Role;
            public readonly int HeroSlot;
            public readonly bool SeedTitanStats;
            public readonly bool UseBonusDefinition;

            public VisualSetup(
                string prefabName,
                string destinationPath,
                string controllerPath,
                string ttPrefab,
                string animSubfolder,
                string idleClip,
                string walkClip,
                ClipRef[] attackClips,
                ClipRef[] castClips,
                string deathClip,
                UnitRole role,
                int heroSlot = 0,
                bool seedTitanStats = false,
                bool useBonusDefinition = false)
            {
                PrefabName = prefabName;
                DestinationPath = destinationPath;
                ControllerPath = controllerPath;
                TtPrefab = ttPrefab;
                AnimSubfolder = animSubfolder;
                IdleClip = idleClip;
                WalkClip = walkClip;
                AttackClips = attackClips ?? Array.Empty<ClipRef>();
                CastClips = castClips ?? Array.Empty<ClipRef>();
                DeathClip = deathClip;
                Role = role;
                HeroSlot = heroSlot;
                SeedTitanStats = seedTitanStats;
                UseBonusDefinition = useBonusDefinition;
            }
        }

        static ClipRef Local(string subfolder, string name) => new(subfolder, name);

        static readonly VisualSetup[] Setups =
        {
            UnitSetup("Human_Melee", UnitVisualPrefabBuilder.HumanMeleePath, "Human_Melee",
                "TT_Heavy_Infantry", "animation_infantry/Infantry",
                "infantry_01_idle", "infantry_03_run",
                new[]
                {
                    Local("animation_infantry/Infantry", "infantry_04_attack_A"),
                    Local("animation_infantry/Infantry", "infantry_04_attack_B"),
                },
                null,
                "infantry_06_death_A",
                UnitRole.Melee),
            UnitSetup("Human_Ranged", UnitVisualPrefabBuilder.HumanRangedPath, "Human_Ranged",
                "TT_Archer", "animation_infantry/Archer",
                "archer_01_idle", "archer_03_run",
                new[]
                {
                    Local("animation_infantry/Archer", "archer_04_attack_A"),
                    Local("animation_infantry/Archer", "archer_04_attack_B"),
                },
                null,
                "archer_06_death_A",
                UnitRole.Ranged),
            UnitSetup("Human_Caster", UnitVisualPrefabBuilder.HumanCasterPath, "Human_Caster",
                "TT_Mage", "animation_infantry/Staff",
                "staff_01_idle", "staff_03_run",
                // Only attack_B: attack_A is the off-hand sword swing on the dual-wield mage.
                new[]
                {
                    Local("animation_infantry/Staff", "staff_04_attack_B"),
                },
                new[]
                {
                    Local("animation_infantry/Staff", "staff_07_cast_A"),
                    Local("animation_infantry/Staff", "staff_07_cast_B"),
                },
                "staff_06_death_A",
                UnitRole.Caster),
            UnitSetup("Human_Siege", UnitVisualPrefabBuilder.HumanSiegePath, "Human_Siege",
                "TT_Mounted_Knight", "animation_cavalry/cavalry_spear_A",
                "cav_spear_A_01_idle", "cav_spear_A_03_run",
                new[] { Local("animation_cavalry/cavalry_spear_A", "cav_spear_A_04_attack") },
                null,
                "cav_spear_A_06_death_A",
                UnitRole.Siege),
            UnitSetup("Human_Flying", UnitVisualPrefabBuilder.HumanFlyingPath, "Human_Flying",
                "Fly_Hors", "animation_cavalry/cavalry",
                "cavalry_01_idle", "cavalry_03_run",
                new[] { Local("animation_cavalry/cavalry", "cavalry_04_attack") },
                null,
                "cavalry_06_death_A",
                UnitRole.Flying),
            UnitSetup("Human_Super", UnitVisualPrefabBuilder.HumanSuperPath, "Human_Super",
                "machines/TT_Ballista_lvl3", "animation_machines/Ballista",
                "ballista_01_idle", "ballista_02_move",
                new[] { Local("animation_machines/Ballista", "ballista_03_attack") },
                null,
                "ballista_05_death",
                UnitRole.Super),
            UnitSetup("Human_Melee_BONUS", UnitVisualPrefabBuilder.HumanMeleeBonusPath, "Human_Melee_BONUS",
                "TT_Halberdier", "animation_infantry/Polearm",
                "polearm_01_idle", "polearm_03_run",
                new[]
                {
                    Local("animation_infantry/Polearm", "polearm_04_attack_A"),
                    Local("animation_infantry/Polearm", "polearm_04_attack_B"),
                },
                null,
                "polearm_06_death_A",
                UnitRole.Melee,
                useBonusDefinition: true),
            UnitSetup("Human_Ranged_BONUS", UnitVisualPrefabBuilder.HumanRangedBonusPath, "Human_Ranged_BONUS",
                "TT_Crossbowman", "animation_infantry/Crossbow",
                "crossbow_01_idle", "crossbow_03_run",
                new[]
                {
                    Local("animation_infantry/Crossbow", "crossbow_04_attack_A"),
                    Local("animation_infantry/Crossbow", "crossbow_04_attack_B"),
                },
                null,
                "crossbow_06_death_A",
                UnitRole.Ranged,
                useBonusDefinition: true),
            UnitSetup("Human_Caster_BONUS", UnitVisualPrefabBuilder.HumanCasterBonusPath, "Human_Caster_BONUS",
                "TT_HighPriest", "animation_infantry/Staff",
                "staff_01_idle", "staff_03_run",
                new[]
                {
                    // AttackVariant 0 = ranged staff, 1 = hybrid melee mace (infantry swing).
                    Local("animation_infantry/Staff", "staff_04_attack_B"),
                    Local("animation_infantry/Infantry", "infantry_04_attack_A"),
                },
                new[]
                {
                    Local("animation_infantry/Staff", "staff_07_cast_A"),
                    Local("animation_infantry/Staff", "staff_07_cast_B"),
                },
                "staff_06_death_A",
                UnitRole.Caster,
                useBonusDefinition: true),
            // Foot Paladin — infantry Shield, not cavalry_shield (mounted clips tip the rig sideways).
            // shield_04_attack_B.FBX internal clip is misnamed "shield_attack_B" — use A only.
            UnitSetup("Human_Siege_BONUS", UnitVisualPrefabBuilder.HumanSiegeBonusPath, "Human_Siege_BONUS",
                "TT_Paladin", "animation_infantry/Shield",
                "shield_01_idle", "shield_03_run",
                new[] { Local("animation_infantry/Shield", "shield_04_attack_A") },
                null,
                "shield_06_death_A",
                UnitRole.Siege,
                useBonusDefinition: true),
            // Horse archer — cavalry_archer bow set, not bare horse cavalry.
            UnitSetup("Human_Flying_BONUS", UnitVisualPrefabBuilder.HumanFlyingBonusPath, "Human_Flying_BONUS",
                "Fly_Hors_Archer", "animation_cavalry/cavalry_archer",
                "cav_archer_01_idle", "cav_archer_03_run",
                new[] { Local("animation_cavalry/cavalry_archer", "cav_archer_04_attack") },
                null,
                "cav_archer_06_death_A",
                UnitRole.Flying,
                useBonusDefinition: true),
            UnitSetup("Human_Super_BONUS", UnitVisualPrefabBuilder.HumanSuperBonusPath, "Human_Super_BONUS",
                "machines/TT_Catapult_lvl1", "animation_machines/Catapult",
                "catapult_01_idle", "catapult_02_move",
                new[] { Local("animation_machines/Catapult", "catapult_03_attack") },
                null,
                "catapult_05_death",
                UnitRole.Super,
                useBonusDefinition: true),
            new(
                "Human_Hero1",
                UnitVisualPrefabBuilder.HumanHero1Path,
                ControllerBeside(UnitVisualPrefabBuilder.HumanHero1Path, "Human_Hero1"),
                "TT_King",
                "animation_infantry/Infantry",
                "infantry_01_idle",
                "infantry_03_run",
                new[]
                {
                    Local("animation_infantry/Infantry", "infantry_04_attack_A"),
                    Local("animation_infantry/Infantry", "infantry_04_attack_B"),
                },
                new[]
                {
                    Local("animation_infantry/Staff", "staff_07_cast_A"),
                    Local("animation_infantry/Staff", "staff_07_cast_B"),
                },
                "infantry_06_death_A",
                UnitRole.Hero,
                heroSlot: 1),
            new(
                "Human_Hero2",
                UnitVisualPrefabBuilder.HumanHero2Path,
                ControllerBeside(UnitVisualPrefabBuilder.HumanHero2Path, "Human_Hero2"),
                "TT_Mounted_Paladin",
                "animation_cavalry/cavalry_shield",
                "cav_shield_01_idle",
                "cav_shield_03_run",
                new[] { Local("animation_cavalry/cavalry_shield", "cav_shield_04_attack") },
                new[]
                {
                    Local("animation_cavalry/cavalry_staff", "cav_staff_07_cast_A"),
                    Local("animation_cavalry/cavalry_staff", "cav_staff_07_cast_B"),
                },
                "cav_shield_06_death_A",
                UnitRole.Hero,
                heroSlot: 2),
            new(
                "Human_Hero3",
                UnitVisualPrefabBuilder.HumanHero3Path,
                ControllerBeside(UnitVisualPrefabBuilder.HumanHero3Path, "Human_Hero3"),
                "TT_Mounted_Priest",
                "animation_cavalry/cavalry_staff",
                "cav_staff_01_idle",
                "cav_staff_03_run",
                new[] { Local("animation_cavalry/cavalry_staff", "cav_staff_04_attack") },
                new[]
                {
                    Local("animation_cavalry/cavalry_staff", "cav_staff_07_cast_A"),
                    Local("animation_cavalry/cavalry_staff", "cav_staff_07_cast_B"),
                },
                "cav_staff_06_death_A",
                UnitRole.Hero,
                heroSlot: 3),
            new(
                "Human_Titan",
                UnitVisualPrefabBuilder.HumanTitanPath,
                ControllerBeside(UnitVisualPrefabBuilder.HumanTitanPath, "Human_Titan"),
                "TT_Peasant",
                "animation_infantry/Infantry",
                "infantry_01_idle",
                "infantry_03_run",
                new[]
                {
                    Local("animation_infantry/Infantry", "infantry_04_attack_A"),
                    Local("animation_infantry/Infantry", "infantry_04_attack_B"),
                },
                new[]
                {
                    Local("animation_infantry/Infantry", "infantry_07_punch_A"),
                    Local("animation_infantry/Infantry", "infantry_07_punch_B"),
                },
                "infantry_06_death_A",
                UnitRole.Titan,
                seedTitanStats: true),
        };

        static VisualSetup UnitSetup(
            string prefabName,
            string destinationPath,
            string controllerName,
            string ttPrefab,
            string animSubfolder,
            string idleClip,
            string walkClip,
            ClipRef[] attackClips,
            ClipRef[] castClips,
            string deathClip,
            UnitRole role,
            bool useBonusDefinition = false) =>
            new(
                prefabName,
                destinationPath,
                ControllerBeside(destinationPath, controllerName),
                ttPrefab,
                animSubfolder,
                idleClip,
                walkClip,
                attackClips,
                castClips,
                deathClip,
                role,
                useBonusDefinition: useBonusDefinition);

        [MenuItem("BARAKI/Units/Rebuild TT Prefabs")]
        public static void RebuildFromMenu()
        {
            RebuildAll();
        }

        public static void RebuildAll()
        {
            UnitVisualPrefabBuilder.EnsureHumanPrefabFolders();

            foreach (var setup in Setups)
            {
                var dir = Path.GetDirectoryName(setup.ControllerPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir))
                {
                    ContentAssetPaths.EnsureFolder(dir);
                }

                DeleteStaleController(setup.ControllerPath);
                BuildPrefab(setup);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnitVisualPrefabBuilder.EnsureContent();
            Debug.Log("TtUnitVisualSetup: rebuilt " + Setups.Length + " Human TT prefabs.");
        }

        static void DeleteStaleController(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        static void BuildPrefab(VisualSetup setup)
        {
            UnitCombatSettings preserved = null;
            try
            {
                preserved = CaptureBalance(setup.DestinationPath);
                var controller = GetOrCreateController(setup.ControllerPath, setup);

                var root = PrefabUtility.LoadPrefabContents(TtPrefabFolder + "/" + setup.TtPrefab + ".prefab");
                try
                {
                    root.name = setup.PrefabName;
                    root.transform.localScale = Vector3.one;
                    root.transform.localRotation = Quaternion.identity;
                    root.transform.localPosition = Vector3.zero;

                    var animator = root.GetComponentInChildren<Animator>(true);
                    if (animator == null)
                    {
                        throw new InvalidOperationException(setup.TtPrefab + " has no Animator");
                    }

                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;

                    var ttColor = root.GetComponent<TtUnitTeamColor>();
                    if (ttColor == null)
                    {
                        ttColor = root.AddComponent<TtUnitTeamColor>();
                    }

                    ttColor.TeamTextures = LoadTeamTextures();

                    var settings = root.GetComponent<UnitCombatSettings>();
                    if (settings == null)
                    {
                        settings = root.AddComponent<UnitCombatSettings>();
                    }

                    if (preserved != null)
                    {
                        settings.CopyFrom(preserved);
                    }
                    else
                    {
                        SeedBalance(settings, setup);
                    }

                    ApplyWeaponVisibility(root, setup);

                    PrefabUtility.SaveAsPrefabAsset(root, setup.DestinationPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            finally
            {
                if (preserved != null)
                {
                    UnityEngine.Object.DestroyImmediate(preserved.gameObject);
                }
            }
        }

        static UnitCombatSettings CaptureBalance(string dstPath)
        {
            var destPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(dstPath);
            var existing = destPrefab != null
                ? destPrefab.GetComponentInChildren<UnitCombatSettings>(true)
                : null;
            if (existing == null)
            {
                return null;
            }

            var temp = new GameObject("TtBalanceCapture");
            temp.hideFlags = HideFlags.HideAndDontSave;
            var copy = temp.AddComponent<UnitCombatSettings>();
            copy.CopyFrom(existing);
            return copy;
        }

        static void SeedBalance(UnitCombatSettings settings, VisualSetup setup)
        {
            var race = LoadHumanRace();
            if (race == null)
            {
                return;
            }

            if (setup.Role == UnitRole.Hero)
            {
                settings.CopyFrom(race.GetHeroBySlot(setup.HeroSlot));
                return;
            }

            if (setup.SeedTitanStats)
            {
                settings.CopyFrom(
                    race.GetHeroBySlot(1),
                    TitanRules.BaseStatMultiplier,
                    TitanRules.AttackRange);
                return;
            }

            var definition = setup.UseBonusDefinition
                ? race.GetUnitBonus(setup.Role)
                : race.GetUnit(setup.Role);
            if (definition != null)
            {
                settings.CopyFrom(definition);
            }
        }

        static RaceDefinition LoadHumanRace()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(UnitBalanceSetup.RaceCatalogPath);
            return catalog?.GetRace(GameIds.Races.Human);
        }

        static AnimatorController GetOrCreateController(string path, VisualSetup setup)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null)
            {
                SyncControllerClips(existing, setup);
                return existing;
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(UnitCombatAnimatorDriver.SpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(UnitCombatAnimatorDriver.AttackParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(UnitCombatAnimatorDriver.DeathParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(UnitCombatAnimatorDriver.AttackVariantParam, AnimatorControllerParameterType.Float);
            if (setup.CastClips.Length > 0)
            {
                controller.AddParameter(UnitCombatAnimatorDriver.CastVariantParam, AnimatorControllerParameterType.Float);
            }

            var sm = controller.layers[0].stateMachine;
            var stand = AddState(sm, UnitCombatAnimatorDriver.StandState, LoadClip(setup.IdleClip, setup.AnimSubfolder));
            var walk = AddState(sm, UnitCombatAnimatorDriver.WalkState, LoadClip(setup.WalkClip, setup.AnimSubfolder));
            var attack = AddStateWithPool(
                controller,
                sm,
                UnitCombatAnimatorDriver.AttackState,
                UnitCombatAnimatorDriver.AttackVariantParam,
                setup.AttackClips);
            var death = AddState(sm, UnitCombatAnimatorDriver.DeathState, LoadClip(setup.DeathClip, setup.AnimSubfolder));
            AnimatorState cast = null;
            if (setup.CastClips.Length > 0)
            {
                cast = AddStateWithPool(
                    controller,
                    sm,
                    UnitCombatAnimatorDriver.CastState,
                    UnitCombatAnimatorDriver.CastVariantParam,
                    setup.CastClips);
            }

            AddSpeedTransition(stand, walk, AnimatorConditionMode.Greater, 0.1f);
            AddSpeedTransition(walk, stand, AnimatorConditionMode.Less, 0.1f);
            AddTriggerTransition(stand, attack, UnitCombatAnimatorDriver.AttackParam);
            AddTriggerTransition(walk, attack, UnitCombatAnimatorDriver.AttackParam);
            AddTriggerTransition(stand, death, UnitCombatAnimatorDriver.DeathParam);
            AddTriggerTransition(walk, death, UnitCombatAnimatorDriver.DeathParam);
            AddTriggerTransition(attack, death, UnitCombatAnimatorDriver.DeathParam);
            if (cast != null)
            {
                AddTriggerTransition(cast, death, UnitCombatAnimatorDriver.DeathParam);
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void SyncControllerClips(AnimatorController controller, VisualSetup setup)
        {
            EnsureFloatParameter(controller, UnitCombatAnimatorDriver.AttackVariantParam);
            if (setup.CastClips.Length > 0)
            {
                EnsureFloatParameter(controller, UnitCombatAnimatorDriver.CastVariantParam);
            }

            var sm = controller.layers[0].stateMachine;
            var hasCast = false;
            foreach (var child in sm.states)
            {
                var state = child.state;
                if (state.name == UnitCombatAnimatorDriver.StandState)
                {
                    state.motion = LoadClip(setup.IdleClip, setup.AnimSubfolder);
                }
                else if (state.name == UnitCombatAnimatorDriver.WalkState)
                {
                    state.motion = LoadClip(setup.WalkClip, setup.AnimSubfolder);
                }
                else if (state.name == UnitCombatAnimatorDriver.AttackState)
                {
                    state.motion = BuildPoolMotion(
                        controller,
                        UnitCombatAnimatorDriver.AttackState,
                        UnitCombatAnimatorDriver.AttackVariantParam,
                        setup.AttackClips);
                }
                else if (state.name == UnitCombatAnimatorDriver.CastState)
                {
                    hasCast = true;
                    state.motion = BuildPoolMotion(
                        controller,
                        UnitCombatAnimatorDriver.CastState,
                        UnitCombatAnimatorDriver.CastVariantParam,
                        setup.CastClips);
                }
                else if (state.name == UnitCombatAnimatorDriver.DeathState)
                {
                    state.motion = LoadClip(setup.DeathClip, setup.AnimSubfolder);
                }
            }

            if (setup.CastClips.Length > 0 && !hasCast)
            {
                AddStateWithPool(
                    controller,
                    sm,
                    UnitCombatAnimatorDriver.CastState,
                    UnitCombatAnimatorDriver.CastVariantParam,
                    setup.CastClips);
            }

            EditorUtility.SetDirty(controller);
        }

        static void EnsureFloatParameter(AnimatorController controller, string name)
        {
            foreach (var parameter in controller.parameters)
            {
                if (parameter.name == name)
                {
                    return;
                }
            }

            controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            var state = sm.AddState(name);
            state.motion = clip;
            return state;
        }

        static AnimatorState AddStateWithPool(
            AnimatorController controller,
            AnimatorStateMachine sm,
            string stateName,
            string variantParam,
            ClipRef[] clips)
        {
            var state = sm.AddState(stateName);
            state.motion = BuildPoolMotion(controller, stateName, variantParam, clips);
            return state;
        }

        static Motion BuildPoolMotion(
            AnimatorController controller,
            string stateName,
            string variantParam,
            ClipRef[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                throw new InvalidOperationException(stateName + " has no clips");
            }

            if (clips.Length == 1)
            {
                return LoadClip(clips[0]);
            }

            var tree = new BlendTree
            {
                name = stateName + "Pool",
                blendParameter = variantParam,
                blendType = BlendTreeType.Simple1D,
                useAutomaticThresholds = false,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            for (var i = 0; i < clips.Length; i++)
            {
                tree.AddChild(LoadClip(clips[i]), i);
            }

            return tree;
        }

        static void AddSpeedTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.conditions = new[]
            {
                new AnimatorCondition
                {
                    parameter = UnitCombatAnimatorDriver.SpeedParam,
                    mode = mode,
                    threshold = threshold,
                },
            };
        }

        static void AddTriggerTransition(AnimatorState from, AnimatorState to, string parameter)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.conditions = new[]
            {
                new AnimatorCondition { parameter = parameter, mode = AnimatorConditionMode.If },
            };
        }

        static AnimationClip LoadClip(ClipRef clipRef) => LoadClip(clipRef.Name, clipRef.Subfolder);

        static AnimationClip LoadClip(string clipName, string animSubfolder)
        {
            var fbxPath = TtAnimationRoot + "/" + animSubfolder + "/" + clipName + ".FBX";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (obj is AnimationClip clip && clip.name == clipName)
                {
                    return clip;
                }
            }

            throw new InvalidOperationException("Missing clip " + clipName + " at " + fbxPath);
        }

        static void ApplyWeaponVisibility(GameObject root, VisualSetup setup)
        {
            // Base caster keeps staff only — TT source ships with an extra active dagger.
            if (setup.Role == UnitRole.Caster && !setup.UseBonusDefinition)
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == "w_dagger_C")
                    {
                        transform.gameObject.SetActive(false);
                    }
                }

                return;
            }

            // Bonus caster: staff + mace both visible (one per hand); only attack clip switches.
            if (setup.Role == UnitRole.Caster && setup.UseBonusDefinition)
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    var name = transform.name;
                    if (name.StartsWith("w_staff", System.StringComparison.Ordinal))
                    {
                        transform.gameObject.SetActive(name == "w_staff_C");
                    }
                    else if (name.StartsWith("w_mace", System.StringComparison.Ordinal))
                    {
                        transform.gameObject.SetActive(name == "w_mace");
                    }
                }
            }
        }

        static Texture2D[] LoadTeamTextures()
        {
            var textures = new Texture2D[TeamTextureFiles.Length];
            for (var i = 0; i < TeamTextureFiles.Length; i++)
            {
                var path = TtTextureFolder + "/" + TeamTextureFiles[i];
                textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (textures[i] == null)
                {
                    throw new InvalidOperationException("Missing team texture " + path);
                }
            }

            return textures;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
