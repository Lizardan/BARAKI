using System.IO;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds <see cref="BuildingVisualCatalog"/> from the three Human building prefabs
    /// authored by <see cref="TtBuildingVisualSetup"/> from ToonyTinyPeople FBX models.
    /// </summary>
    public static class BuildingVisualPrefabBuilder
    {
        public const string CatalogPath = "Assets/Game/Resources/Buildings/BuildingVisualCatalog.asset";

        public static void EnsureContent()
        {
            EnsureFolder("Assets/Game/Resources/Buildings");
            EnsureFolder("Assets/Game/Resources");

            var catalog = LoadOrCreateCatalog();
            var main = AssetDatabase.LoadAssetAtPath<GameObject>(TtBuildingVisualSetup.TownHallPath);
            var tower = AssetDatabase.LoadAssetAtPath<GameObject>(TtBuildingVisualSetup.TowerPath);
            var barracks = AssetDatabase.LoadAssetAtPath<GameObject>(TtBuildingVisualSetup.BarracksPath);

            if (main == null || tower == null || barracks == null)
            {
                throw new System.InvalidOperationException(
                    "Missing building prefabs. Run BARAKI/Buildings/Rebuild TT Prefabs first.");
            }

            catalog.EditorAssign(main, tower, barracks);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        static BuildingVisualCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingVisualCatalog>(CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<BuildingVisualCatalog>();
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
