using System;
using System.IO;
using Game.Core;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds <see cref="UnitVisualCatalog"/> from ready combat unit, hero, and titan prefabs.
    /// The prefabs themselves are authored by <see cref="TtUnitVisualSetup"/> from ToonyTinyPeople models.
    /// </summary>
    public static class UnitVisualPrefabBuilder
    {
        public const string RootPath = "Assets/Game/Prefabs/Races";
        public const string HumanPath = RootPath + "/Humans/Units";
        public const string HumanHeroesPath = RootPath + "/Humans/Heroes";
        public const string CatalogPath = ContentAssetPaths.UnitVisualCatalog;
        public const string HumanMeleePath = HumanPath + "/Human_Melee.prefab";
        public const string HumanRangedPath = HumanPath + "/Human_Ranged.prefab";
        public const string HumanCasterPath = HumanPath + "/Human_Caster.prefab";
        public const string HumanSiegePath = HumanPath + "/Human_Siege.prefab";
        public const string HumanFlyingPath = HumanPath + "/Human_Flying.prefab";
        public const string HumanSuperPath = HumanPath + "/Human_Super.prefab";
        public const string HumanHero1Path = HumanHeroesPath + "/Human_Hero1.prefab";
        public const string HumanHero2Path = HumanHeroesPath + "/Human_Hero2.prefab";
        public const string HumanHero3Path = HumanHeroesPath + "/Human_Hero3.prefab";
        public const string HumanTitanPath = HumanPath + "/Human_Titan.prefab";

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
            EnsureFolder(HumanHeroesPath);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.Catalogs);

            var humanPrefabs = LoadAnimatedHumanPrefabs();
            var hero1 = LoadRequiredPrefab(HumanHero1Path);
            var hero2 = LoadRequiredPrefab(HumanHero2Path);
            var hero3 = LoadRequiredPrefab(HumanHero3Path);
            var titan = LoadRequiredPrefab(HumanTitanPath);
            UpdateCatalogFromPrefabs(humanPrefabs, hero1, hero2, hero3, titan);
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
                prefabs[i] = LoadRequiredPrefab(HumanAnimatedPrefabPaths[i]);
            }

            return prefabs;
        }

        static GameObject LoadRequiredPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Missing unit prefab at '{path}'. " +
                    "Run BARAKI/Units/Rebuild TT Prefabs first.");
            }

            return prefab;
        }

        static void UpdateCatalogFromPrefabs(
            GameObject[] humanPrefabs,
            GameObject hero1,
            GameObject hero2,
            GameObject hero3,
            GameObject titan)
        {
            var catalog = LoadOrCreateCatalog();
            var so = new SerializedObject(catalog);
            var races = so.FindProperty("_races");
            var entry = FindOrAddRace(races, GameIds.Races.Human);
            AssignSet(entry.FindPropertyRelative("_visuals"), humanPrefabs, hero1, hero2, hero3, titan);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        static SerializedProperty FindOrAddRace(SerializedProperty races, string raceId)
        {
            for (var i = 0; i < races.arraySize; i++)
            {
                var entry = races.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("_raceId").stringValue == raceId)
                {
                    return entry;
                }
            }

            var index = races.arraySize;
            races.InsertArrayElementAtIndex(index);
            var created = races.GetArrayElementAtIndex(index);
            created.FindPropertyRelative("_raceId").stringValue = raceId;
            return created;
        }

        static void AssignSet(
            SerializedProperty setProperty,
            GameObject[] prefabs,
            GameObject hero1,
            GameObject hero2,
            GameObject hero3,
            GameObject titan)
        {
            setProperty.FindPropertyRelative("_melee").objectReferenceValue = prefabs[0];
            setProperty.FindPropertyRelative("_ranged").objectReferenceValue = prefabs[1];
            setProperty.FindPropertyRelative("_caster").objectReferenceValue = prefabs[2];
            setProperty.FindPropertyRelative("_siege").objectReferenceValue = prefabs[3];
            setProperty.FindPropertyRelative("_flying").objectReferenceValue = prefabs[4];
            setProperty.FindPropertyRelative("_super").objectReferenceValue = prefabs[5];
            setProperty.FindPropertyRelative("_hero1").objectReferenceValue = hero1;
            setProperty.FindPropertyRelative("_hero2").objectReferenceValue = hero2;
            setProperty.FindPropertyRelative("_hero3").objectReferenceValue = hero3;
            setProperty.FindPropertyRelative("_titan").objectReferenceValue = titan;
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
