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
        const string LegacyHumanHeroPath = "Assets/Game/Prefabs/Races/Humans/Heroes/Human_Hero.prefab";
        public const string ControllersFolder = "Assets/Game/Prefabs/Races/Humans/Units/Controllers";
        public const string HeroControllersFolder = "Assets/Game/Prefabs/Races/Humans/Heroes/Controllers";

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
                bool seedTitanStats = false)
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
            new(
                "Human_Hero1",
                UnitVisualPrefabBuilder.HumanHero1Path,
                HeroControllersFolder + "/Human_Hero1.controller",
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
                HeroControllersFolder + "/Human_Hero2.controller",
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
                HeroControllersFolder + "/Human_Hero3.controller",
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
                ControllersFolder + "/Human_Titan.controller",
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
            UnitRole role) =>
            new(
                prefabName,
                destinationPath,
                ControllersFolder + "/" + controllerName + ".controller",
                ttPrefab,
                animSubfolder,
                idleClip,
                walkClip,
                attackClips,
                castClips,
                deathClip,
                role);

        [MenuItem("BARAKI/Units/Rebuild TT Prefabs")]
        public static void RebuildFromMenu()
        {
            RebuildAll();
        }

        public static void RebuildAll()
        {
            EnsureFolder(ControllersFolder);
            EnsureFolder(HeroControllersFolder);
            EnsureFolder(UnitVisualPrefabBuilder.HumanPath);
            EnsureFolder(UnitVisualPrefabBuilder.HumanHeroesPath);
            RenameLegacyHeroPrefab();

            foreach (var setup in Setups)
            {
                DeleteStaleController(setup.ControllerPath);
                BuildPrefab(setup);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnitVisualPrefabBuilder.EnsureContent();
            Debug.Log("TtUnitVisualSetup: rebuilt " + Setups.Length + " Human TT prefabs.");
        }

        static void RenameLegacyHeroPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LegacyHumanHeroPath) == null)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(UnitVisualPrefabBuilder.HumanHero1Path) != null)
            {
                return;
            }

            var error = AssetDatabase.RenameAsset(LegacyHumanHeroPath, "Human_Hero1");
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException(
                    "Failed to rename Human_Hero.prefab to Human_Hero1: " + error);
            }
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

            settings.CopyFrom(race.GetUnit(setup.Role));
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
