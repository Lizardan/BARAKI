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
    /// Prefabs live under <c>Prefabs/Races/Humans/{Units|BonusUnits}/{Role}/</c> and
    /// <c>Heroes/{HeroN|Titan}/</c> — matching ScriptableObjects layout.
    /// </summary>
    public static class UnitVisualPrefabBuilder
    {
        public const string RootPath = "Assets/Game/Prefabs/Races";
        public const string HumanRoot = RootPath + "/Humans";
        public const string HumanPath = HumanRoot + "/Units";
        public const string HumanBonusUnitsPath = HumanRoot + "/BonusUnits";
        public const string HumanHeroesPath = HumanRoot + "/Heroes";
        public const string CatalogPath = ContentAssetPaths.UnitVisualCatalog;

        public const string FacelessRoot = RootPath + "/Faceless";
        public const string FacelessPath = FacelessRoot + "/Units";
        public const string FacelessHeroesPath = FacelessRoot + "/Heroes";

        public const string FacelessMeleePath = FacelessPath + "/Melee/Faceless_Melee.prefab";
        public const string FacelessRangedPath = FacelessPath + "/Ranged/Faceless_Ranged.prefab";
        public const string FacelessCasterPath = FacelessPath + "/Caster/Faceless_Caster.prefab";
        public const string FacelessSiegePath = FacelessPath + "/Siege/Faceless_Siege.prefab";
        public const string FacelessFlyingPath = FacelessPath + "/Flying/Faceless_Flying.prefab";
        public const string FacelessSuperPath = FacelessPath + "/Super/Faceless_Super.prefab";
        public const string FacelessTitanPath = FacelessHeroesPath + "/Titan/Faceless_Titan.prefab";

        public const string FacelessHero1Path = FacelessHeroesPath + "/Hero1/Faceless_Hero1.prefab";
        public const string FacelessHero2Path = FacelessHeroesPath + "/Hero2/Faceless_Hero2.prefab";
        public const string FacelessHero3Path = FacelessHeroesPath + "/Hero3/Faceless_Hero3.prefab";

        // --- Faceless bonus / veteran prefabs (slots 1–10), mirroring Humans' BonusUnits / BonusHeroes ---
        public const string FacelessBonusUnitsPath = FacelessRoot + "/BonusUnits";
        public const string FacelessBonusHeroesPath = FacelessRoot + "/BonusHeroes";

        public const string FacelessMeleeBonusPath = FacelessBonusUnitsPath + "/Melee/Faceless_Melee_BONUS.prefab";
        public const string FacelessRangedBonusPath = FacelessBonusUnitsPath + "/Ranged/Faceless_Ranged_BONUS.prefab";
        public const string FacelessCasterBonusPath = FacelessBonusUnitsPath + "/Caster/Faceless_Caster_BONUS.prefab";
        public const string FacelessSiegeBonusPath = FacelessBonusUnitsPath + "/Siege/Faceless_Siege_BONUS.prefab";
        public const string FacelessFlyingBonusPath = FacelessBonusUnitsPath + "/Flying/Faceless_Flying_BONUS.prefab";
        public const string FacelessSuperBonusPath = FacelessBonusUnitsPath + "/Super/Faceless_Super_BONUS.prefab";

        public const string FacelessHero1BonusPath = FacelessBonusHeroesPath + "/Hero1/Faceless_Hero1_BONUS.prefab";
        public const string FacelessHero2BonusPath = FacelessBonusHeroesPath + "/Hero2/Faceless_Hero2_BONUS.prefab";
        public const string FacelessHero3BonusPath = FacelessBonusHeroesPath + "/Hero3/Faceless_Hero3_BONUS.prefab";
        public const string FacelessTitanBonusPath = FacelessBonusHeroesPath + "/Titan/Faceless_Titan_BONUS.prefab";

        public const string HumanMeleePath = HumanPath + "/Melee/Human_Melee.prefab";
        public const string HumanRangedPath = HumanPath + "/Ranged/Human_Ranged.prefab";
        public const string HumanCasterPath = HumanPath + "/Caster/Human_Caster.prefab";
        public const string HumanSiegePath = HumanPath + "/Siege/Human_Siege.prefab";
        public const string HumanFlyingPath = HumanPath + "/Flying/Human_Flying.prefab";
        public const string HumanSuperPath = HumanPath + "/Super/Human_Super.prefab";
        public const string HumanTitanPath = HumanHeroesPath + "/Titan/Human_Titan.prefab";

        public const string HumanMeleeBonusPath = HumanBonusUnitsPath + "/Melee/Human_Melee_BONUS.prefab";
        public const string HumanRangedBonusPath = HumanBonusUnitsPath + "/Ranged/Human_Ranged_BONUS.prefab";
        public const string HumanCasterBonusPath = HumanBonusUnitsPath + "/Caster/Human_Caster_BONUS.prefab";
        public const string HumanSiegeBonusPath = HumanBonusUnitsPath + "/Siege/Human_Siege_BONUS.prefab";
        public const string HumanFlyingBonusPath = HumanBonusUnitsPath + "/Flying/Human_Flying_BONUS.prefab";
        public const string HumanSuperBonusPath = HumanBonusUnitsPath + "/Super/Human_Super_BONUS.prefab";

        public const string HumanHero1Path = HumanHeroesPath + "/Hero1/Human_Hero1.prefab";
        public const string HumanHero2Path = HumanHeroesPath + "/Hero2/Human_Hero2.prefab";
        public const string HumanHero3Path = HumanHeroesPath + "/Hero3/Human_Hero3.prefab";

        public const string HumanBonusHeroesPath = HumanRoot + "/BonusHeroes";
        public const string HumanHero1BonusPath = HumanBonusHeroesPath + "/Hero1/Human_Hero1_BONUS.prefab";
        public const string HumanHero2BonusPath = HumanBonusHeroesPath + "/Hero2/Human_Hero2_BONUS.prefab";
        public const string HumanHero3BonusPath = HumanBonusHeroesPath + "/Hero3/Human_Hero3_BONUS.prefab";
        public const string HumanTitanBonusPath = HumanBonusHeroesPath + "/Titan/Human_Titan_BONUS.prefab";

        static readonly string[] HumanAnimatedPrefabPaths =
        {
            HumanMeleePath,
            HumanRangedPath,
            HumanCasterPath,
            HumanSiegePath,
            HumanFlyingPath,
            HumanSuperPath,
        };

        static readonly string[] HumanBonusPrefabPaths =
        {
            HumanMeleeBonusPath,
            HumanRangedBonusPath,
            HumanCasterBonusPath,
            HumanSiegeBonusPath,
            HumanFlyingBonusPath,
            HumanSuperBonusPath,
        };

        static readonly string[] FacelessPrefabPaths =
        {
            FacelessMeleePath,
            FacelessRangedPath,
            FacelessCasterPath,
            FacelessSiegePath,
            FacelessFlyingPath,
            FacelessSuperPath,
        };

        static readonly string[] FacelessBonusPrefabPaths =
        {
            FacelessMeleeBonusPath,
            FacelessRangedBonusPath,
            FacelessCasterBonusPath,
            FacelessSiegeBonusPath,
            FacelessFlyingBonusPath,
            FacelessSuperBonusPath,
        };

        public static void EnsureContent()
        {
            EnsureHumanPrefabFolders();
            EnsureFacelessPrefabFolders();
            EnsureFacelessBonusFolders();
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.Catalogs);

            var humanPrefabs = LoadAnimatedHumanPrefabs();
            var bonusPrefabs = LoadOptionalBonusPrefabs();
            var hero1 = LoadRequiredPrefab(HumanHero1Path);
            var hero2 = LoadRequiredPrefab(HumanHero2Path);
            var hero3 = LoadRequiredPrefab(HumanHero3Path);
            var titan = LoadRequiredPrefab(HumanTitanPath);
            var veteran1 = LoadOptionalPrefab(HumanHero1BonusPath);
            var veteran2 = LoadOptionalPrefab(HumanHero2BonusPath);
            var veteran3 = LoadOptionalPrefab(HumanHero3BonusPath);
            var veteranTitan = LoadOptionalPrefab(HumanTitanBonusPath);
            UpdateCatalogFromPrefabs(
                GameIds.Races.Human,
                humanPrefabs,
                bonusPrefabs,
                hero1,
                hero2,
                hero3,
                titan,
                veteran1,
                veteran2,
                veteran3,
                veteranTitan);

            var facelessUnits = LoadOptionalFacelessPrefabs();
            if (AllPresent(facelessUnits))
            {
                var fHero1 = LoadRequiredPrefab(FacelessHero1Path);
                var fHero2 = LoadRequiredPrefab(FacelessHero2Path);
                var fHero3 = LoadRequiredPrefab(FacelessHero3Path);
                var fTitan = LoadRequiredPrefab(FacelessTitanPath);
                var facelessBonusUnits = LoadOptionalFacelessBonusPrefabs();
                var fHero1Bonus = LoadOptionalPrefab(FacelessHero1BonusPath);
                var fHero2Bonus = LoadOptionalPrefab(FacelessHero2BonusPath);
                var fHero3Bonus = LoadOptionalPrefab(FacelessHero3BonusPath);
                var fTitanBonus = LoadOptionalPrefab(FacelessTitanBonusPath);
                UpdateCatalogFromPrefabs(
                    GameIds.Races.Faceless,
                    facelessUnits,
                    facelessBonusUnits,
                    fHero1,
                    fHero2,
                    fHero3,
                    fTitan,
                    fHero1Bonus,
                    fHero2Bonus,
                    fHero3Bonus,
                    fTitanBonus,
                    clearBonus: false);
            }

            UnitPortraitBaker.BakeIntoCatalog(AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(CatalogPath));
            AssetDatabase.SaveAssets();
        }

        [MenuItem("BARAKI/Faceless/Update Visual Catalog")]
        public static void UpdateFacelessCatalogFromMenu()
        {
            UpdateFacelessCatalog();
            AssetDatabase.SaveAssets();
            Debug.Log("UnitVisualPrefabBuilder: Faceless catalog updated.");
        }

        /// <summary>Registers Faceless unit/hero/titan prefabs into the shared visual catalog.</summary>
        public static void UpdateFacelessCatalog()
        {
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.Catalogs);
            var facelessUnits = LoadOptionalFacelessPrefabs();
            if (!AllPresent(facelessUnits))
            {
                Debug.LogWarning(
                    "UnitVisualPrefabBuilder: not all Faceless unit prefabs present — run " +
                    "'BARAKI/Faceless/Rebuild Unit Prefabs' first.");
                return;
            }

            var fHero1 = LoadRequiredPrefab(FacelessHero1Path);
            var fHero2 = LoadRequiredPrefab(FacelessHero2Path);
            var fHero3 = LoadRequiredPrefab(FacelessHero3Path);
            var fTitan = LoadRequiredPrefab(FacelessTitanPath);
            var facelessBonusUnits = LoadOptionalFacelessBonusPrefabs();
            var fHero1Bonus = LoadOptionalPrefab(FacelessHero1BonusPath);
            var fHero2Bonus = LoadOptionalPrefab(FacelessHero2BonusPath);
            var fHero3Bonus = LoadOptionalPrefab(FacelessHero3BonusPath);
            var fTitanBonus = LoadOptionalPrefab(FacelessTitanBonusPath);
            UpdateCatalogFromPrefabs(
                GameIds.Races.Faceless,
                facelessUnits,
                facelessBonusUnits,
                fHero1,
                fHero2,
                fHero3,
                fTitan,
                fHero1Bonus,
                fHero2Bonus,
                fHero3Bonus,
                fTitanBonus,
                clearBonus: false);

            UnitPortraitBaker.BakeIntoCatalog(AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(CatalogPath));
        }

        /// <summary>Creates the Faceless <c>BonusUnits</c> / <c>BonusHeroes</c> folder tree so the
        /// bonus prefab builder and catalog can register enhanced-variant prefabs.</summary>
        public static void EnsureFacelessBonusFolders()
        {
            EnsureFolder(FacelessBonusUnitsPath);
            EnsureFolder(FacelessBonusUnitsPath + "/Melee");
            EnsureFolder(FacelessBonusUnitsPath + "/Ranged");
            EnsureFolder(FacelessBonusUnitsPath + "/Caster");
            EnsureFolder(FacelessBonusUnitsPath + "/Siege");
            EnsureFolder(FacelessBonusUnitsPath + "/Flying");
            EnsureFolder(FacelessBonusUnitsPath + "/Super");
            EnsureFolder(FacelessBonusHeroesPath);
            EnsureFolder(FacelessBonusHeroesPath + "/Hero1");
            EnsureFolder(FacelessBonusHeroesPath + "/Hero2");
            EnsureFolder(FacelessBonusHeroesPath + "/Hero3");
            EnsureFolder(FacelessBonusHeroesPath + "/Titan");
        }

        public static void EnsureHumanPrefabFolders()
        {
            EnsureFolder(RootPath);
            EnsureFolder(HumanRoot);
            EnsureFolder(HumanPath);
            EnsureFolder(HumanBonusUnitsPath);
            EnsureFolder(HumanHeroesPath);
            EnsureFolder(HumanPath + "/Melee");
            EnsureFolder(HumanPath + "/Ranged");
            EnsureFolder(HumanPath + "/Caster");
            EnsureFolder(HumanPath + "/Siege");
            EnsureFolder(HumanPath + "/Flying");
            EnsureFolder(HumanPath + "/Super");
            EnsureFolder(HumanBonusUnitsPath + "/Melee");
            EnsureFolder(HumanBonusUnitsPath + "/Ranged");
            EnsureFolder(HumanBonusUnitsPath + "/Caster");
            EnsureFolder(HumanBonusUnitsPath + "/Siege");
            EnsureFolder(HumanBonusUnitsPath + "/Flying");
            EnsureFolder(HumanBonusUnitsPath + "/Super");
            EnsureFolder(HumanHeroesPath + "/Hero1");
            EnsureFolder(HumanHeroesPath + "/Hero2");
            EnsureFolder(HumanHeroesPath + "/Hero3");
            EnsureFolder(HumanHeroesPath + "/Titan");
        }

        public static void EnsureFacelessPrefabFolders()
        {
            EnsureFolder(RootPath);
            EnsureFolder(FacelessRoot);
            EnsureFolder(FacelessPath);
            EnsureFolder(FacelessHeroesPath);
            EnsureFolder(FacelessPath + "/Melee");
            EnsureFolder(FacelessPath + "/Ranged");
            EnsureFolder(FacelessPath + "/Caster");
            EnsureFolder(FacelessPath + "/Siege");
            EnsureFolder(FacelessPath + "/Flying");
            EnsureFolder(FacelessPath + "/Super");
            EnsureFolder(FacelessHeroesPath + "/Hero1");
            EnsureFolder(FacelessHeroesPath + "/Hero2");
            EnsureFolder(FacelessHeroesPath + "/Hero3");
            EnsureFolder(FacelessHeroesPath + "/Titan");
        }

        static GameObject[] LoadAnimatedHumanPrefabs()
        {
            var prefabs = new GameObject[HumanAnimatedPrefabPaths.Length];
            for (var i = 0; i < HumanAnimatedPrefabPaths.Length; i++)
            {
                prefabs[i] = LoadRequiredPrefab(HumanAnimatedPrefabPaths[i]);
            }

            return prefabs;
        }

        static GameObject[] LoadOptionalBonusPrefabs()
        {
            var prefabs = new GameObject[HumanBonusPrefabPaths.Length];
            for (var i = 0; i < HumanBonusPrefabPaths.Length; i++)
            {
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(HumanBonusPrefabPaths[i]);
            }

            return prefabs;
        }

        static GameObject[] LoadOptionalFacelessPrefabs()
        {
            var prefabs = new GameObject[FacelessPrefabPaths.Length];
            for (var i = 0; i < FacelessPrefabPaths.Length; i++)
            {
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(FacelessPrefabPaths[i]);
            }

            return prefabs;
        }

        static GameObject[] LoadOptionalFacelessBonusPrefabs()
        {
            var prefabs = new GameObject[FacelessBonusPrefabPaths.Length];
            for (var i = 0; i < FacelessBonusPrefabPaths.Length; i++)
            {
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(FacelessBonusPrefabPaths[i]);
            }

            return prefabs;
        }

        static bool AllPresent(GameObject[] prefabs)
        {
            for (var i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        static GameObject LoadOptionalPrefab(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path);

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
            string raceId,
            GameObject[] prefabs,
            GameObject[] bonusPrefabs,
            GameObject hero1,
            GameObject hero2,
            GameObject hero3,
            GameObject titan,
            GameObject hero1Bonus = null,
            GameObject hero2Bonus = null,
            GameObject hero3Bonus = null,
            GameObject titanBonus = null,
            bool clearBonus = false)
        {
            var catalog = LoadOrCreateCatalog();
            var so = new SerializedObject(catalog);
            var races = so.FindProperty("_races");
            var entry = FindOrAddRace(races, raceId);
            AssignSet(
                entry.FindPropertyRelative("_visuals"),
                prefabs,
                bonusPrefabs,
                hero1,
                hero2,
                hero3,
                titan,
                hero1Bonus,
                hero2Bonus,
                hero3Bonus,
                titanBonus,
                clearBonus);
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
            GameObject[] bonusPrefabs,
            GameObject hero1,
            GameObject hero2,
            GameObject hero3,
            GameObject titan,
            GameObject hero1Bonus = null,
            GameObject hero2Bonus = null,
            GameObject hero3Bonus = null,
            GameObject titanBonus = null,
            bool clearBonus = false)
        {
            setProperty.FindPropertyRelative("_melee").objectReferenceValue = prefabs[0];
            setProperty.FindPropertyRelative("_ranged").objectReferenceValue = prefabs[1];
            setProperty.FindPropertyRelative("_caster").objectReferenceValue = prefabs[2];
            setProperty.FindPropertyRelative("_siege").objectReferenceValue = prefabs[3];
            setProperty.FindPropertyRelative("_flying").objectReferenceValue = prefabs[4];
            setProperty.FindPropertyRelative("_super").objectReferenceValue = prefabs[5];
            if (bonusPrefabs != null && bonusPrefabs.Length >= 6)
            {
                AssignIfPresent(setProperty, "_meleeBonus", bonusPrefabs[0]);
                AssignIfPresent(setProperty, "_rangedBonus", bonusPrefabs[1]);
                AssignIfPresent(setProperty, "_casterBonus", bonusPrefabs[2]);
                AssignIfPresent(setProperty, "_siegeBonus", bonusPrefabs[3]);
                AssignIfPresent(setProperty, "_flyingBonus", bonusPrefabs[4]);
                AssignIfPresent(setProperty, "_superBonus", bonusPrefabs[5]);
            }
            else if (clearBonus)
            {
                setProperty.FindPropertyRelative("_meleeBonus").objectReferenceValue = null;
                setProperty.FindPropertyRelative("_rangedBonus").objectReferenceValue = null;
                setProperty.FindPropertyRelative("_casterBonus").objectReferenceValue = null;
                setProperty.FindPropertyRelative("_siegeBonus").objectReferenceValue = null;
                setProperty.FindPropertyRelative("_flyingBonus").objectReferenceValue = null;
                setProperty.FindPropertyRelative("_superBonus").objectReferenceValue = null;
            }

            setProperty.FindPropertyRelative("_hero1").objectReferenceValue = hero1;
            setProperty.FindPropertyRelative("_hero2").objectReferenceValue = hero2;
            setProperty.FindPropertyRelative("_hero3").objectReferenceValue = hero3;
            setProperty.FindPropertyRelative("_titan").objectReferenceValue = titan;

            if (clearBonus)
            {
                setProperty.FindPropertyRelative("_hero1Bonus").objectReferenceValue = null;
                setProperty.FindPropertyRelative("_hero2Bonus").objectReferenceValue = null;
                setProperty.FindPropertyRelative("_hero3Bonus").objectReferenceValue = null;
                setProperty.FindPropertyRelative("_titanBonus").objectReferenceValue = null;
            }
            else
            {
                if (hero1Bonus != null)
                {
                    setProperty.FindPropertyRelative("_hero1Bonus").objectReferenceValue = hero1Bonus;
                }

                if (hero2Bonus != null)
                {
                    setProperty.FindPropertyRelative("_hero2Bonus").objectReferenceValue = hero2Bonus;
                }

                if (hero3Bonus != null)
                {
                    setProperty.FindPropertyRelative("_hero3Bonus").objectReferenceValue = hero3Bonus;
                }

                if (titanBonus != null)
                {
                    setProperty.FindPropertyRelative("_titanBonus").objectReferenceValue = titanBonus;
                }
            }
        }

        static void AssignIfPresent(SerializedProperty setProperty, string field, GameObject value)
        {
            if (value != null)
            {
                setProperty.FindPropertyRelative(field).objectReferenceValue = value;
            }
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
