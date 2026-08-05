using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds a throwaway preview scene for the FBX conversions in
    /// <c>Assets/NewModels/UnitModels/fbx_test</c>: every preview prefab lined up on a
    /// ground plane, each driven by a single-state AnimatorController playing its Walk
    /// clip (falling back to Stand), with a camera, directional light and an on-screen
    /// control panel (FbxPreviewControls) for toggling Attack/Run loops and team colors.
    /// A reference unit (Human_Melee, REF_SIZE) is added so scaled units can be compared.
    /// </summary>
    public static class FbxPreviewSceneBuilder
    {
        public const string ScenePath = "Assets/NewModels/UnitModels/PreviewScene.unity";
        public const string AnimatorFolder = "Assets/NewModels/UnitModels/animators";

        const string PrefabFolder = FbxTextureBinder.PrefabFolder;
        const string RefPrefabPath = "Assets/Game/Prefabs/Units/Human/Human_Melee.prefab";
        const float RefScale = 0.02f;
        const float Spacing = 5f;
        const string KnightToken = "KnightUnmounted";

        [MenuItem("BARAKI/Units/Build Fbx Preview Scene")]
        public static void BuildScene()
        {
            var prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p)
                .ToArray();

            if (prefabPaths.Length == 0)
            {
                Debug.LogWarning("FbxPreviewSceneBuilder: no preview prefabs under " + PrefabFolder);
                return;
            }

            EnsureFolder(AnimatorFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("PreviewUnits");
            CreateCamera();
            CreateLight();

            var unitScale = ComputeUnitScale(prefabPaths);
            var total = prefabPaths.Length + 1;
            var positions = ComputeRowPositions(total);
            var controls = root.AddComponent<FbxPreviewControls>();

            var refPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RefPrefabPath);
            if (refPrefab != null)
            {
                var refInstance = (GameObject)PrefabUtility.InstantiatePrefab(refPrefab);
                refInstance.name = "REF_SIZE";
                refInstance.transform.SetParent(root.transform, false);
                refInstance.transform.localScale = Vector3.one * RefScale;
                refInstance.transform.position = positions[0];
            }
            else
            {
                Debug.LogWarning("FbxPreviewSceneBuilder: REF_SIZE prefab not found at " + RefPrefabPath);
            }

            var controllers = new List<AnimatorController>();
            for (var i = 0; i < prefabPaths.Length; i++)
            {
                var index = i + 1;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
                if (prefab == null)
                {
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = Path.GetFileNameWithoutExtension(prefabPaths[i]);
                instance.transform.SetParent(root.transform, false);
                instance.transform.localScale = Vector3.one * unitScale;
                var position = positions[index];
                if (instance.name.IndexOf("Gryphon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    position.y = 2f;
                }

                instance.transform.position = position;

                var setup = CreateUnitSetup(instance.name, prefabPaths[i]);
                if (setup.controller == null)
                {
                    continue;
                }

                controllers.Add(setup.controller);
                var animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    animator = instance.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = setup.controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                controls.units.Add(new FbxPreviewControls.UnitRef
                {
                    animator = animator,
                    runClip = setup.walkClip,
                    attackClip = setup.attackClip,
                });
            }

            CreateGround(total * Spacing);
            FitCameraToContents(root);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("FbxPreviewSceneBuilder: saved " + ScenePath + " with " +
                      prefabPaths.Length + " units (scale " + unitScale.ToString("0.000000") +
                      "), " + controllers.Count + " walk animators.");
        }

        /// <summary>
        /// Single uniform scale for every unit so KnightUnmounted matches the visible
        /// height of the REF_SIZE reference (Human_Melee prefab at scale 0.02).
        /// </summary>
        static float ComputeUnitScale(string[] prefabPaths)
        {
            var refPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RefPrefabPath);
            if (refPrefab == null)
            {
                return 1f;
            }

            var refGo = (GameObject)PrefabUtility.InstantiatePrefab(refPrefab);
            refGo.transform.localScale = Vector3.one * RefScale;
            var refHeight = MeasureVisibleHeight(refGo);
            Object.DestroyImmediate(refGo);

            var knightPath = prefabPaths.FirstOrDefault(p =>
                Path.GetFileNameWithoutExtension(p).Contains(KnightToken));
            if (knightPath == null)
            {
                Debug.LogWarning("FbxPreviewSceneBuilder: no knight prefab for scale reference, using 1f");
                return 1f;
            }

            var knightGo = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(knightPath));
            var knightHeight = MeasureVisibleHeight(knightGo);
            Object.DestroyImmediate(knightGo);

            if (refHeight <= 0f || knightHeight <= 0f)
            {
                return 1f;
            }

            return refHeight / knightHeight;
        }

        static float MeasureVisibleHeight(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
            {
                return 0f;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.size.y;
        }

        static Vector3[] ComputeRowPositions(int count)
        {
            var positions = new Vector3[count];
            var start = -(count - 1) * Spacing * 0.5f;
            for (var i = 0; i < count; i++)
            {
                positions[i] = new Vector3(0f, 0f, start + i * Spacing);
            }

            return positions;
        }

        static void CreateCamera()
        {
            var go = new GameObject("PreviewCamera");
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.28f, 0.3f, 0.34f);
            go.AddComponent<AudioListener>();
        }

        static void CreateLight()
        {
            var go = new GameObject("PreviewLight");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static void CreateGround(float rowWidth)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "PreviewGround";
            go.transform.position = new Vector3(0f, -0.02f, 0f);
            go.transform.localScale = new Vector3(Mathf.Max(0.1f, rowWidth * 0.4f / 10f), 1f, Mathf.Max(0.1f, rowWidth / 10f));
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard");
                renderer.sharedMaterial = new Material(shader)
                {
                    color = new Color(0.55f, 0.58f, 0.6f),
                };
            }

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        static void FitCameraToContents(GameObject root)
        {
            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                return;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            var radius = bounds.extents.magnitude;
            var dist = (radius / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)) * 1.6f;
            camera.transform.position = new Vector3(
                bounds.center.x,
                bounds.center.y + dist * 0.55f,
                bounds.center.z - dist * 1.1f);
            camera.transform.LookAt(bounds.center + Vector3.up * bounds.extents.y * 0.3f);
        }

        /// <summary>
        /// Single-state controller looping the Walk clip (Stand fallback). Also resolves
        /// and loops the first Attack clip so the on-screen buttons work.
        /// </summary>
        static (AnimatorController controller, AnimationClip walkClip, AnimationClip attackClip) CreateUnitSetup(
            string displayName, string prefabPath)
        {
            var fbxPath = ResolveFbxPath(prefabPath);
            if (fbxPath == null)
            {
                return (null, null, null);
            }

            var clips = LoadClips(fbxPath);
            if (clips.Length == 0)
            {
                return (null, null, null);
            }

            var walk = PickClip(clips, "Walk", new[] { "defend", "attack", "death" });
            var motion = walk ?? PickClip(clips, "Stand", new[] { "defend", "attack", "death" })
                ?? clips[0];
            EnsureClipLoops(motion, fbxPath);

            var attack = PickClip(clips, "attack", new[] { "defend", "death" });
            if (attack != null)
            {
                EnsureClipLoops(attack, fbxPath);
            }

            clips = LoadClips(fbxPath);
            motion = clips.FirstOrDefault(c => c.name == motion.name) ?? motion;
            attack = clips.FirstOrDefault(c => attack != null && c.name == attack.name) ?? attack;

            var controllerPath = AnimatorFolder + "/" + displayName + ".controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var root = controller.layers[0].stateMachine;
            var state = root.AddState("Play");
            state.motion = motion;
            state.iKOnFeet = true;
            root.defaultState = state;
            EditorUtility.SetDirty(controller);
            return (controller, motion, attack);
        }

        static AnimationClip[] LoadClips(string fbxPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__", System.StringComparison.Ordinal))
                .ToArray();
        }

        static AnimationClip PickClip(AnimationClip[] clips, string token, string[] exclude)
        {
            foreach (var clip in clips)
            {
                var lower = clip.name.ToLowerInvariant();
                var excluded = exclude.Any(e => lower.Contains(e));
                if (!excluded && lower.Contains(token.ToLowerInvariant()))
                {
                    return clip;
                }
            }

            return null;
        }

        static void EnsureClipLoops(AnimationClip clip, string fbxPath)
        {
            if (clip == null)
            {
                return;
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime)
            {
                return;
            }

            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        static string ResolveFbxPath(string prefabPath)
        {
            var name = Path.GetFileNameWithoutExtension(prefabPath);
            var fbxPath = FbxTextureBinder.FbxFolder + "/" + name + ".fbx";
            return AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath) != null ? fbxPath : null;
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
