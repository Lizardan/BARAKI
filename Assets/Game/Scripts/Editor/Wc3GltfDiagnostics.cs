using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// One-shot diagnostics for glTFast-imported WC3 GLB prefab scenes.
    /// Logs the main object hierarchy, Animator presence and animation clip bindings
    /// so the Wc3GltfPrefabBuilder can bind materials and clips correctly.
    /// </summary>
    public static class Wc3GltfDiagnostics
    {
        const string ModelFolder = "Assets/Game/Prefabs/UnitsNew/Models";
        const string FlagPref = "BARAKI.Wc3Gltf.Diagnostics.Done.V1";

        [InitializeOnLoadMethod]
        static void AutoRunOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorPrefs.GetBool(FlagPref, false))
            {
                return;
            }

            EditorApplication.delayCall += Run;
        }

        [MenuItem("BARAKI/Units/GLB Diagnostics")]
        public static void Run()
        {
            EditorPrefs.SetBool(FlagPref, true);

            if (!AssetDatabase.IsValidFolder(ModelFolder))
            {
                Debug.Log("Wc3GltfDiagnostics: no Models folder yet.");
                return;
            }

            var glbs = AssetDatabase.FindAssets("t:GameObject", new[] { ModelFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var glb in glbs.OrderBy(p => p))
            {
                LogStructure(glb);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Wc3GltfDiagnostics: done, " + glbs.Length + " glb prefab scenes.");
        }

        static void LogStructure(string glbPath)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (go == null)
            {
                Debug.Log("Wc3GltfDiagnostics: " + glbPath + " main asset is not a GameObject.");
                return;
            }

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(glbPath);
            var clips = subAssets.OfType<AnimationClip>().ToArray();
            var meshes = subAssets.OfType<Mesh>().ToArray();
            var mats = subAssets.OfType<Material>().ToArray();
            var animators = go.GetComponentsInChildren<Animator>(true);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== " + glbPath + " ===");
            sb.AppendLine("main=" + go.name + " children=" + go.transform.childCount);
            sb.AppendLine("animatorOnMain=" + (go.GetComponent<Animator>() != null)
                + " animatorsInTree=" + animators.Length);
            foreach (var a in animators)
            {
                sb.AppendLine("  animator on " + a.gameObject.name
                    + " controller=" + (a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "null"));
            }

            sb.AppendLine("clips=" + clips.Length + " meshes=" + meshes.Length + " mats=" + mats.Length);
            foreach (var c in clips)
            {
                var bindings = AnimationUtility.GetCurveBindings(c);
                var sample = bindings.Length > 0 ? bindings[0].path + "|" + bindings[0].propertyName : "(none)";
                sb.AppendLine("  clip '" + c.name + "' len=" + c.length.ToString("0.00")
                    + " bindings=" + bindings.Length + " first=" + sample);
            }

            foreach (var m in mats)
            {
                sb.AppendLine("  mat '" + m.name + "' shader=" + (m.shader != null ? m.shader.name : "null"));
            }

            DumpTree(sb, go.transform, 0);
            Debug.Log(sb.ToString());
        }

        static void DumpTree(System.Text.StringBuilder sb, Transform t, int depth)
        {
            sb.AppendLine(new string(' ', depth * 2) + "- " + t.name
                + " (SMR=" + (t.GetComponent<SkinnedMeshRenderer>() != null)
                + " MR=" + (t.GetComponent<MeshRenderer>() != null) + ")");
            foreach (Transform child in t)
            {
                DumpTree(sb, child, depth + 1);
            }
        }
    }
}
