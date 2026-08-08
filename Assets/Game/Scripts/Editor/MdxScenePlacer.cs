using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Mdx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// Places every "scene-ready" MDX model (has run + attack + all textures resolved) onto
    /// the active scene as prefab instances. Mirrors MdxModelAudit's readiness filter so the
    /// placed set is identical to what the audit reports.
    /// </summary>
    public static class MdxScenePlacer
    {
        public const string PrefabFolder = "Assets/NewModels/MODEL_PREFAB/Prefabs";

        [MenuItem("BARAKI/Units/Place Scene-ready MDX Models")]
        public static void PlaceFromMenu()
        {
            var ready = MdxModelAudit.Audit().Where(r => r.SceneReady)
                .Select(r => r.Name).OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase).ToList();

            var placed = new List<string>();
            var missing = new List<string>();

            const float spacing = 2.5f;
            int cols = 8;
            int row = 0;
            int col = 0;

            for (var i = 0; i < ready.Count; i++)
            {
                var prefabPath = PrefabFolder + "/" + ready[i] + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    missing.Add(ready[i]);
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
                instance.name = ready[i];
                instance.transform.position = new Vector3(col * spacing, 0f, row * spacing);
                placed.Add(ready[i]);

                col++;
                if (col >= cols)
                {
                    col = 0;
                    row++;
                }
            }

            if (placed.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            Debug.Log("MdxScenePlacer: placed " + placed.Count + " models.\n" + string.Join("\n", placed.Select(n => "  " + n)));
            if (missing.Count > 0)
            {
                Debug.LogWarning("MdxScenePlacer: no prefab for: " + string.Join(", ", missing));
            }

            EditorApplication.Beep();
        }
    }
}