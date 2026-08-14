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
    /// from TT clips (Stand/Walk/Attack/Death), zeroes root transform (native TT scale, yaw 0),
    /// attaches <see cref="TtUnitTeamColor"/> with the four slot-color texture variants,
    /// and preserves or seeds <see cref="UnitBalanceSettings"/>.
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

        readonly struct VisualSetup
        {
            public readonly string PrefabName;
            public readonly string DestinationPath;
            public readonly string ControllerPath;
            public readonly string TtPrefab;
            public readonly string AnimSubfolder;
            public readonly string IdleClip;
            public readonly string WalkClip;
            public readonly string AttackClip;
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
                string attackClip,
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
                AttackClip = attackClip;
                DeathClip = deathClip;
                Role = role;
                HeroSlot = heroSlot;
                SeedTitanStats = seedTitanStats;
            }
        }

        static readonly VisualSetup[] Setups =
        {
            UnitSetup("Human_Melee", UnitVisualPrefabBuilder.HumanMeleePath, "Human_Melee",
                "TT_Heavy_Infantry", "animation_infantry/Infantry",
                "infantry_01_idle", "infantry_03_run", "infantry_04_attack_A", "infantry_06_death_A",
                UnitRole.Melee),
            UnitSetup("Human_Ranged", UnitVisualPrefabBuilder.HumanRangedPath, "Human_Ranged",
                "TT_Archer", "animation_infantry/Archer",
                "archer_01_idle", "archer_03_run", "archer_04_attack_A", "archer_06_death_A",
                UnitRole.Ranged),
            UnitSetup("Human_Caster", UnitVisualPrefabBuilder.HumanCasterPath, "Human_Caster",
                "TT_Mage", "animation_infantry/Staff",
                "staff_01_idle", "staff_03_run", "staff_07_cast_A", "staff_06_death_A",
                UnitRole.Caster),
            UnitSetup("Human_Siege", UnitVisualPrefabBuilder.HumanSiegePath, "Human_Siege",
                "TT_Mounted_Knight", "animation_cavalry/cavalry_spear_A",
                "cav_spear_A_01_idle", "cav_spear_A_03_run", "cav_spear_A_04_attack", "cav_spear_A_06_death_A",
                UnitRole.Siege),
            UnitSetup("Human_Flying", UnitVisualPrefabBuilder.HumanFlyingPath, "Human_Flying",
                "Fly_Hors", "animation_cavalry/cavalry",
                "cavalry_01_idle", "cavalry_03_run", "cavalry_04_attack", "cavalry_06_death_A",
                UnitRole.Flying),
            UnitSetup("Human_Super", UnitVisualPrefabBuilder.HumanSuperPath, "Human_Super",
                "machines/TT_Ballista_lvl3", "animation_machines/Ballista",
                "ballista_01_idle", "ballista_02_move", "ballista_03_attack", "ballista_05_death",
                UnitRole.Super),
            new(
                "Human_Hero1",
                UnitVisualPrefabBuilder.HumanHero1Path,
                HeroControllersFolder + "/Human_Hero1.controller",
                "TT_King",
                "animation_infantry/Infantry",
                "infantry_01_idle",
                "infantry_03_run",
                "infantry_04_attack_A",
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
                "cav_shield_04_attack",
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
                "cav_staff_07_cast_A",
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
                "infantry_04_attack_A",
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
            string attackClip,
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
                attackClip,
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
            UnitBalanceSettings preserved = null;
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

                    var settings = root.GetComponent<UnitBalanceSettings>();
                    if (settings == null)
                    {
                        settings = root.AddComponent<UnitBalanceSettings>();
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

        static UnitBalanceSettings CaptureBalance(string dstPath)
        {
            var destPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(dstPath);
            var existing = destPrefab != null
                ? destPrefab.GetComponentInChildren<UnitBalanceSettings>(true)
                : null;
            if (existing == null)
            {
                return null;
            }

            var temp = new GameObject("TtBalanceCapture");
            temp.hideFlags = HideFlags.HideAndDontSave;
            var copy = temp.AddComponent<UnitBalanceSettings>();
            copy.CopyFrom(existing);
            return copy;
        }

        static void SeedBalance(UnitBalanceSettings settings, VisualSetup setup)
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
                settings.CopyFrom(race.GetHeroBySlot(1), TitanRules.BaseStatMultiplier);
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

        static void SyncControllerClips(AnimatorController controller, VisualSetup setup)
        {
            var sm = controller.layers[0].stateMachine;
            foreach (var child in sm.states)
            {
                var state = child.state;
                if (state.name == UnitCombatAnimatorDriver.StandState)
                {
                    state.motion = LoadClip(setup, setup.IdleClip);
                }
                else if (state.name == UnitCombatAnimatorDriver.WalkState)
                {
                    state.motion = LoadClip(setup, setup.WalkClip);
                }
                else if (state.name == UnitCombatAnimatorDriver.AttackState)
                {
                    state.motion = LoadClip(setup, setup.AttackClip);
                }
                else if (state.name == UnitCombatAnimatorDriver.DeathState)
                {
                    state.motion = LoadClip(setup, setup.DeathClip);
                }
            }

            EditorUtility.SetDirty(controller);
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

        static AnimationClip LoadClip(VisualSetup setup, string clipName)
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
