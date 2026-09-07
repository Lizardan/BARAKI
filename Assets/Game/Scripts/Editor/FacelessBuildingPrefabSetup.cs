using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds Faceless building prefabs from the review meshes (user mapping: 04=Main, 05=Tower,
    /// 03=Barracks; scales/Y calibrated on the TestBuildings scene) and registers the Faceless
    /// race set in <see cref="BuildingVisualCatalog"/>. Prefab contract: root + Model
    /// (baked scale/yaw) + Foundation (procedural pad, inactive) — see BuildingRuinsVisual.
    /// Run via menu BARAKI/Faceless/Rebuild Building Prefabs.
    /// </summary>
    public static class FacelessBuildingPrefabSetup
    {
        const string SourceFolder = "Assets/Nazjatar_Houses_By_Ageron";
        const string ProductionFolder = "Assets/Game/Art/Races/Faceless/Buildings/Production";
        const string ProductionMeshFolder = ProductionFolder + "/Meshes";
        const string ProductionMatFolder = ProductionFolder + "/Mats";
        const string PrefabFolder = "Assets/Game/Prefabs/Races/Faceless/Buildings";
        const string FoundationMatPath = ProductionMatFolder + "/Faceless_Foundation.mat";
        const float ModelYawDegrees = 270f;
        const float FoundationHeight = 0.25f;

        static readonly (string MeshStem, string PrefabName, Vector3 Scale, string BuildingId)[] Buildings =
        {
            ("04_FacelessBuilding", "Faceless_TownHall", new Vector3(4f, 2.5f, 4f), GameIds.Buildings.Main),
            ("05_FacelessBuilding", "Faceless_Tower", new Vector3(3f, 2.5f, 3f), GameIds.Buildings.TowerNw),
            ("03_FacelessBuilding", "Faceless_Barracks", new Vector3(6f, 4f, 6f), GameIds.Buildings.BarracksCenter),
        };

        [MenuItem("BARAKI/Faceless/Rebuild Building Prefabs")]
        public static void RebuildFromMenu()
        {
            RebuildAll();
        }

        public static string RebuildAll()
        {
            ContentAssetPaths.EnsureFolder(ProductionFolder);
            ContentAssetPaths.EnsureFolder(ProductionMeshFolder);
            ContentAssetPaths.EnsureFolder(ProductionMatFolder);
            ContentAssetPaths.EnsureFolder(PrefabFolder);

            var foundationMaterial = EnsureFoundationMaterial();
            var log = new List<string>();
            var prefabPaths = new string[Buildings.Length];

            for (var i = 0; i < Buildings.Length; i++)
            {
                var (meshStem, prefabName, scale, buildingId) = Buildings[i];
                var doc = FacelessMdxDocument.Load(SourceFolder + "/" + MeshFileName(meshStem));
                var (mesh, materials) = FacelessBuildingReviewSetup.BuildStaticMesh(
                    prefabName,
                    doc,
                    ProductionMeshFolder,
                    ProductionMatFolder);
                prefabPaths[i] = PrefabFolder + "/" + prefabName + ".prefab";
                BuildPrefab(prefabName, mesh, materials, scale, buildingId, foundationMaterial, prefabPaths[i]);
                log.Add($"{prefabName}: mesh={mesh.name} scale={scale} mats={materials.Length} foundationDiameter={FoundationSize(buildingId):F2}");
            }

            RegisterInCatalog(prefabPaths);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FacelessBuildingPrefabSetup:\n" + string.Join("\n", log));
            return string.Join("\n", log);
        }

        static string MeshFileName(string stem) => stem switch
        {
            "04_FacelessBuilding" => "8nzj_nazjatar_building_small01_2578738.mdx",
            "05_FacelessBuilding" => "8nzj_nazjatar_building_small03_2406772.mdx",
            "03_FacelessBuilding" => "8nzj_nazjatar_building_small01_2565378.mdx",
            _ => throw new InvalidOperationException("Unknown building stem " + stem),
        };

        static void BuildPrefab(
            string prefabName,
            Mesh mesh,
            Material[] materials,
            Vector3 modelScale,
            string buildingId,
            Material foundationMaterial,
            string prefabPath)
        {
            var root = new GameObject(prefabName);
            try
            {
                var model = new GameObject(BuildingRuinsVisual.ModelName);
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, ModelYawDegrees, 0f);
                model.transform.localScale = modelScale;
                var filter = model.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var renderer = model.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;

                var foundationSize = FoundationSize(buildingId);
                var foundation = GameObject.CreatePrimitive(PrimitiveType.Cube);
                foundation.name = BuildingRuinsVisual.FoundationName;
                foundation.transform.SetParent(root.transform, false);
                foundation.transform.localPosition = new Vector3(0f, FoundationHeight * 0.5f, 0f);
                foundation.transform.localScale = new Vector3(foundationSize, FoundationHeight, foundationSize);
                var foundationRenderer = foundation.GetComponent<MeshRenderer>();
                foundationRenderer.sharedMaterial = foundationMaterial;
                UnityEngine.Object.DestroyImmediate(foundation.GetComponent<Collider>());
                foundation.SetActive(false);
                foundation.transform.SetAsFirstSibling();

                root.AddComponent<FacelessUnitTeamColor>();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static float FoundationSize(string buildingId)
        {
            var diameter = MatchPickFootprint.GetBuildingDiameter(buildingId, margin: 1f);
            return Mathf.Max(1.5f, diameter * 0.72f);
        }

        static Material EnsureFoundationMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FoundationMatPath);
            if (mat != null)
            {
                return mat;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("Missing URP Lit shader");
            }

            mat = new Material(shader) { name = "Faceless_Foundation" };
            mat.SetColor("_BaseColor", new Color(0.42f, 0.41f, 0.4f));
            mat.SetFloat("_Smoothness", 0.15f);
            return FacelessBuildingReviewSetup.SaveAsset(mat, FoundationMatPath);
        }

        static void RegisterInCatalog(string[] prefabPaths)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingVisualCatalog>(BuildingVisualPrefabBuilder.CatalogPath);
            if (catalog == null)
            {
                BuildingVisualPrefabBuilder.EnsureContent();
                catalog = AssetDatabase.LoadAssetAtPath<BuildingVisualCatalog>(BuildingVisualPrefabBuilder.CatalogPath);
            }

            if (catalog == null)
            {
                throw new InvalidOperationException("Missing BuildingVisualCatalog at " + BuildingVisualPrefabBuilder.CatalogPath);
            }

            catalog.EditorAssignRaceSet(
                GameIds.Races.Faceless,
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[0]),
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[1]),
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[2]));
            EditorUtility.SetDirty(catalog);
        }

        [MenuItem("BARAKI/Faceless/Register Building Catalog")]
        public static void RegisterCatalogFromMenu()
        {
            var paths = new string[Buildings.Length];
            for (var i = 0; i < Buildings.Length; i++)
            {
                paths[i] = PrefabFolder + "/" + Buildings[i].PrefabName + ".prefab";
            }

            RegisterInCatalog(paths);
            AssetDatabase.SaveAssets();
            Debug.Log("FacelessBuildingPrefabSetup: Faceless race set registered in BuildingVisualCatalog.");
        }
    }
}
