using System.IO;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Assigns TT_RTS FX prefabs to a <see cref="MatchFxCatalog"/> and wires it into Game.unity.</summary>
    public static class MatchFxSetup
    {
        public const string CatalogPath = "Assets/Game/Resources/Fx/MatchFxCatalog.asset";
        public const string GameScenePath = "Assets/Game/Scenes/Game.unity";

        const string FxPrefabsRoot = "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/FX/FX_prefabs";

        [MenuItem("BARAKI/FX/Setup Scene (TT_RTS)")]
        public static void Run()
        {
            EnsureContent();
            ApplyToScene();
            Debug.Log("[MatchFxSetup] Catalog and Game.unity are configured.");
        }

        public static void EnsureContent()
        {
            EnsureFolder("Assets/Game/Resources/Fx");
            EnsureFolder("Assets/Game/Resources");

            var catalog = LoadOrCreateCatalog();
            catalog.EditorAssign(
                LoadFx("FX_Blood"),
                LoadFx("FX_machine_destroyed"),
                LoadFx("FX_Building_Destroyed_mid"),
                LoadFx("FX_Building_burning"),
                LoadFx("FX_Building_burning_small"));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        static void ApplyToScene()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MatchFxCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new System.InvalidOperationException(
                    $"Missing MatchFxCatalog at {CatalogPath}. Run EnsureContent first.");
            }

            var scene = EditorSceneManager.OpenScene(GameScenePath);
            var runtime = Object.FindAnyObjectByType<MatchRuntime>();
            if (runtime == null)
            {
                throw new System.InvalidOperationException($"No MatchRuntime found in {GameScenePath}.");
            }

            var combatPresenter = runtime.GetComponent<MatchCombatPresenter>();
            if (combatPresenter == null)
            {
                combatPresenter = runtime.gameObject.AddComponent<MatchCombatPresenter>();
            }

            var buildingFx = runtime.GetComponent<MatchBuildingFxPresenter>();
            if (buildingFx == null)
            {
                buildingFx = runtime.gameObject.AddComponent<MatchBuildingFxPresenter>();
            }

            SetFxCatalog(combatPresenter, catalog);
            SetFxCatalog(buildingFx, catalog);
            EditorUtility.SetDirty(runtime);
            EditorSceneManager.SaveScene(scene);
        }

        static void SetFxCatalog(Object component, MatchFxCatalog catalog)
        {
            var serialized = new SerializedObject(component);
            var prop = serialized.FindProperty("_fxCatalog");
            if (prop == null)
            {
                throw new System.InvalidOperationException(
                    $"{component.GetType().Name} has no serialized _fxCatalog field.");
            }

            prop.objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
            if (prop.objectReferenceValue != catalog)
            {
                throw new System.InvalidOperationException(
                    $"[MatchFxSetup] Failed to assign _fxCatalog on {component.GetType().Name}.");
            }
        }

        static GameObject LoadFx(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{FxPrefabsRoot}/{name}.prefab");
            if (prefab == null)
            {
                throw new System.InvalidOperationException($"Missing FX prefab: {FxPrefabsRoot}/{name}.prefab");
            }

            return prefab;
        }

        static MatchFxCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MatchFxCatalog>(CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<MatchFxCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
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
