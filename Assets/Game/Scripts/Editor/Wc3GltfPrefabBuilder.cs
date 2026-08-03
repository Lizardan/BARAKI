using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds prefabs from the 21 glTFast-imported WC3 GLBs under
    /// <c>Assets/Game/Prefabs/UnitsNew/Models</c>:
    /// creates URP materials from the copied PNG textures, builds an AnimatorController
    /// from the GLB animation clips (Stand/Walk/Attack/Death) and saves a prefab per GLB
    /// into <c>Assets/Game/Prefabs/UnitsNew/Prefabs</c>.
    /// </summary>
    public static class Wc3GltfPrefabBuilder
    {
        public const string ModelFolder = "Assets/Game/Prefabs/UnitsNew/Models";
        public const string TextureFolder = "Assets/Game/Prefabs/UnitsNew/Textures";
        public const string MaterialFolder = "Assets/Game/Prefabs/UnitsNew/Materials";
        public const string AnimatorFolder = "Assets/Game/Prefabs/UnitsNew/Animators";
        public const string PrefabFolder = "Assets/Game/Prefabs/UnitsNew/Prefabs";

        static readonly Dictionary<string, string> SpecialTextureMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "TeamGlow00", "teamglow01" },
            { "heroranger", "ranger" },
            { "yellow_star_dim", "white" },
        };

        [InitializeOnLoadMethod]
        static void AutoRunOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(ModelFolder))
            {
                return;
            }

            // Rebuild automatically until every GLB has a prefab (survives partial failures).
            var glbs = AssetDatabase.FindAssets("t:GameObject", new[] { ModelFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
                .Count();
            var prefabs = AssetDatabase.IsValidFolder(PrefabFolder)
                ? AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }).Length
                : 0;
            if (prefabs >= glbs)
            {
                return;
            }

            EditorApplication.delayCall += RebuildAll;
        }

        [MenuItem("BARAKI/Units/Rebuild WC3 GLB Prefabs (UnitsNew)")]
        public static void RebuildFromMenu()
        {
            RebuildAll();
        }

        public static void RebuildAll()
        {
            EnsureFolders();
            CreateMaterialsFromTextures();

            var glbs = AssetDatabase.FindAssets("t:GameObject", new[] { ModelFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p)
                .ToArray();

            var built = 0;
            var failed = new List<string>();
            foreach (var glb in glbs)
            {
                try
                {
                    BuildPrefab(glb);
                    built++;
                }
                catch (Exception e)
                {
                    failed.Add(glb + " : " + e.Message);
                    Debug.LogError("Wc3GltfPrefabBuilder: failed " + glb + " -> " + e);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Wc3GltfPrefabBuilder: rebuilt " + built + "/" + glbs.Length + " UnitsNew prefabs."
                + (failed.Count > 0 ? " FAILED: " + string.Join(" | ", failed) : ""));
        }

        static void EnsureFolders()
        {
            EnsureFolder(ModelFolder);
            EnsureFolder(TextureFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(AnimatorFolder);
            EnsureFolder(PrefabFolder);
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

        static void CreateMaterialsFromTextures()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Lit shader not found.");
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
            {
                var texPath = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null)
                {
                    continue;
                }

                var matPath = MaterialFolder + "/Mat_" + tex.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader) { name = "Mat_" + tex.name };
                    AssetDatabase.CreateAsset(mat, matPath);
                }

                mat.shader = shader;
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(mat);
            }
        }

        static Material ResolveMaterial(string materialName)
        {
            var texKey = ResolveTextureName(materialName);
            var matPath = MaterialFolder + "/Mat_" + texKey + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Mat_white.mat");
            }

            return mat;
        }

        static string ResolveTextureName(string materialName)
        {
            var baseName = materialName;
            var dot = baseName.LastIndexOf('.');
            if (dot > 0 && dot < baseName.Length - 1
                && int.TryParse(baseName.Substring(dot + 1), out _))
            {
                baseName = baseName.Substring(0, dot);
            }

            if (SpecialTextureMap.TryGetValue(baseName, out var special))
            {
                return special;
            }

            var exact = TextureFolder + "/" + baseName + ".png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(exact) != null)
            {
                return baseName;
            }

            // Case-insensitive fallback across the copied texture set.
            var candidates = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => string.Equals(
                    Path.GetFileNameWithoutExtension(p),
                    baseName,
                    StringComparison.OrdinalIgnoreCase));
            return candidates != null ? Path.GetFileNameWithoutExtension(candidates) : "white";
        }

        static void BuildPrefab(string glbPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (model == null)
            {
                throw new InvalidOperationException("Missing model " + glbPath);
            }

            var unitName = Path.GetFileNameWithoutExtension(glbPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(
                instance,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            instance.name = unitName;
            instance.transform.localPosition = Vector3.zero;

            var animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            var controller = CreateAnimatorController(unitName, glbPath);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(animator);

            ApplyMaterials(instance);

            var prefabPath = PrefabFolder + "/" + unitName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
        }

        static void ApplyMaterials(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Cast<Renderer>()
                .Concat(root.GetComponentsInChildren<MeshRenderer>(true));
            foreach (var renderer in renderers)
            {
                var shared = renderer.sharedMaterials;
                if (shared == null || shared.Length == 0)
                {
                    continue;
                }

                var replaced = shared.Select(m =>
                {
                    if (m == null)
                    {
                        return m;
                    }

                    var resolved = ResolveMaterial(m.name);
                    return resolved != null ? resolved : m;
                }).ToArray();
                renderer.sharedMaterials = replaced;
            }
        }

        static AnimatorController CreateAnimatorController(string unitName, string glbPath)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(glbPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("#", StringComparison.Ordinal))
                .ToArray();

            var stand = PickClip(clips, new[] { "Stand - 1", "Stand", "stand" },
                new[] { "Stand" }, new[] { "Ready", "Victory", "Defend", "Work", "Channel", "Walk", "Attack", "Death", "Upgrade" });
            var walk = PickClip(clips, new[] { "Walk", "walk" },
                new[] { "Walk" }, new[] { "Defend", "Attack", "Death", "Stand" });
            var attack = PickClip(clips, new[] { "Attack - 1", "Attack - 2", "Attack", "attack" },
                new[] { "Attack" }, new[] { "Defend", "Slam", "Spin", "Walk", "Alternate" });
            var death = PickClip(clips, new[] { "Death", "death" },
                new[] { "Death" }, new[] { "Bone", "Flesh", "Alternate" });

            // Static buildings may ship zero-length Stand clips (and no Walk/Attack).
            stand = stand ?? clips.FirstOrDefault(c => c.name.Contains("Stand", StringComparison.OrdinalIgnoreCase));
            death = death ?? clips.FirstOrDefault(c => c.name.Contains("Death", StringComparison.OrdinalIgnoreCase));
            walk = walk ?? stand;
            attack = attack ?? stand;

            // Absolute fallback: buildings with only Birth/Decay/Stand Work (BirthMesh, ElvenBarracks, GoblinOutpost).
            var anyUsable = clips.FirstOrDefault(c => c.length > 0.01f) ?? clips.FirstOrDefault();
            stand = stand ?? anyUsable;
            walk = walk ?? anyUsable;
            attack = attack ?? anyUsable;
            death = death ?? anyUsable;

            if (stand == null || death == null)
            {
                var names = string.Join(", ", clips.Select(c => c.name));
                throw new InvalidOperationException(unitName + " has no usable Stand/Death clip. available=[" + names + "]");
            }

            var controllerPath = AnimatorFolder + "/" + unitName + ".controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

            var root = controller.layers[0].stateMachine;
            var standState = root.AddState("Stand");
            standState.motion = stand;
            var walkState = root.AddState("Walk");
            walkState.motion = walk;
            var attackState = root.AddState("Attack");
            attackState.motion = attack;
            var deathState = root.AddState("Death");
            deathState.motion = death;
            root.defaultState = standState;

            var anyAttack = root.AddAnyStateTransition(attackState);
            anyAttack.hasExitTime = false;
            anyAttack.duration = 0.12f;
            anyAttack.canTransitionToSelf = false;
            anyAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

            var anyDeath = root.AddAnyStateTransition(deathState);
            anyDeath.hasExitTime = false;
            anyDeath.duration = 0.12f;
            anyDeath.canTransitionToSelf = false;
            anyDeath.AddCondition(AnimatorConditionMode.If, 0f, "Death");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimationClip PickClip(
            AnimationClip[] clips,
            string[] preferExact,
            string[] preferContains,
            string[] exclude)
        {
            bool Excluded(string name)
            {
                var lower = name.ToLowerInvariant();
                foreach (var ex in exclude)
                {
                    if (lower.Contains(ex.ToLowerInvariant()))
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (var exact in preferExact)
            {
                foreach (var clip in clips)
                {
                    if (string.Equals(clip.name, exact, StringComparison.OrdinalIgnoreCase)
                        && !Excluded(clip.name))
                    {
                        return clip;
                    }
                }
            }

            foreach (var token in preferContains)
            {
                foreach (var clip in clips)
                {
                    if (clip.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0
                        && !Excluded(clip.name))
                    {
                        return clip;
                    }
                }
            }

            return null;
        }
    }
}
