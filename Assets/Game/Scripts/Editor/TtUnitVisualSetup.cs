using System;
using System.IO;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds the six Human combat unit prefabs from ToonyTinyPeople TT_RTS assets:
    /// copies the matched TT prefab, points its Animator at a generated controller built
    /// from TT clips (Stand/Walk/Attack/Death), zeroes root transform (native TT scale, yaw 0),
    /// and attaches <see cref="TtUnitTeamColor"/> with the four slot-color texture variants.
    /// Run via menu BARAKI/Units/Rebuild TT Prefabs.
    /// </summary>
    public static class TtUnitVisualSetup
    {
        const string TtPrefabFolder = "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/prefabs";
        const string TtAnimationRoot = "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/animation";
        const string TtTextureFolder =
            "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Units/Textures";
        public const string ControllersFolder = "Assets/Game/Prefabs/Races/Humans/Units/Controllers";

        static readonly string[] TeamTextureFiles =
        {
            "TT_RTS_Units_red.tga",
            "TT_RTS_Units_blue.tga",
            "TT_RTS_Units_green.tga",
            "TT_RTS_Units_yellow.tga",
        };

        readonly struct RoleSetup
        {
            public readonly UnitRole Role;
            public readonly string TtPrefab;
            public readonly string AnimSubfolder;
            public readonly string IdleClip;
            public readonly string WalkClip;
            public readonly string AttackClip;
            public readonly string DeathClip;

            public RoleSetup(
                UnitRole role, string ttPrefab, string animSubfolder,
                string idleClip, string walkClip, string attackClip, string deathClip)
            {
                Role = role;
                TtPrefab = ttPrefab;
                AnimSubfolder = animSubfolder;
                IdleClip = idleClip;
                WalkClip = walkClip;
                AttackClip = attackClip;
                DeathClip = deathClip;
            }
        }

        static readonly RoleSetup[] RoleSetups =
        {
            new(UnitRole.Melee, "TT_Heavy_Infantry", "animation_infantry/Infantry", "infantry_01_idle", "infantry_03_run", "infantry_04_attack_A", "infantry_06_death_A"),
            new(UnitRole.Ranged, "TT_Archer", "animation_infantry/Archer", "archer_01_idle", "archer_03_run", "archer_04_attack_A", "archer_06_death_A"),
            new(UnitRole.Caster, "TT_Mage", "animation_infantry/Staff", "staff_01_idle", "staff_03_run", "staff_04_attack_A", "staff_06_death_A"),
            new(UnitRole.Siege, "TT_Mounted_Knight", "animation_cavalry/cavalry_spear_A", "cav_spear_A_01_idle", "cav_spear_A_03_run", "cav_spear_A_04_attack", "cav_spear_A_06_death_A"),
            new(UnitRole.Flying, "Fly_Hors", "animation_cavalry/cavalry", "cavalry_01_idle", "cavalry_03_run", "cavalry_04_attack", "cavalry_06_death_A"),
            new(UnitRole.Super, "machines/TT_Ballista_lvl3", "animation_machines/Ballista", "ballista_01_idle", "ballista_02_move", "ballista_03_attack", "ballista_05_death"),
        };

        [MenuItem("BARAKI/Units/Rebuild TT Prefabs")]
        public static void RebuildFromMenu()
        {
            RebuildAll();
        }

        public static void RebuildAll()
        {
            EnsureFolder(ControllersFolder);
            EnsureFolder(UnitVisualPrefabBuilder.HumanPath);

            foreach (var setup in RoleSetups)
            {
                DeleteStaleController(setup.Role);
                BuildPrefab(setup);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnitVisualPrefabBuilder.EnsureContent();
            Debug.Log("TtUnitVisualSetup: rebuilt " + RoleSetups.Length + " Human TT prefabs.");
        }

        static void DeleteStaleController(UnitRole role)
        {
            var path = GetControllerPath(role);
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        static void BuildPrefab(RoleSetup setup)
        {
            var dstPath = GetDestinationPath(setup.Role);
            var controllerPath = GetControllerPath(setup.Role);
            var controller = GetOrCreateController(controllerPath, setup);

            var root = PrefabUtility.LoadPrefabContents(TtPrefabFolder + "/" + setup.TtPrefab + ".prefab");
            try
            {
                root.name = "Human_" + setup.Role;
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

                PrefabUtility.SaveAsPrefabAsset(root, dstPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static AnimatorController GetOrCreateController(string path, RoleSetup setup)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null)
            {
                return existing;
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(UnitCombatAnimatorDriver.SpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(UnitCombatAnimatorDriver.AttackParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(UnitCombatAnimatorDriver.DeathParam, AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            var stand = AddState(sm, UnitCombatAnimatorDriver.StandState, LoadClip(setup, setup.IdleClip));
            var walk = AddState(sm, UnitCombatAnimatorDriver.WalkState, LoadClip(setup, setup.WalkClip));
            var attack = AddState(sm, UnitCombatAnimatorDriver.AttackState, LoadClip(setup, setup.AttackClip));
            var death = AddState(sm, UnitCombatAnimatorDriver.DeathState, LoadClip(setup, setup.DeathClip));

            AddSpeedTransition(stand, walk, AnimatorConditionMode.Greater, 0.1f);
            AddSpeedTransition(walk, stand, AnimatorConditionMode.Less, 0.1f);
            AddTriggerTransition(stand, attack, UnitCombatAnimatorDriver.AttackParam);
            AddTriggerTransition(walk, attack, UnitCombatAnimatorDriver.AttackParam);
            AddTriggerTransition(stand, death, UnitCombatAnimatorDriver.DeathParam);
            AddTriggerTransition(walk, death, UnitCombatAnimatorDriver.DeathParam);
            AddTriggerTransition(attack, death, UnitCombatAnimatorDriver.DeathParam);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            var state = sm.AddState(name);
            state.motion = clip;
            return state;
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

        static AnimationClip LoadClip(RoleSetup setup, string clipName)
        {
            var fbxPath = TtAnimationRoot + "/" + setup.AnimSubfolder + "/" + clipName + ".FBX";
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

        static string GetDestinationPath(UnitRole role) =>
            role switch
            {
                UnitRole.Melee => UnitVisualPrefabBuilder.HumanMeleePath,
                UnitRole.Ranged => UnitVisualPrefabBuilder.HumanRangedPath,
                UnitRole.Caster => UnitVisualPrefabBuilder.HumanCasterPath,
                UnitRole.Siege => UnitVisualPrefabBuilder.HumanSiegePath,
                UnitRole.Flying => UnitVisualPrefabBuilder.HumanFlyingPath,
                UnitRole.Super => UnitVisualPrefabBuilder.HumanSuperPath,
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
            };

        static string GetControllerPath(UnitRole role) =>
            ControllersFolder + "/Human_" + role + ".controller";

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
