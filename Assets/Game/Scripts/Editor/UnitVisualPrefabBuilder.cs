using System;
using System.IO;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds <see cref="UnitVisualCatalog"/> from ready combat unit prefabs (Human × 6 roles).
    /// The prefabs themselves are authored by <see cref="TtUnitVisualSetup"/> from ToonyTinyPeople models.
    /// </summary>
    public static class UnitVisualPrefabBuilder
    {
        public const string RootPath = "Assets/Game/Prefabs/Races";
        public const string HumanPath = RootPath + "/Humans/Units";
        public const string CatalogPath = "Assets/Game/ScriptableObjects/UnitVisualCatalog.asset";
        public const string HumanMeleePath = HumanPath + "/Human_Melee.prefab";
        public const string HumanRangedPath = HumanPath + "/Human_Ranged.prefab";
        public const string HumanCasterPath = HumanPath + "/Human_Caster.prefab";
        public const string HumanSiegePath = HumanPath + "/Human_Siege.prefab";
        public const string HumanFlyingPath = HumanPath + "/Human_Flying.prefab";
        public const string HumanSuperPath = HumanPath + "/Human_Super.prefab";

        static readonly string[] HumanAnimatedPrefabPaths =
        {
            HumanMeleePath,
            HumanRangedPath,
            HumanCasterPath,
            HumanSiegePath,
            HumanFlyingPath,
            HumanSuperPath,
        };

        public static void EnsureContent()
        {
            EnsureFolder(RootPath);
            EnsureFolder(HumanPath);
            EnsureFolder("Assets/Game/ScriptableObjects");

            var humanPrefabs = LoadAnimatedHumanPrefabs();
            UpdateCatalogFromPrefabs(humanPrefabs);
            UnitPortraitBaker.BakeIntoCatalog(AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(CatalogPath));
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// All Human combat roles live under Units/Human (TT models + Animator).
        /// Rebuild via menu BARAKI/Units/Rebuild TT Prefabs when models change.
        /// </summary>
        static GameObject[] LoadAnimatedHumanPrefabs()
        {
            var prefabs = new GameObject[HumanAnimatedPrefabPaths.Length];
            for (var i = 0; i < HumanAnimatedPrefabPaths.Length; i++)
            {
                var path = HumanAnimatedPrefabPaths[i];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Missing unit prefab at '{path}'. " +
                        "Run BARAKI/Units/Rebuild TT Prefabs first.");
                }

                prefabs[i] = prefab;
            }

            return prefabs;
        }

        static void UpdateCatalogFromPrefabs(GameObject[] humanPrefabs)
        {
            var catalog = LoadOrCreateCatalog();
            var so = new SerializedObject(catalog);

            AssignSet(so.FindProperty("_human"), humanPrefabs);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        static void AssignSet(SerializedProperty setProperty, GameObject[] prefabs)
        {
            setProperty.FindPropertyRelative("_melee").objectReferenceValue = prefabs[0];
            setProperty.FindPropertyRelative("_ranged").objectReferenceValue = prefabs[1];
            setProperty.FindPropertyRelative("_caster").objectReferenceValue = prefabs[2];
            setProperty.FindPropertyRelative("_siege").objectReferenceValue = prefabs[3];
            setProperty.FindPropertyRelative("_flying").objectReferenceValue = prefabs[4];
            setProperty.FindPropertyRelative("_super").objectReferenceValue = prefabs[5];
        }

        static UnitVisualCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<UnitVisualCatalog>();
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
